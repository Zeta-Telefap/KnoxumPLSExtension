using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Characters walk around props: the detours and the guard that stops a character from circling one prop forever.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Characters walk around props (see TickNpcAvoidance).
        private const float AvoidScanInterval = 0.4f;
        private const float AvoidScanRadius = 60f;
        private const float AvoidMaxSize = 25f;
        // A prop only matters to a character if it reaches into this band above the floor.
        private const float AvoidBandBottom = 0.6f;
        private const float AvoidBandTop = 7f;
        // Clearance = the character's radius (limited to this range) + the margin. A leg only counts as blocked
        // when it comes closer to a prop than clearance - slack, so legs that run along a detour (at clearance)
        // never are and a character brushing a prop does not flip between plans.
        private const float AvoidMinClearance = 0.8f;
        private const float AvoidMaxClearance = 1.5f;
        private const float AvoidMargin = 0.15f;
        private const float AvoidSlack = 0.3f;
        // A character already closer to a prop than that keeps the margin it has minus the shrink; one that
        // touches the prop (closer than the touch distance) ignores it.
        private const float AvoidShrink = 0.1f;
        private const float AvoidTouch = 0.1f;
        // Detour points lie this far outside the clearance rectangle, so legs along it never count as inside.
        private const float AvoidPad = 0.05f;
        private const float AvoidEpsilon = 0.01f;
        // Guards against detours that never end (see ChangeAllowed and ReachedAgain).
        private const float AvoidReach = 1f;
        private const float AvoidSameCorner = 0.75f;
        private const float AvoidWindow = 3f;
        private const int AvoidMaxChanges = 8;
        private const float AvoidLoopWindow = 25f;
        private const int AvoidLoopVisits = 2;
        private const float AvoidLoopPause = 10f;
        // A blocked leg with no way round is looked at again after this long, not every frame.
        private const float AvoidRetryDelay = 0.15f;
        private const int AvoidForgetFrames = 600;
        // A detour point must keep this half width clear of the walls.
        private const float AvoidWallClearance = 0.6f;
        private const int AvoidMaxPoints = 16;
        private const float AvoidLogInterval = 3f;
        // Navigator.destinationPoints is protected: the list is read and edited through reflection.
        private static readonly FieldInfo NavigatorPointsField = typeof(Navigator).GetField(
            "destinationPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Vector3 AvoidWallHalf = new Vector3(AvoidWallClearance, 1f, AvoidWallClearance);
        private static readonly float[] AvoidCornerX = new float[4];
        private static readonly float[] AvoidCornerZ = new float[4];

        /// <summary>A prop near a character: its footprint, the zone a leg must stay out of, and whether it is part of the obstacle being walked around.</summary>
        private struct AvoidNearProp
        {
            public PropRect Raw;
            public PropRect Hard;
            public bool InCluster;
        }

        /// <summary>What the walk-around remembers about one character.</summary>
        private sealed class AvoidState
        {
            public bool HasCommit;
            public Vector3 Commit;
            public int Changes;
            public float WindowEnd;
            public float BlockedUntil;
            public float RetryAt;
            public readonly Vector3[] Reached = new Vector3[8];
            public readonly float[] ReachedAt = new float[8];
            public int ReachedCount;
            public int ReachedNext;
            public int LastFrame;
        }
        // Footprints of the props near any character (refreshed a few times per second) and, per character
        // and frame, the ones its next leg can touch.
        private readonly List<Vector3> _avoidNpcPositions = new List<Vector3>();
        private readonly List<PropRect> _avoidProps = new List<PropRect>();
        private readonly List<AvoidNearProp> _avoidNear = new List<AvoidNearProp>();
        private readonly Dictionary<int, AvoidState> _avoidStates = new Dictionary<int, AvoidState>();
        private readonly List<int> _avoidForget = new List<int>();

        // ------------------------------------------------------------------------------------------
        // Characters walk around props
        // ------------------------------------------------------------------------------------------
        // The vanilla Navigator moves a character along its path points by plain transform.position changes,
        // so nothing but the walls ever stopped it and props were simply walked through. A character whose next
        // leg crosses a prop now gets a detour: one or two corner points of the obstacle's rectangle (the
        // prop, or props standing too close together to pass between, plus the character's radius) are put in
        // front of that leg in Navigator.destinationPoints (a protected list). Every leg of the detour is
        // checked against the walls and against the other props; when no such detour exists the character
        // walks on as before. A detour is left alone until its first point is reached, and a character that
        // keeps circling (or changes plan too often) walks on straight for a while. Nobody kicks, pushes or
        // launches a prop.

        private void TickNpcAvoidance()
        {
            EnvironmentController environment = _player.ec;
            if (NavigatorPointsField == null || environment == null || environment.Npcs == null
                || environment.Npcs.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextAvoidScanTime)
            {
                _nextAvoidScanTime = now + AvoidScanInterval;
                RefreshAvoidProps(environment);
                ForgetAvoidStates();
            }

            if (_avoidProps.Count == 0)
            {
                return;
            }

            List<NPC> npcs = environment.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                try
                {
                    DetourAroundProps(npcs[i], environment);
                }
                catch (Exception exception)
                {
                    LogEventError("character walking around a prop", exception);
                }
            }
        }

        /// <summary>
        /// The footprints (renderer bounds on the floor plane) of the props near any character, a couple of
        /// times per second. Props that are carried or in the air are not obstacles; they are when they land.
        /// </summary>
        private void RefreshAvoidProps(EnvironmentController environment)
        {
            _avoidProps.Clear();
            List<NPC> npcs = environment.Npcs;
            _avoidNpcPositions.Clear();
            for (int n = 0; n < npcs.Count; n++)
            {
                NPC npc = npcs[n];
                if (npc != null)
                {
                    _avoidNpcPositions.Add(npc.transform.position);
                }
            }

            if (_avoidNpcPositions.Count == 0)
            {
                return;
            }

            float reachSqr = AvoidScanRadius * AvoidScanRadius;
            for (int i = 0; i < _proxies.Count; i++)
            {
                // The registry's own position and box (the box is kept until the prop moves), so a prop far from every
                // character costs a few distance tests, and a near one costs nothing more once its box is known.
                ProxyProp proxy = _proxies[i];
                Vector3 position = proxy.Position;
                bool near = false;
                for (int n = 0; n < _avoidNpcPositions.Count && !near; n++)
                {
                    float dx = _avoidNpcPositions[n].x - position.x;
                    float dz = _avoidNpcPositions[n].z - position.z;
                    near = dx * dx + dz * dz <= reachSqr;
                }

                if (!near || proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy
                    || (_held != null && proxy.Root == _held.Root) || IsReleased(proxy.Root))
                {
                    continue;
                }

                Bounds bounds = new Bounds();
                if (!ProxyBox(proxy, out bounds)
                    || bounds.max.y < AvoidBandBottom || bounds.min.y > AvoidBandTop
                    || bounds.size.x > AvoidMaxSize || bounds.size.z > AvoidMaxSize)
                {
                    continue;
                }

                PropRect rect = new PropRect();
                rect.Root = proxy.Root;
                rect.MinX = bounds.min.x;
                rect.MaxX = bounds.max.x;
                rect.MinZ = bounds.min.z;
                rect.MaxZ = bounds.max.z;
                _avoidProps.Add(rect);
            }
        }

        /// <summary>
        /// One character, every frame: look at the leg it is walking (its position to its next path point)
        /// and, when a prop is in the way, put the corner points of a detour in front of it.
        /// </summary>
        private void DetourAroundProps(NPC npc, EnvironmentController environment)
        {
            Navigator navigator = npc != null && npc.isActiveAndEnabled ? npc.Navigator : null;
            if (navigator == null || !navigator.HasDestination)
            {
                return;
            }

            Entity entity = navigator.Entity;
            if (entity == null || !entity.isActiveAndEnabled || entity.Frozen || entity.InteractionDisabled)
            {
                return;
            }

            List<Vector3> points = NavigatorPointsField.GetValue(navigator) as List<Vector3>;
            if (points == null || points.Count == 0 || points.Count > AvoidMaxPoints)
            {
                return;
            }

            AvoidState state = StateOf(npc);
            float now = Time.time;
            if (now < state.BlockedUntil || now < state.RetryAt)
            {
                return;
            }

            Vector3 start = navigator.transform.position;
            if (state.HasCommit)
            {
                // The detour put in front of this character is still being walked: leave it alone.
                if (Distance2D(points[0], state.Commit) < 0.0001f)
                {
                    return;
                }

                // Its first point is gone: reached, or the Navigator built a new path.
                state.HasCommit = false;
                if (Distance2D(start, state.Commit) < AvoidReach && ReachedAgain(npc, state, state.Commit, now))
                {
                    return;
                }
            }

            Vector3 next = points[0];
            float clearance = Mathf.Clamp(navigator.Radius, AvoidMinClearance, AvoidMaxClearance) + AvoidMargin;
            float hard = clearance - AvoidSlack;

            // The props this leg can touch. A prop blocks the leg when the leg comes closer to it than `hard`;
            // props whose zones overlap form one obstacle (nobody fits between them) and a detour goes round
            // all of it. The detour leaves the leg's box, so the box grows until the obstacle fits in it.
            float segLowX = Mathf.Min(start.x, next.x) - clearance;
            float segHighX = Mathf.Max(start.x, next.x) + clearance;
            float segLowZ = Mathf.Min(start.z, next.z) - clearance;
            float segHighZ = Mathf.Max(start.z, next.z) + clearance;
            float lowX = segLowX;
            float highX = segHighX;
            float lowZ = segLowZ;
            float highZ = segHighZ;
            int blocker = -1;
            PropRect corners = new PropRect();
            for (int expand = 0; expand < 3; expand++)
            {
                CollectNearProps(start, hard, lowX, highX, lowZ, highZ);
                blocker = FirstBlocker(start, next);
                if (blocker < 0)
                {
                    return;
                }

                corners = ClusterBounds(blocker, clearance + AvoidPad);
                float boxLowX = Mathf.Min(segLowX, corners.MinX - clearance);
                float boxHighX = Mathf.Max(segHighX, corners.MaxX + clearance);
                float boxLowZ = Mathf.Min(segLowZ, corners.MinZ - clearance);
                float boxHighZ = Mathf.Max(segHighZ, corners.MaxZ + clearance);
                if (boxLowX >= lowX && boxHighX <= highX && boxLowZ >= lowZ && boxHighZ <= highZ)
                {
                    break;
                }

                lowX = Mathf.Min(lowX, boxLowX);
                highX = Mathf.Max(highX, boxHighX);
                lowZ = Mathf.Min(lowZ, boxLowZ);
                highZ = Mathf.Max(highZ, boxHighZ);
            }

            if (InsideCluster(next.x, next.z))
            {
                // The next path point lies inside the obstacle (a cell centre under a table). The destination
                // itself cannot be moved; any other point is skipped if the next one can be walked to
                // straight, else it moves to the nearest free spot beside the obstacle.
                if (points.Count < 2 || !ChangeAllowed(state, now))
                {
                    return;
                }

                if (LegClear(environment, start, points[1], false))
                {
                    points.RemoveAt(0);
                }
                else
                {
                    Vector3 moved;
                    if (TryPushOut(environment, start, next, corners, out moved))
                    {
                        points[0] = moved;
                        state.Commit = moved;
                        state.HasCommit = true;
                    }
                }

                return;
            }

            AvoidCornerX[0] = corners.MinX;
            AvoidCornerZ[0] = corners.MinZ;
            AvoidCornerX[1] = corners.MaxX;
            AvoidCornerZ[1] = corners.MinZ;
            AvoidCornerX[2] = corners.MaxX;
            AvoidCornerZ[2] = corners.MaxZ;
            AvoidCornerX[3] = corners.MinX;
            AvoidCornerZ[3] = corners.MaxZ;

            // One corner, or two neighbouring ones: the shortest way around that is clear all along. When the
            // far end of the leg is cluttered with other props, the second pass accepts a way that is clear up
            // to the last corner and past this obstacle; the next one gets its own detour when the character is there.
            float bestLength = float.MaxValue;
            int bestFirst = -1;
            int bestSecond = -1;
            for (int pass = 0; pass < 2 && bestFirst < 0; pass++)
            {
                for (int first = 0; first < 4; first++)
                {
                    for (int variant = 0; variant < 3; variant++)
                    {
                        int second = variant == 0 ? -1 : (variant == 1 ? (first + 1) & 3 : (first + 3) & 3);
                        float length;
                        if (DetourClear(environment, start, next, first, second, pass == 1, out length)
                            && length < bestLength)
                        {
                            bestLength = length;
                            bestFirst = first;
                            bestSecond = second;
                        }
                    }
                }
            }

            if (bestFirst < 0)
            {
                // Wall to wall, or a cluster of props: no way round, so the character walks on as it always did.
                state.RetryAt = now + AvoidRetryDelay;
                return;
            }

            if (!ChangeAllowed(state, now))
            {
                return;
            }

            if (bestSecond >= 0)
            {
                points.Insert(0, new Vector3(AvoidCornerX[bestSecond], next.y, AvoidCornerZ[bestSecond]));
            }

            Vector3 firstPoint = new Vector3(AvoidCornerX[bestFirst], next.y, AvoidCornerZ[bestFirst]);
            points.Insert(0, firstPoint);
            state.Commit = firstPoint;
            state.HasCommit = true;
            if (Time.unscaledTime >= _nextAvoidLogTime)
            {
                _nextAvoidLogTime = Time.unscaledTime + AvoidLogInterval;
                Transform blockRoot = _avoidNear[blocker].Raw.Root;
                Debug.Log("[Lifter Frenzy] " + npc.name + " walks around "
                    + (blockRoot != null ? blockRoot.name : "a prop") + " (" + (bestSecond >= 0 ? 2 : 1)
                    + " detour point(s), " + bestLength.ToString("0.0") + " units).");
            }
        }

        private AvoidState StateOf(NPC npc)
        {
            int id = npc.GetInstanceID();
            AvoidState state;
            if (!_avoidStates.TryGetValue(id, out state))
            {
                state = new AvoidState();
                _avoidStates[id] = state;
            }

            state.LastFrame = Time.frameCount;
            return state;
        }

        private void ForgetAvoidStates()
        {
            if (_avoidStates.Count == 0)
            {
                return;
            }

            _avoidForget.Clear();
            foreach (KeyValuePair<int, AvoidState> pair in _avoidStates)
            {
                if (Time.frameCount - pair.Value.LastFrame > AvoidForgetFrames)
                {
                    _avoidForget.Add(pair.Key);
                }
            }

            for (int i = 0; i < _avoidForget.Count; i++)
            {
                _avoidStates.Remove(_avoidForget[i]);
            }
        }

        /// <summary>
        /// Guard against detours that never end: a character has only so many plan changes per window, and a
        /// corner it keeps coming back to means it is circling. Either way it walks on straight for a while.
        /// </summary>
        private static bool ChangeAllowed(AvoidState state, float now)
        {
            if (now > state.WindowEnd)
            {
                state.WindowEnd = now + AvoidWindow;
                state.Changes = 0;
            }

            if (state.Changes >= AvoidMaxChanges)
            {
                state.BlockedUntil = state.WindowEnd;
                return false;
            }

            state.Changes++;
            return true;
        }

        private bool ReachedAgain(NPC npc, AvoidState state, Vector3 corner, float now)
        {
            int visits = 0;
            for (int i = 0; i < state.ReachedCount; i++)
            {
                if (now - state.ReachedAt[i] < AvoidLoopWindow && Distance2D(state.Reached[i], corner) < AvoidSameCorner)
                {
                    visits++;
                }
            }

            state.Reached[state.ReachedNext] = corner;
            state.ReachedAt[state.ReachedNext] = now;
            state.ReachedNext = (state.ReachedNext + 1) % state.Reached.Length;
            state.ReachedCount = Mathf.Min(state.ReachedCount + 1, state.Reached.Length);
            if (visits < AvoidLoopVisits)
            {
                return false;
            }

            state.BlockedUntil = now + AvoidLoopPause;
            if (Time.unscaledTime >= _nextAvoidLogTime)
            {
                _nextAvoidLogTime = Time.unscaledTime + AvoidLogInterval;
                Debug.Log("[Lifter Frenzy] " + npc.name + " keeps circling props; it walks on straight for "
                    + AvoidLoopPause.ToString("0") + " s.");
            }

            return true;
        }

        /// <summary>
        /// The props within the box, each with the zone a leg must stay out of. A character that already stands
        /// closer to a prop than that keeps the margin it has (a prop it touches is ignored), so it is never
        /// forced the long way round a prop it is next to.
        /// </summary>
        private void CollectNearProps(Vector3 start, float hard, float lowX, float highX, float lowZ, float highZ)
        {
            _avoidNear.Clear();
            for (int i = 0; i < _avoidProps.Count; i++)
            {
                PropRect prop = _avoidProps[i];
                if (prop.MaxX < lowX || prop.MinX > highX || prop.MaxZ < lowZ || prop.MinZ > highZ)
                {
                    continue;
                }

                float zone = hard;
                float away = DistanceToRect(prop, start.x, start.z);
                if (away < hard + AvoidEpsilon)
                {
                    if (away < AvoidTouch)
                    {
                        continue;
                    }

                    zone = away - AvoidShrink;
                }

                AvoidNearProp item = new AvoidNearProp();
                item.Raw = prop;
                item.Hard = GrowRect(prop, zone);
                _avoidNear.Add(item);
            }
        }

        /// <summary>The prop whose zone the leg enters first, or -1.</summary>
        private int FirstBlocker(Vector3 start, Vector3 next)
        {
            int blocker = -1;
            float blockerEntry = float.MaxValue;
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                float entry;
                if (SegmentEntersRect(start.x, start.z, next.x, next.z, _avoidNear[i].Hard, out entry)
                    && entry < blockerEntry)
                {
                    blocker = i;
                    blockerEntry = entry;
                }
            }

            return blocker;
        }

        /// <summary>
        /// Marks the obstacle the blocking prop belongs to (props whose zones overlap, transitively) and returns
        /// its outline: the footprints' bounds grown by offset. A detour uses the corners of that rectangle.
        /// </summary>
        private PropRect ClusterBounds(int blocker, float offset)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                SetCluster(i, i == blocker);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < _avoidNear.Count; i++)
                {
                    if (_avoidNear[i].InCluster)
                    {
                        continue;
                    }

                    for (int j = 0; j < _avoidNear.Count; j++)
                    {
                        if (_avoidNear[j].InCluster && RectsOverlap(_avoidNear[i].Hard, _avoidNear[j].Hard))
                        {
                            SetCluster(i, true);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            PropRect bounds = _avoidNear[blocker].Raw;
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (!_avoidNear[i].InCluster)
                {
                    continue;
                }

                PropRect raw = _avoidNear[i].Raw;
                bounds.MinX = Mathf.Min(bounds.MinX, raw.MinX);
                bounds.MaxX = Mathf.Max(bounds.MaxX, raw.MaxX);
                bounds.MinZ = Mathf.Min(bounds.MinZ, raw.MinZ);
                bounds.MaxZ = Mathf.Max(bounds.MaxZ, raw.MaxZ);
            }

            return GrowRect(bounds, offset);
        }

        private void SetCluster(int index, bool value)
        {
            AvoidNearProp item = _avoidNear[index];
            item.InCluster = value;
            _avoidNear[index] = item;
        }

        private bool InsideCluster(float x, float z)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (_avoidNear[i].InCluster && InsideRect(_avoidNear[i].Hard, x, z))
                {
                    return true;
                }
            }

            return false;
        }

        private bool LegEntersCluster(Vector3 origin, Vector3 target)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                float entry;
                if (_avoidNear[i].InCluster
                    && SegmentEntersRect(origin.x, origin.z, target.x, target.z, _avoidNear[i].Hard, out entry))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
