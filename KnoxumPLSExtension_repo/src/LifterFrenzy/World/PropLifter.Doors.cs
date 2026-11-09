using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Doors and windows: how a flying prop meets them and what a carried prop does to a door.
    /// </summary>
    public sealed partial class PropLifter
    {


        private void UpdateDoorForCarriedProp()
        {
            if (_held == null || _held.Player == null || Time.unscaledTime < _nextDoorCheckTime)
            {
                return;
            }

            _nextDoorCheckTime = Time.unscaledTime + DoorCheckInterval;
            Bounds propBounds;
            if (_held.Root == null || !CombinedBounds(_held.Renderers, out propBounds))
            {
                return;
            }

            Bounds contactBounds = propBounds;
            contactBounds.Expand(DoorContactPadding * 2f);
            Ray cameraRay;
            bool hasCameraRay = TryGetCameraRay(_held.Player, out cameraRay);
            Vector3 forward = hasCameraRay
                ? Vector3.ProjectOnPlane(cameraRay.direction, Vector3.up).normalized
                : Vector3.ProjectOnPlane(_held.Player.transform.forward, Vector3.up).normalized;
            EnsureLevelDoors(_held.Environment);
            Door[] doors = _levelDoors;
            Vector3 playerPosition = _held.Player.transform.position;
            float contactReach = propBounds.extents.magnitude + WorldTileWidth;
            float aheadReach = DoorLookAhead + DoorLateralTolerance + 1f;

            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                // Windows are broken, not opened; a super door is closed by its event or lever and a prop never opens it.
                if (door == null || door is Window || door is LockdownDoor
                    || door.ec != _held.Environment || door.locked)
                {
                    continue;
                }

                // Only the doors near the prop (touching it) or near the player (the one being walked through).
                Vector3 doorPosition = door.transform.position;
                if ((doorPosition - propBounds.center).sqrMagnitude > contactReach * contactReach
                    && (doorPosition - playerPosition).sqrMagnitude > aheadReach * aheadReach)
                {
                    continue;
                }

                bool touching = DoorBoundsTouchProp(door, contactBounds);
                bool fitsDoorway = PropFitsDoorway(_held.Root, propBounds, door);
                if (fitsDoorway && touching)
                {
                    // Re-evaluate the object's rotated cross-door width every check, so turning it
                    // can make a previously-too-wide prop fit and open this doorway.
                    door.OpenTimed(DoorOpenDuration, false);
                }
                else if (!fitsDoorway && door.open
                    && IsDoorSafelyAhead(door, _held.Player.transform.position, forward))
                {
                    // A wide prop must not be carried through an already-open doorway.
                    door.Shut();
                }
            }
        }

        private void BreakWindowsTouchingProp(CarryState state)
        {
            Bounds propBounds;
            if (state == null || state.Root == null || !CombinedBounds(state.Renderers, out propBounds))
            {
                return;
            }

            // A crane carries the prop through the windows round its output cell: they stay whole.
            if (state.Fx != null && state.Fx.Craned)
            {
                return;
            }

            EnsureLevelDoors(state.Environment);
            if (_levelWindows.Length == 0 || _levelDoorsEnvironment != state.Environment)
            {
                return;
            }

            // Tiny geometric tolerance avoids missed contacts from floating-point bounds; no cooldown is used.
            propBounds.Expand(0.1f);
            for (int i = 0; i < _levelWindows.Length; i++)
            {
                Window window = _levelWindows[i];
                if (window != null && propBounds.Intersects(_levelWindowBounds[i]))
                {
                    window.Break(false);
                }
            }
        }

        /// <summary>
        /// Every door, window and super door of the floor, looked up once per floor (and renewed when it is
        /// LevelDoorsInterval seconds old and something asks). FindObjectsOfType walks the whole scene, which
        /// on a floor with many objects is far too slow to repeat every frame or every physics step. The windows
        /// come with their world boxes (they never move); the boxes come from the meshes, so a window whose
        /// renderers are culled at the moment has one too.
        /// </summary>
        private void EnsureLevelDoors(EnvironmentController environment)
        {
            if (environment == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (environment == _levelDoorsEnvironment && now - _levelDoorsBuiltAt < LevelDoorsInterval)
            {
                return;
            }

            _levelDoorsEnvironment = environment;
            _levelDoorsBuiltAt = now;
            _levelDoorsVersion++;
            List<Door> doors = new List<Door>();
            List<Window> windows = new List<Window>();
            List<Bounds> windowBounds = new List<Bounds>();
            List<LockdownDoor> superDoors = new List<LockdownDoor>();
            Door[] all = FindObjectsOfType<Door>();
            for (int i = 0; i < all.Length; i++)
            {
                Door door = all[i];
                // A door of another environment is not of this floor; one that does not say (ec not set) is kept,
                // as it always was.
                if (door == null || (door.ec != null && door.ec != environment))
                {
                    continue;
                }

                doors.Add(door);
                Window window = door as Window;
                if (window != null)
                {
                    Bounds box;
                    if (TryGetWindowBounds(window, out box))
                    {
                        windows.Add(window);
                        windowBounds.Add(box);
                    }

                    continue;
                }

                LockdownDoor superDoor = door as LockdownDoor;
                if (superDoor != null)
                {
                    superDoors.Add(superDoor);
                }
            }

            _levelDoors = doors.ToArray();
            _levelWindows = windows.ToArray();
            _levelWindowBounds = windowBounds.ToArray();
            _levelSuperDoors = superDoors.ToArray();
        }

        private static bool TryGetWindowBounds(Window window, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            Renderer[] panes = window.windows;
            for (int i = 0; panes != null && i < panes.Length; i++)
            {
                Bounds pane;
                if (!TryGetMeshWorldBounds(panes[i], out pane))
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(pane);
                }
                else
                {
                    bounds = pane;
                    found = true;
                }
            }

            return found || CombinedBounds(panes, out bounds);
        }

        private static bool DoorBoundsTouchProp(Door door, Bounds propBounds)
        {
            Collider[] colliders = door.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger
                    && propBounds.Intersects(collider.bounds))
                {
                    return true;
                }
            }

            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && propBounds.Intersects(renderer.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PropFitsDoorway(Transform root, Bounds propBounds, Door door)
        {
            if (root == null || door == null)
            {
                return false;
            }

            Vector3 doorwayWidthAxis = Vector3.ProjectOnPlane(door.transform.right, Vector3.up);
            if (doorwayWidthAxis.sqrMagnitude < 0.001f)
            {
                return false;
            }

            doorwayWidthAxis.Normalize();
            // Project the rotated world bounds across this specific doorway. This is re-evaluated
            // after every turn, rather than reusing the side that happened to face the player at pickup.
            float width = 2f * (
                Mathf.Abs(Vector3.Dot(doorwayWidthAxis, Vector3.right)) * propBounds.extents.x
                + Mathf.Abs(Vector3.Dot(doorwayWidthAxis, Vector3.forward)) * propBounds.extents.z);
            return width <= WorldTileWidth - DoorWidthTolerance;
        }

        private static bool IsDoorSafelyAhead(Door door, Vector3 playerPosition, Vector3 forward)
        {
            if (door == null || forward.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Vector3 toDoor = door.transform.position - playerPosition;
            toDoor.y = 0f;
            float forwardDistance = Vector3.Dot(toDoor, forward);
            Vector3 lateralOffset = toDoor - forward * forwardDistance;
            return forwardDistance >= DoorSafeCloseDistance && forwardDistance <= DoorLookAhead
                && lateralOffset.magnitude <= DoorLateralTolerance;
        }

        private static bool FlightDoorLetsPass(CarryState state, FlightDoor entry, Vector3 center)
        {
            Door door = entry != null ? entry.Door : null;
            if (door == null)
            {
                return true;
            }

            Window window = door as Window;
            if (window != null)
            {
                // The same call BreakWindowsTouchingProp makes. A broken (or already open) window has its
                // colliders switched off, so the prop flies on; an unbreakable one stays a wall.
                if (!window.open)
                {
                    window.Break(false);
                }

                return window.open;
            }

            if (door is LockdownDoor)
            {
                // A super door is closed by its event or its lever and a prop never opens it: open it is
                // a doorway, shut a wall (the slab squishes a prop that is under it, see TickSuperDoors).
                return door.open;
            }

            if (door.open)
            {
                return true;
            }

            // Locked doors and props wider than a doorway behave like walls.
            Bounds propBounds = new Bounds(center, state.FlightFullHalf * 2f);
            if (door.ec == state.Environment && !door.locked
                && PropFitsDoorway(state.Root, propBounds, door))
            {
                door.OpenTimed(DoorOpenDuration, false);
                return door.open;
            }

            return false;
        }

        // --- the cell grid as a set of solid edges ---

        private void RefreshFlightDoors(EnvironmentController environment)
        {
            EnsureLevelDoors(environment);
            if (environment == _flightDoorsEnvironment && _flightDoorsVersion == _levelDoorsVersion)
            {
                return;
            }

            _flightDoorsEnvironment = environment;
            _flightDoorsVersion = _levelDoorsVersion;
            _flightDoors.Clear();
            if (environment == null)
            {
                return;
            }

            Door[] doors = _levelDoors;
            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                long key;
                if (door == null || door.ec != environment
                    || !TryFlightEdgeKey(door.position, door.direction, out key))
                {
                    continue;
                }

                FlightDoor entry = new FlightDoor();
                entry.Door = door;
                // Whether a collider is enabled is read live, so doors that open or lock between refreshes
                // are handled; only the list of doors and their colliders is cached.
                entry.Colliders = door.GetComponentsInChildren<Collider>(true);
                List<FlightDoor> list;
                if (!_flightDoors.TryGetValue(key, out list))
                {
                    list = new List<FlightDoor>(2);
                    _flightDoors[key] = list;
                }

                list.Add(entry);
            }
        }

        private static bool FlightDoorIsSolid(FlightDoor entry)
        {
            Door door = entry.Door;
            if (door == null || door.open)
            {
                return false;
            }

            Collider[] colliders = entry.Colliders;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger
                    && collider.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Like FlightOverlaps, but for the slab of every door and window edge, open or closed.</summary>
        private bool FlightDoorSlabOverlaps(Vector3 c, Vector3 half)
        {
            if (_flightDoors.Count == 0)
            {
                return false;
            }

            float margin = FlightSkin + FlightWallHalfThickness;
            int k0 = Mathf.CeilToInt((c.x - half.x - margin) / WorldTileWidth);
            int k1 = Mathf.FloorToInt((c.x + half.x + margin) / WorldTileWidth);
            int j0 = Mathf.FloorToInt((c.z - half.z - margin) / WorldTileWidth);
            int j1 = Mathf.FloorToInt((c.z + half.z + margin) / WorldTileWidth);
            for (int k = k0; k <= k1; k++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    float plane = k * WorldTileWidth;
                    if (plane - FlightWallHalfThickness - half.x < c.x && c.x < plane + FlightWallHalfThickness + half.x
                        && j * WorldTileWidth - half.z < c.z && c.z < (j + 1) * WorldTileWidth + half.z
                        && FlightDoorBlocksSettling(FlightEdgeKey(k - 1, j, 0)))
                    {
                        return true;
                    }
                }
            }

            int m0 = Mathf.CeilToInt((c.z - half.z - margin) / WorldTileWidth);
            int m1 = Mathf.FloorToInt((c.z + half.z + margin) / WorldTileWidth);
            int i0 = Mathf.FloorToInt((c.x - half.x - margin) / WorldTileWidth);
            int i1 = Mathf.FloorToInt((c.x + half.x + margin) / WorldTileWidth);
            for (int m = m0; m <= m1; m++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    float plane = m * WorldTileWidth;
                    if (plane - FlightWallHalfThickness - half.z < c.z && c.z < plane + FlightWallHalfThickness + half.z
                        && i * WorldTileWidth - half.x < c.x && c.x < (i + 1) * WorldTileWidth + half.x
                        && FlightDoorBlocksSettling(FlightEdgeKey(i, m - 1, 1)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// True when a door or window of this edge would be left with the prop in it. A super door (LockdownDoor)
        /// is not one: closing on a prop it squishes it and goes back up, or (a lever door) waits on it
        /// (TickSuperDoors in the Events files), so a prop may be put in its doorway.
        /// </summary>
        private bool FlightDoorBlocksSettling(long key)
        {
            List<FlightDoor> list;
            if (!_flightDoors.TryGetValue(key, out list))
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                FlightDoor entry = list[i];
                if (entry != null && !(entry.Door is LockdownDoor))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
