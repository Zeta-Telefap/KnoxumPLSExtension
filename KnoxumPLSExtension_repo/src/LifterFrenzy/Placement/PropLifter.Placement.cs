using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Putting a prop back into the world: the cell, the parent, the settle and the placement preview.
    /// </summary>
    public sealed partial class PropLifter
    {


        private void UpdatePlacementPreview(CarryState state)
        {
            if (state == null || state.Glow == null
                || Time.unscaledTime < state.NextPlacementPreviewTime)
            {
                return;
            }

            state.NextPlacementPreviewTime = Time.unscaledTime + PlacementPreviewRefreshInterval;
            state.Glow.SetColor(CanPlaceAtCurrentPosition(state)
                ? ValidPlacementColor
                : InvalidPlacementColor);
        }

        private bool CanPlaceAtCurrentPosition(CarryState state)
        {
            Cell target;
            Cell playerCell;
            if (state == null || state.Root == null || state.Player == null
                || !TryGetCell(state.Environment, state.Root.position, out target)
                || !TryGetCell(state.Environment, state.Player.transform.position, out playerCell))
            {
                return false;
            }

            RoomController oldRoom = state.PreferredRoom;
            Dictionary<Cell, bool> oldWalkability = state.PlacementWalkabilityCache;
            state.PreferredRoom = playerCell.room;
            state.PlacementWalkabilityCache = new Dictionary<Cell, bool>();
            try
            {
                Vector3 proposedPosition = state.Root.position;
                // Preview mirrors the game navigation rule, not the carried mesh size; physical prop
                // colliders are checked separately so the preview also turns red if it would hit a wall.
                if (target.room != playerCell.room)
                {
                    target = NearestCellInRoom(playerCell.room, state.Root.position);
                    if (target != null)
                    {
                        proposedPosition = target.FloorWorldPosition;
                        proposedPosition.y += state.InitialY - state.OriginFloorY;
                    }
                }

                return target != null && target.room == playerCell.room
                    && target.room.category != RoomCategory.EventSpawner
                    && DestinationParent(state.ParentKind, target) != null
                    && PlayerCanReachCell(state, target)
                    && PropPlacementCollisionFree(state, proposedPosition, state.CarryRotation);
            }
            finally
            {
                state.PreferredRoom = oldRoom;
                state.PlacementWalkabilityCache = oldWalkability;
            }
        }

        private void SettleThrown(CarryState state, Cell target)
        {
            InvalidateAim();
            if (state == null || state.Root == null || target == null)
            {
                ReturnToOrigin(state);
                return;
            }

            float initialCellY = state.InitialY - state.OriginFloorY;
            // Restore the prop's original height above its cell floor after the physics landing.
            // Some BB+ props (for example, carousel bases) are intentionally authored above ground.
            Vector3 landingPosition = state.Root.position;
            landingPosition.y = target.FloorWorldPosition.y + initialCellY;

            // A THROWN prop settles where it actually came to rest, even in another room: a broken window
            // or an open doorway lets a throw cross a wall, and walls themselves stop it (see
            // StepFlight). Moving it to the thrower's side of the wall is what made a prop thrown
            // through a window end up in the room it was thrown from. A DROPPED prop (LMB) keeps the
            // older rule and settles in the player's room.
            RoomController placementRoom = target.room;
            Cell playerCell;
            if (!state.Thrown && state.Player != null
                && TryGetCell(state.Environment, state.Player.transform.position, out playerCell))
            {
                placementRoom = playerCell.room;
            }

            state.PreferredRoom = placementRoom;
            bool placed = false;
            try
            {
                state.PlacementWalkabilityCache = new Dictionary<Cell, bool>();
                // The prop may rest only where its full-size box clears every wall and doorway (a flight can
                // end with a smaller box, in a doorway that is about to close, or with a turned footprint).
                state.CheckFlightBox = true;
                RefreshFlightDoors(state.Environment);
                // A dropped prop that physics carried over a wall line stays on the player's side of it.
                if (placementRoom != null && target.room != placementRoom)
                {
                    Cell nearestRoomCell = NearestCellInRoom(placementRoom, landingPosition);
                    if (nearestRoomCell != null)
                    {
                        target = nearestRoomCell;
                        landingPosition = nearestRoomCell.FloorWorldPosition;
                        landingPosition.y += initialCellY;
                    }
                }

                state.CellOffset = landingPosition - target.FloorWorldPosition;
                placed = TryPlace(state, target) || TryPlaceNearby(state, landingPosition);
            }
            finally
            {
                state.PlacementWalkabilityCache = null;
                state.PreferredRoom = null;
                state.CheckFlightBox = false;
            }

            if (placed)
            {
                NoteProxyMoved(state.Root);
                LogLanding(state);
                return;
            }

            Debug.LogWarning("[Lifter Frenzy] No free spot for the prop near " + landingPosition
                + " in " + DescribeRoom(placementRoom) + "; it went back to where it came from.");
            ReturnToOrigin(state);
        }

        private static string DescribeRoom(RoomController room)
        {
            return room == null ? "no room" : room.name + " (" + room.category + ")";
        }

        private static void LogLanding(CarryState state)
        {
            string landed = "an unknown place";
            Cell landedCell;
            if (state.Root != null && TryGetCell(state.Environment, state.Root.position, out landedCell))
            {
                landed = DescribeRoom(landedCell.room) + ", cell " + landedCell.position;
            }

            string standing = "an unknown place";
            Cell playerCell;
            if (state.Player != null
                && TryGetCell(state.Environment, state.Player.transform.position, out playerCell))
            {
                standing = DescribeRoom(playerCell.room);
            }

            Debug.Log("[Lifter Frenzy] Prop '" + (state.Root != null ? state.Root.name : "?") + "' "
                + (state.Thrown ? "thrown" : "dropped") + " came to rest in " + landed
                + "; the player is in " + standing + "; wall impacts on the way: " + state.WallHits + ".");
        }

        private bool TryPlaceNearby(CarryState state, Vector3 landingPosition)
        {
            if (state == null || state.Root == null || state.Environment == null
                || state.PreferredRoom == null)
            {
                return false;
            }

            int fineRings = Mathf.CeilToInt(PlacementSearchFineRadius / PlacementSearchFineStep);
            for (int ring = 1; ring <= fineRings; ring++)
            {
                if (TryPlaceSearchRing(state, landingPosition, ring, PlacementSearchFineStep))
                {
                    return true;
                }
            }

            int firstCoarseRing = Mathf.FloorToInt(PlacementSearchFineRadius / PlacementSearchCoarseStep) + 1;
            int lastCoarseRing = Mathf.CeilToInt(PlacementSearchRadius / PlacementSearchCoarseStep);
            for (int ring = firstCoarseRing; ring <= lastCoarseRing; ring++)
            {
                if (TryPlaceSearchRing(state, landingPosition, ring, PlacementSearchCoarseStep))
                {
                    return true;
                }
            }

            if (state.PreferredRoom.cells == null)
            {
                return false;
            }

            List<Cell> roomCells = new List<Cell>(state.PreferredRoom.cells);
            roomCells.Sort(new RoomCellDistanceComparer(landingPosition));
            for (int i = 0; i < roomCells.Count; i++)
            {
                Cell cell = roomCells[i];
                if (cell == null || cell.Null || cell.room != state.PreferredRoom)
                {
                    continue;
                }

                Vector3 tileCenter = cell.FloorWorldPosition;
                tileCenter.y = landingPosition.y;
                if (TryPlaceAtPosition(state, tileCenter))
                {
                    return true;
                }
            }

            return false;
        }

        private static Cell NearestCellInRoom(RoomController room, Vector3 position)
        {
            if (room == null || room.cells == null || room.cells.Count == 0)
            {
                return null;
            }

            Cell nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell cell = room.cells[i];
                if (cell == null || cell.Null || cell.room != room)
                {
                    continue;
                }

                Vector3 delta = cell.FloorWorldPosition - position;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = cell;
                }
            }

            return nearest;
        }

        private bool TryPlaceSearchRing(CarryState state, Vector3 center, int ring, float step)
        {
            // Check the four nearest directions first, then diagonals around the same-radius perimeter.
            Vector3[] cardinalOffsets =
            {
                new Vector3(ring * step, 0f, 0f),
                new Vector3(-ring * step, 0f, 0f),
                new Vector3(0f, 0f, ring * step),
                new Vector3(0f, 0f, -ring * step)
            };
            for (int i = 0; i < cardinalOffsets.Length; i++)
            {
                if (TryPlaceAtPosition(state, center + cardinalOffsets[i]))
                {
                    return true;
                }
            }

            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring
                        || dx == 0 || dz == 0)
                    {
                        continue;
                    }

                    Vector3 candidate = center + new Vector3(dx * step, 0f, dz * step);
                    if (TryPlaceAtPosition(state, candidate))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryPlaceAtPosition(CarryState state, Vector3 position)
        {
            Cell target;
            if (!TryGetCell(state.Environment, position, out target))
            {
                return false;
            }

            Vector3 oldOffset = state.CellOffset;
            state.CellOffset = position - target.FloorWorldPosition;
            if (TryPlace(state, target))
            {
                return true;
            }

            state.CellOffset = oldOffset;
            return false;
        }

        private bool TryPlace(CarryState state, Cell target)
        {
            if (state == null || state.Root == null || target == null || target.Null
                || target.room == null || target.room.category == RoomCategory.EventSpawner
                || (state.PreferredRoom != null && target.room != state.PreferredRoom)
                || DestinationParent(state.ParentKind, target) == null)
            {
                return false;
            }

            // A thrown prop may land somewhere the player cannot walk to (that is the point of throwing
            // it through a window), so only dropped props must be reachable.
            if (!state.Thrown && !PlayerCanReachCell(state, target))
            {
                return false;
            }

            // Player-sized capsule/path tests decide reachability; this separate physics test
            // prevents the actual prop colliders from being committed through a wall or solid prop.
            Vector3 proposedPosition = target.FloorWorldPosition + state.CellOffset;
            if (state.CheckFlightBox && !FlightBoxClear(state, proposedPosition))
            {
                return false;
            }

            if (!PropPlacementCollisionFree(state, proposedPosition, state.CarryRotation))
            {
                return false;
            }

            bool sameCell = target == state.OriginCell;

            CellCoverage oldHard = state.UsesCoverage
                ? ReadCoverage(HardCoverageField, state.OriginCell)
                : CellCoverage.None;
            CellCoverage oldSoft = state.UsesCoverage
                ? ReadCoverage(SoftCoverageField, state.OriginCell)
                : CellCoverage.None;
            CellCoverage targetHard = state.UsesCoverage && !sameCell
                ? ReadCoverage(HardCoverageField, target)
                : CellCoverage.None;
            CellCoverage targetSoft = state.UsesCoverage && !sameCell
                ? ReadCoverage(SoftCoverageField, target)
                : CellCoverage.None;
            Transform oldParent = state.Root.parent;
            Vector3 oldPosition = state.Root.position;
            Quaternion oldRotation = state.Root.rotation;

            try
            {
                if (state.UsesCoverage)
                {
                    if (sameCell)
                    {
                        WriteCoverage(target,
                            oldHard | state.Coverage,
                            oldSoft | state.Coverage);
                    }
                    else
                    {
                        target.HardCover(state.Coverage);
                    }
                }

                MoveRendererReferences(state.OriginCell, target, state.RegisteredRenderers);
                state.Root.SetParent(DestinationParent(state.ParentKind, target), true);
                Vector3 placedPosition = target.FloorWorldPosition + state.CellOffset;
                state.Root.position = placedPosition;
                state.Root.rotation = state.TargetRotation;
                RestoreBody(state, false);
                return true;
            }
            catch (Exception exception)
            {
                if (state.UsesCoverage)
                {
                    WriteCoverage(state.OriginCell, oldHard, oldSoft);
                    if (!sameCell)
                    {
                        WriteCoverage(target, targetHard, targetSoft);
                    }
                }

                RemoveRendererReferences(target, state.RegisteredRenderers);
                AddRendererReferences(state.OriginCell, state.RegisteredRenderers);
                if (state.Root != null)
                {
                    state.Root.SetParent(oldParent, true);
                    state.Root.position = oldPosition;
                    state.Root.rotation = oldRotation;
                }

                Debug.LogError("[Lifter Frenzy] Placement transaction rolled back: " + exception);
                return false;
            }
        }

        private static bool PlayerCanReachCell(CarryState state, Cell target)
        {
            if (state == null || state.Player == null || state.Environment == null
                || target == null || target.Null || target.room == null)
            {
                return false;
            }

            bool cached;
            if (state.PlacementWalkabilityCache != null
                && state.PlacementWalkabilityCache.TryGetValue(target, out cached))
            {
                return cached;
            }

            Cell playerCell;
            bool reachable = TryGetCell(state.Environment, state.Player.transform.position, out playerCell)
                && (playerCell == target || state.Environment.CheckPath(playerCell, target, PathType.Nav));
            if (reachable)
            {
                reachable = PlayerCapsuleFitsAtCell(state, playerCell, target);
            }

            if (state.PlacementWalkabilityCache != null)
            {
                state.PlacementWalkabilityCache[target] = reachable;
            }

            return reachable;
        }

        private static bool PlayerCapsuleFitsAtCell(CarryState state, Cell playerCell, Cell target)
        {
            if (state == null || state.Player == null || playerCell == null || target == null)
            {
                return false;
            }

            PlayerMovement movement = state.Player.plm;
            CharacterController controller = movement != null ? movement.cc : null;
            if (controller == null || !controller.enabled)
            {
                // Keep NavBin reachability as the fallback on controllers without a character capsule.
                return true;
            }

            Vector3 cellOffset = target.FloorWorldPosition - playerCell.FloorWorldPosition;
            Vector3 center = controller.bounds.center + cellOffset;
            Vector3 scale = controller.transform.lossyScale;
            float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float radius = Mathf.Max(0.01f, controller.radius * horizontalScale - 0.025f);
            float height = Mathf.Max(radius * 2f, controller.height * Mathf.Abs(scale.y) - 0.025f);
            Vector3 up = controller.transform.up;
            // Lift the test capsule slightly so resting contact with the floor isn't mistaken for a wall.
            center += up * 0.05f;
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 pointA = center + up * halfSegment;
            Vector3 pointB = center - up * halfSegment;
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                WalkabilityHits,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            // A full buffer can truncate results, so fail closed rather than accept an unchecked spot.
            if (hitCount >= WalkabilityHits.Length)
            {
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = WalkabilityHits[i];
                WalkabilityHits[i] = null;
                if (hit == null || !hit.enabled || hit.isTrigger
                    || hit.transform == state.Player.transform
                    || hit.transform.IsChildOf(state.Player.transform)
                    || (state.Root != null && (hit.transform == state.Root
                        || hit.transform.IsChildOf(state.Root)))
                    || (state.Body != null && hit.attachedRigidbody == state.Body)
                    || Physics.GetIgnoreLayerCollision(controller.gameObject.layer, hit.gameObject.layer)
                    || Physics.GetIgnoreCollision(controller, hit))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool PropPlacementCollisionFree(
            CarryState state,
            Vector3 proposedPosition,
            Quaternion proposedRotation)
        {
            if (state == null || state.Root == null || state.PlacementColliders == null
                || state.PlacementColliders.Length == 0)
            {
                return true;
            }

            Vector3 originalPosition = state.Root.position;
            Quaternion originalRotation = state.Root.rotation;
            Vector3 originalBodyPosition = state.Body != null ? state.Body.position : originalPosition;
            Quaternion originalBodyRotation = state.Body != null ? state.Body.rotation : originalRotation;
            try
            {
                state.Root.SetPositionAndRotation(proposedPosition, proposedRotation);
                if (state.Body != null)
                {
                    state.Body.position = proposedPosition;
                    state.Body.rotation = proposedRotation;
                }

                Physics.SyncTransforms();
                for (int i = 0; i < state.PlacementColliders.Length; i++)
                {
                    Collider propCollider = state.PlacementColliders[i];
                    if (propCollider == null || !propCollider.enabled || !propCollider.gameObject.activeInHierarchy
                        || propCollider.isTrigger)
                    {
                        continue;
                    }

                    Bounds bounds = propCollider.bounds;
                    Vector3 queryExtents = bounds.extents + Vector3.one * 0.01f;
                    int hitCount = Physics.OverlapBoxNonAlloc(
                        bounds.center,
                        queryExtents,
                        WalkabilityHits,
                        Quaternion.identity,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore);
                    if (hitCount >= WalkabilityHits.Length)
                    {
                        return false;
                    }

                    for (int j = 0; j < hitCount; j++)
                    {
                        Collider hit = WalkabilityHits[j];
                        WalkabilityHits[j] = null;
                        if (hit == null || hit == propCollider || !hit.enabled || hit.isTrigger
                            || hit.transform == state.Root || hit.transform.IsChildOf(state.Root)
                            || (state.Player != null && (hit.transform == state.Player.transform
                                || hit.transform.IsChildOf(state.Player.transform)))
                            || Physics.GetIgnoreLayerCollision(propCollider.gameObject.layer, hit.gameObject.layer)
                            || Physics.GetIgnoreCollision(propCollider, hit))
                        {
                            continue;
                        }

                        Vector3 separationDirection;
                        float separationDistance;
                        if (Physics.ComputePenetration(
                            propCollider,
                            propCollider.transform.position,
                            propCollider.transform.rotation,
                            hit,
                            hit.transform.position,
                            hit.transform.rotation,
                            out separationDirection,
                            out separationDistance)
                            && separationDistance > 0.001f)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                if (state.Root != null)
                {
                    state.Root.SetPositionAndRotation(originalPosition, originalRotation);
                }

                if (state.Body != null)
                {
                    state.Body.position = originalBodyPosition;
                    state.Body.rotation = originalBodyRotation;
                }

                Physics.SyncTransforms();
            }
        }

        private void ReturnToOrigin(CarryState state)
        {
            InvalidateAim();
            StopFlightSound(state);
            FxAbortSink(state);
            if (state == null || state.Root == null)
            {
                return;
            }

            try
            {
                if (state.OriginCell != null && !state.OriginCell.Null
                    && state.OriginCell.room != null && state.UsesCoverage)
                {
                    CellCoverage hard = ReadCoverage(HardCoverageField, state.OriginCell);
                    CellCoverage soft = ReadCoverage(SoftCoverageField, state.OriginCell);
                    WriteCoverage(state.OriginCell,
                        hard | state.RemovedHardCoverage,
                        soft | state.RemovedSoftCoverage);
                }

                Transform parent = state.OriginParent != null
                    ? state.OriginParent
                    : DestinationParent(state.ParentKind, state.OriginCell);
                if (parent != null)
                {
                    state.Root.SetParent(parent, true);
                }

                state.Root.position = state.OriginPosition;
                state.Root.rotation = state.OriginalRotation;
                AddRendererReferences(state.OriginCell, state.RegisteredRenderers);
                RestoreBody(state, true);
                NoteProxyMoved(state.Root);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Lifter Frenzy] Failed to restore prop: " + exception);
            }
        }

        private static Transform DestinationParent(ParentKind kind, Cell cell)
        {
            if (cell == null || cell.room == null)
            {
                return null;
            }

            if (kind == ParentKind.CellObjectBase)
            {
                return cell.ObjectBase;
            }

            if (kind == ParentKind.RoomObjects)
            {
                return cell.room.objectObject != null
                    ? cell.room.objectObject.transform
                    : cell.room.transform;
            }

            return cell.room.transform;
        }

        private enum ParentKind
        {
            Room,
            RoomObjects,
            CellObjectBase
        }

        private sealed class RoomCellDistanceComparer : IComparer<Cell>
        {
            private readonly Vector3 _origin;

            public RoomCellDistanceComparer(Vector3 origin)
            {
                _origin = origin;
            }

            public int Compare(Cell left, Cell right)
            {
                if (left == right)
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                Vector3 leftOffset = left.FloorWorldPosition - _origin;
                Vector3 rightOffset = right.FloorWorldPosition - _origin;
                leftOffset.y = 0f;
                rightOffset.y = 0f;
                return leftOffset.sqrMagnitude.CompareTo(rightOffset.sqrMagnitude);
            }
        }
    }
}
