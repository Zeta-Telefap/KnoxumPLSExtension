using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Flood: the whirlpools that swallow a prop and give it back out of another one.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Flood: the numbers of Whirlpool (maxForce 20, catches at 5 units, sinkSpeed 2, down to 20%, an exit
        // whirlpool that forms for 3 s).
        private const float WhirlpoolPull = 20f;
        private const float WhirlpoolSinkDistance = 5f;
        private const float WhirlpoolSinkSpeed = 2f;
        private const float WhirlpoolSinkFloor = 0.2f;
        private const float WhirlpoolExitFormSeconds = 3f;
        // The prop goes down by its own height plus this (the floor hides it) while the sink percent goes to 20%.
        private const float WhirlpoolDropMargin = 0.3f;
        private const float WhirlpoolGlideSpeed = 12f;
        private const float WhirlpoolFloorTolerance = 0.3f;
        private const float WhirlpoolMinHoldPull = 0.3f;
        private const int WhirlpoolClosingLayer = 19;
        // The magnet for props. The vanilla pull is zero at the edge of the sphere and a prop that is thrown past
        // a pool has its own speed, so a prop within this far of the sphere is caught: it is pulled at least this
        // fast (units per second), a bit stronger than an entity, and its own sideways speed dies out (rate per
        // second, stronger toward the middle).
        private const float WhirlpoolMagnetExtra = 4f;
        // A prop that is pulled this long without reaching the middle (something in the way) is let go.
        private const float WhirlpoolMaxPullSeconds = 10f;
        private const float WhirlpoolMinSlide = 5f;
        private const float WhirlpoolPropBoost = 1.5f;
        private const float WhirlpoolCatchDamp = 6f;

        // Whirlpool.Close() (private) shrinks the pool and destroys it; Whirlpool.Update starts it when it catches
        // an entity, so a pool that has caught a prop does the same.
        private static readonly MethodInfo WhirlpoolCloseMethod = typeof(Whirlpool).GetMethod(
            "Close", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo WhirlpoolSphereField = typeof(Whirlpool).GetField(
            "sphereCollider", BindingFlags.Instance | BindingFlags.NonPublic);
        private bool _floodRunning;
        private Whirlpool[] _whirlpools = new Whirlpool[0];
        private SphereCollider[] _poolSpheres = new SphereCollider[0];

        private void RefreshWhirlpools(FloodEvent flood)
        {
            List<Whirlpool> list = flood != null ? flood.whirlpools : null;
            int count = list != null ? list.Count : 0;
            _whirlpools = new Whirlpool[count];
            _poolSpheres = new SphereCollider[count];
            int alive = 0;
            for (int i = 0; i < count; i++)
            {
                _whirlpools[i] = list[i];
                _poolSpheres[i] = PoolSphere(list[i]);
                if (list[i] != null)
                {
                    alive++;
                }
            }

            LogEventObjects("Flood: whirlpools", alive, ref _loggedWhirlpools);
        }

        /// <summary>The sphere trigger that Whirlpool itself uses for its radius (its private sphereCollider).</summary>
        private static SphereCollider PoolSphere(Whirlpool pool)
        {
            if (pool == null)
            {
                return null;
            }

            SphereCollider sphere = null;
            try
            {
                if (WhirlpoolSphereField != null)
                {
                    sphere = WhirlpoolSphereField.GetValue(pool) as SphereCollider;
                }
            }
            catch (Exception)
            {
                sphere = null;
            }

            return sphere != null ? sphere : pool.GetComponentInChildren<SphereCollider>();
        }

        // --- Flood: whirlpools ---

        private bool WhirlpoolShape(int index, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0f;
            Whirlpool pool = _whirlpools[index];
            SphereCollider sphere = _poolSpheres[index];
            // The sphere is enabled once the pool has formed; a closing pool (layer 19) pulls nothing.
            if (pool == null || sphere == null || !pool.isActiveAndEnabled || !sphere.enabled
                || pool.gameObject.layer == WhirlpoolClosingLayer)
            {
                return false;
            }

            Vector3 scale = sphere.transform.lossyScale;
            radius = sphere.radius * Mathf.Max(scale.x, scale.z);
            centre = pool.transform.position;
            return radius > 0.5f;
        }

        /// <summary>
        /// The pull of Whirlpool.Update, made a magnet for props. A prop within WhirlpoolMagnetExtra of the
        /// pool's sphere is caught: the sideways speed it came with dies out (v is changed here) and it is pulled
        /// to the middle of the pool at the vanilla speed, (R - d) / R * 20 units per second, times
        /// WhirlpoolPropBoost, never slower than WhirlpoolMinSlide and never more than the distance left; a wall
        /// in between stops it. At 5 units from the middle, on the floor, the prop starts to sink (FxStepSink).
        /// The pull is a drift on top of the velocity: the caller adds it and takes it off again after the step.
        /// </summary>
        private Vector3 FxDrift(CarryState state, Vector3 c, Vector3 half, float dt, ref Vector3 v)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return Vector3.zero;
            }

            fx.Pulled = false;
            if (_whirlpools.Length == 0 || fx.PullGaveUp)
            {
                return Vector3.zero;
            }

            try
            {
                Bounds box = new Bounds(c, half * 2f);
                float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
                int bestIndex = -1;
                float bestSpeed = 0f;
                float bestFlat = 0f;
                float bestZone = 1f;
                Vector3 bestCentre = Vector3.zero;
                Vector3 bestDirection = Vector3.zero;
                for (int i = 0; i < _whirlpools.Length; i++)
                {
                    Vector3 centre;
                    float radius;
                    if (!WhirlpoolShape(i, out centre, out radius))
                    {
                        continue;
                    }

                    float zone = radius + WhirlpoolMagnetExtra;
                    if (box.SqrDistance(centre) > zone * zone)
                    {
                        continue;
                    }

                    Vector3 flat = new Vector3(centre.x - c.x, 0f, centre.z - c.z);
                    float distance = flat.magnitude;
                    float pull = (radius - Mathf.Min(distance, radius)) / radius * WhirlpoolPull * timeScale;
                    float speed = Mathf.Max(pull * WhirlpoolPropBoost, WhirlpoolMinSlide * timeScale);
                    FlightHit wall;
                    if (speed <= bestSpeed
                        || (FlightGridSweep(state.Environment, c, Vector3.zero, flat, false, out wall)
                            && wall.Kind == FlightHitKind.Wall))
                    {
                        continue;
                    }

                    bestIndex = i;
                    bestSpeed = speed;
                    bestFlat = distance;
                    bestZone = zone;
                    bestCentre = centre;
                    bestDirection = distance > 0.001f ? flat / distance : Vector3.zero;
                }

                if (bestIndex < 0)
                {
                    return Vector3.zero;
                }

                if (bestFlat <= WhirlpoolSinkDistance && c.y - half.y <= state.FlightFloorY + WhirlpoolFloorTolerance)
                {
                    FxBeginSink(state, fx, _whirlpools[bestIndex], bestCentre);
                    return Vector3.zero;
                }

                // The catch: the speed a thrown prop brings dies out, faster toward the middle. It is taken off
                // before the drift is added (a drift inside v would be damped too, and taking it off later would
                // turn it around).
                float closeness = 1f - Mathf.Clamp01(bestFlat / bestZone);
                float keep = Mathf.Exp(-WhirlpoolCatchDamp * (0.25f + 0.75f * closeness) * dt);
                v.x *= keep;
                v.z *= keep;

                float pullSpeed = bestSpeed;
                if (pullSpeed * dt > bestFlat)
                {
                    pullSpeed = bestFlat / Mathf.Max(dt, 0.0001f);
                }

                Vector3 drift = bestDirection * pullSpeed;
                // Inside the sink distance the prop never settles before it has sunk, however short the last pull.
                fx.Pulled = bestFlat <= WhirlpoolSinkDistance
                    || drift.sqrMagnitude >= WhirlpoolMinHoldPull * WhirlpoolMinHoldPull;
                if (fx.Pulled)
                {
                    if (!fx.PullLogged)
                    {
                        fx.PullLogged = true;
                        Debug.Log("[Lifter Frenzy] A whirlpool pulls " + state.Root.name + " in ("
                            + bestFlat.ToString("0.0") + " from its middle).");
                    }

                    fx.PulledSeconds += dt;
                    if (fx.PulledSeconds > WhirlpoolMaxPullSeconds)
                    {
                        // Something keeps the prop from reaching the middle: let it go and land where it is.
                        fx.PullGaveUp = true;
                        fx.Pulled = false;
                        return Vector3.zero;
                    }
                }

                return drift;
            }
            catch (Exception exception)
            {
                LogEventError("whirlpool pull", exception);
                return Vector3.zero;
            }
        }

        /// <summary>
        /// A prop that is pulled along the floor of a pool's area stays in the air as far as the sound goes: the
        /// whoosh ends and the quiet LockDoorStop plays, once (like the landing of any other throw).
        /// </summary>
        private static void FxFloorContact(CarryState state, Vector3 c)
        {
            EventFx fx = state.Fx;
            if (fx == null || fx.FloorTouched)
            {
                return;
            }

            fx.FloorTouched = true;
            StopFlightSound(state);
            PlaySoundAt(c, FloorSoundName, FloorSoundVolume);
        }

        /// <summary>
        /// What Whirlpool.Update does when it catches an entity, for a prop: the pool takes this one prop and
        /// closes (layer 19, Close(): it shrinks and is destroyed), so a pool takes one prop. The prop comes out of
        /// another whirlpool of the flood, picked at random; that pool closes too once the prop is out (as the new
        /// whirlpool of Whirlpool.Teleport does). Only when no other whirlpool is left does it happen as in
        /// Whirlpool.Teleport: a new whirlpool forms at a random cell and closes again when the prop is out.
        /// </summary>
        private void FxBeginSink(CarryState state, EventFx fx, Whirlpool pool, Vector3 centre)
        {
            fx.Sinking = true;
            fx.SinkRising = false;
            fx.SinkPool = pool;
            fx.SinkCentre = centre;
            fx.SinkPercent = 1f;
            fx.KeepLanding = true;
            fx.HasExit = false;
            fx.ExitPool = null;
            fx.ExitOther = null;
            StopFlightSound(state);
            CloseWhirlpool(pool);

            string exitKind = null;
            Vector3 otherFloor;
            Whirlpool otherPool;
            Cell exit;
            if (TryPickOtherWhirlpool(state.Environment, pool, out otherFloor, out otherPool))
            {
                fx.HasExit = true;
                fx.ExitFloor = otherFloor;
                fx.ExitOther = otherPool;
                exitKind = "another whirlpool";
            }
            else if (TryPickWhirlpoolExit(state.Environment, out exit))
            {
                fx.HasExit = true;
                fx.ExitFloor = exit.FloorWorldPosition;
                fx.ExitPool = CreateExitWhirlpool(pool, fx.ExitFloor);
                exitKind = "a new whirlpool (no other one is left)";
            }

            Debug.Log("[Lifter Frenzy] A whirlpool took " + state.Root.name + (fx.HasExit
                ? "; it comes out of " + exitKind + " at " + fx.ExitFloor + "." : "; there is no place to come out."));
        }

        /// <summary>
        /// One of the other whirlpools of the flood, at random: not the one that took the prop, none that is
        /// closing (layer 19: used up, or the exit of a character) and none in an event spawner room.
        /// </summary>
        private bool TryPickOtherWhirlpool(
            EnvironmentController environment, Whirlpool taken, out Vector3 floor, out Whirlpool pool)
        {
            floor = Vector3.zero;
            pool = null;
            List<Whirlpool> options = new List<Whirlpool>();
            for (int i = 0; i < _whirlpools.Length; i++)
            {
                Whirlpool other = _whirlpools[i];
                if (other == null || other == taken || !other.isActiveAndEnabled
                    || other.gameObject.layer == WhirlpoolClosingLayer)
                {
                    continue;
                }

                Cell cell;
                if (TryGetCell(environment, other.transform.position, out cell) && IsEventSpawnerCell(cell))
                {
                    continue;
                }

                options.Add(other);
            }

            if (options.Count == 0)
            {
                return false;
            }

            pool = options[UnityEngine.Random.Range(0, options.Count)];
            floor = pool.transform.position;
            return true;
        }

        /// <summary>A whirlpool that has caught something: no more catching (layer 19) and it shrinks away.</summary>
        private static void CloseWhirlpool(Whirlpool pool)
        {
            if (pool == null)
            {
                return;
            }

            pool.gameObject.layer = WhirlpoolClosingLayer;
            System.Collections.IEnumerator routine = null;
            try
            {
                if (WhirlpoolCloseMethod != null)
                {
                    routine = WhirlpoolCloseMethod.Invoke(pool, null) as System.Collections.IEnumerator;
                }
            }
            catch (Exception)
            {
                routine = null;
            }

            if (routine != null)
            {
                // Started next to whatever the pool already runs (StartClose would stop all of it).
                pool.StartCoroutine(routine);
            }
            else
            {
                pool.StartClose();
            }
        }

        /// <summary>
        /// The new whirlpool of Whirlpool.Teleport: it forms for 3 s on layer 19 (it catches nothing), is listed in
        /// the flood so the flood removes it at its end, and is closed (StartClose) when the prop is out.
        /// </summary>
        private static Whirlpool CreateExitWhirlpool(Whirlpool source, Vector3 floor)
        {
            try
            {
                if (source == null || source.whirlpoolPre == null)
                {
                    return null;
                }

                Whirlpool exit = UnityEngine.Object.Instantiate<Whirlpool>(source.whirlpoolPre);
                exit.ec = source.ec;
                exit.FormTime = WhirlpoolExitFormSeconds;
                exit.transform.position = floor;
                exit.whirlpoolPre = source.whirlpoolPre;
                exit.flood = source.flood;
                exit.gameObject.layer = WhirlpoolClosingLayer;
                if (source.flood != null && source.flood.whirlpools != null)
                {
                    source.flood.whirlpools.Add(exit);
                }

                return exit;
            }
            catch (Exception exception)
            {
                LogEventError("exit whirlpool", exception);
                return null;
            }
        }

        /// <summary>
        /// The prop slides to the middle of the pool and sinks (0.4 s, the sink speed of Whirlpool.Teleport; it
        /// goes down by its own height, the floor hides it), jumps to the other whirlpool and rises out of it (0.4 s).
        /// It lands there like any thrown prop. Vanilla lowers the entity's height the same way.
        /// </summary>
        private bool FxStepSink(CarryState state, EventFx fx, float dt)
        {
            float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
            Vector3 half = state.FlightFullHalf;
            float fullDrop = 2f * half.y + WhirlpoolDropMargin;
            float step = dt * timeScale * WhirlpoolSinkSpeed;
            Vector3 c = state.FlightPosition + state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;

            if (!fx.SinkRising)
            {
                Vector3 centre = fx.SinkPool != null ? fx.SinkPool.transform.position : fx.SinkCentre;
                fx.SinkCentre = centre;
                c.x = Mathf.MoveTowards(c.x, centre.x, WhirlpoolGlideSpeed * dt);
                c.z = Mathf.MoveTowards(c.z, centre.z, WhirlpoolGlideSpeed * dt);
                fx.SinkPercent = Mathf.Max(WhirlpoolSinkFloor, fx.SinkPercent - step);
                c.y = state.FlightFloorY + half.y - fullDrop * SinkDepth(fx.SinkPercent);
                FxMoveSinking(state, c);
                if (fx.SinkPercent > WhirlpoolSinkFloor + 0.0001f)
                {
                    return true;
                }

                // At the bottom: the jump to the new whirlpool (Whirlpool.Teleport: subject.Teleport(newPos)).
                fx.SinkRising = true;
                if (fx.HasExit)
                {
                    state.FlightFloorY = fx.ExitFloor.y;
                    c = new Vector3(fx.ExitFloor.x, fx.ExitFloor.y + half.y - fullDrop, fx.ExitFloor.z);
                    FxMoveSinking(state, c);
                }

                return true;
            }

            fx.SinkPercent = Mathf.Min(1f, fx.SinkPercent + step);
            c.y = state.FlightFloorY + half.y - fullDrop * SinkDepth(fx.SinkPercent);
            FxMoveSinking(state, c);
            if (fx.SinkPercent < 1f - 0.0001f)
            {
                return true;
            }

            // Out: a whirlpool that was formed for this prop closes (newPool.StartClose()), and so does the other
            // whirlpool of the flood it came up out of (unless it is closing already: two Close() would fight over
            // its scale); the prop lands where it stands.
            Whirlpool exit = fx.ExitPool;
            Whirlpool other = fx.ExitOther;
            fx.ExitPool = null;
            fx.ExitOther = null;
            fx.Sinking = false;
            fx.SinkRising = false;
            fx.SinkPool = null;
            if (exit != null)
            {
                exit.StartClose();
            }

            if (other != null && other.gameObject.layer != WhirlpoolClosingLayer)
            {
                CloseWhirlpool(other);
            }

            state.Landed = true;
            Debug.Log("[Lifter Frenzy] " + state.Root.name + " came up out of a whirlpool at " + c + ".");
            return true;
        }

        /// <summary>1 at the top of the ride (sink percent 1), 0 at the bottom (the floor percent).</summary>
        private static float SinkDepth(float percent)
        {
            return Mathf.Clamp01((1f - percent) / (1f - WhirlpoolSinkFloor));
        }

        private static void FxMoveSinking(CarryState state, Vector3 centre)
        {
            state.FlightPosition = centre - state.FlightCenterOffset;
            state.Body.MovePosition(state.FlightPosition);
        }

        /// <summary>
        /// The cell a prop comes out at: a random one, as Whirlpool.Teleport picks it (the cells that are not off
        /// limits; ConvertEntityUnsafeCells moves it to a safe cell of its room), without the cells that have hard
        /// coverage when there are others, and never in an event spawner room.
        /// </summary>
        private static bool TryPickWhirlpoolExit(EnvironmentController environment, out Cell exit)
        {
            exit = null;
            if (environment == null)
            {
                return false;
            }

            // AllTilesNoGarbage hands out a shared buffer (the flood itself uses it): it is copied before it is changed.
            List<Cell> cells = new List<Cell>(environment.AllTilesNoGarbage(false, false));
            if (cells.Count == 0)
            {
                cells = new List<Cell>(environment.AllTilesNoGarbage(false, true));
            }

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (IsEventSpawnerCell(cells[i]))
                {
                    cells.RemoveAt(i);
                }
            }

            if (cells.Count == 0)
            {
                return false;
            }

            List<Cell> pick = new List<Cell>(1);
            pick.Add(cells[UnityEngine.Random.Range(0, cells.Count)]);
            pick.ConvertEntityUnsafeCells();
            exit = pick.Count > 0 && !IsEventSpawnerCell(pick[0]) ? pick[0] : null;
            return exit != null;
        }

        /// <summary>A cell of an event spawner room (where the Student Shuffle crowd comes from): no prop goes there.</summary>
        private static bool IsEventSpawnerCell(Cell cell)
        {
            return cell != null && !cell.Null && cell.room != null && cell.room.category == RoomCategory.EventSpawner;
        }

        /// <summary>True when the floor plan of the box (its centre and half size) lies over a cell of an event spawner room.</summary>
        private static bool BoxTouchesEventSpawner(EnvironmentController environment, Vector3 centre, Vector3 half)
        {
            if (environment == null)
            {
                return false;
            }

            // The corners and the points between them, never more than a cell apart.
            int stepsX = Mathf.Max(1, Mathf.CeilToInt(2f * half.x / WorldTileWidth));
            int stepsZ = Mathf.Max(1, Mathf.CeilToInt(2f * half.z / WorldTileWidth));
            for (int i = 0; i <= stepsX; i++)
            {
                for (int j = 0; j <= stepsZ; j++)
                {
                    Vector3 point = new Vector3(
                        centre.x - half.x + 2f * half.x * i / stepsX,
                        centre.y,
                        centre.z - half.z + 2f * half.z * j / stepsZ);
                    Cell cell;
                    if (TryGetCell(environment, point, out cell) && IsEventSpawnerCell(cell))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
