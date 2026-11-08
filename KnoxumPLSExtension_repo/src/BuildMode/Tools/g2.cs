using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace RebuildTheSchool
{
    /// <summary>
    /// Grid-snapped structural dragging for doors, windows, and wall edges.
    /// G begins a drag, camera-facing cardinal directions choose the edge, and LMB commits.
    /// The wheel moves the anchored preview forward/back to a wall; Shift + wheel moves it
    /// laterally, and R + wheel rotates in quarter-turns. Walking cannot pull the structure along.
    /// Right-click is intentionally consumed while a structural target is held.
    /// </summary>
    public sealed class g2 : MonoBehaviour
    {
        private const float StructuralPickupRange = 16f;
        private const float StructuralHighlightGraceTime = 0.45f;
        private const float StructuralHighlightRefreshInterval = 0.2f;
        private const float WallPanelThickness = 0.12f;

        private static readonly FieldInfo WindowBrokenField = typeof(Window).GetField(
            "broken", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo HardCoverageField = typeof(Cell).GetField(
            "hardCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SoftCoverageField = typeof(Cell).GetField(
            "softCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CellTileField = typeof(Cell).GetField(
            "tile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WindowMapPositionsField = typeof(EnvironmentController).GetField(
            "windowMapPositions", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WindowMapDirectionsField = typeof(EnvironmentController).GetField(
            "windowMapDirections", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WindowMapTilesField = typeof(EnvironmentController).GetField(
            "windowMapTiles", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WindowAMapTileField = typeof(Window).GetField(
            "aMapTile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WindowBMapTileField = typeof(Window).GetField(
            "bMapTile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo StandardDoorBgField = typeof(StandardDoor).GetField(
            "bg", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<Type, FieldInfo[]> DoorMapTileFields =
            new Dictionary<Type, FieldInfo[]>();
        // Walls that a moved door took away when it landed on a wall edge, keyed by the edge (EdgeKey).
        // When that door leaves the edge, exactly the faces recorded here are walled up again. An edge
        // that had no wall when the door arrived (a vanilla doorway, or any open edge) has no entry and
        // stays open. Entries only change after a move succeeded, and belong to one level.
        private static readonly Dictionary<long, RemovedWall> RemovedWalls = new Dictionary<long, RemovedWall>();
        private static Elevator[] ElevatorCache = new Elevator[0];
        private static float NextElevatorRefreshTime;

        private StructuralDragState _held;
        private PlayerManager _player;
        private Transform _hoveredDoorRoot;
        private Door _hoveredDoor;
        private IntVector2 _hoveredWallPosition;
        private Direction _hoveredWallDirection;
        private bool _hasHoveredWall;
        private g.GlowState _doorHighlight;
        private GameObject _hoverWallHighlight;
        private float _nextStructuralHighlightTime;
        private float _lastStructuralHighlightTime;
        private int _lastPickupFrame = -1;
        private bool _lastPickupWasStructural;

        public static g2 Instance { get; private set; }
        public bool IsDragging { get { return _held != null; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            RefreshPlayer();

            if (_held != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDrag();
                    return;
                }

                UpdateDragWheel(_held);
                Ray ray;
                if (TryGetCameraRay(_held.Player, out ray))
                {
                    UpdateTarget(_held, ray);
                }

                return;
            }

            if (_player != null && _player.ec != null && Time.timeScale > 0f
                && (g.Instance == null || !g.Instance.IsHoldingObject))
            {
                if (g.Instance != null && g.Instance.HasPropTarget(_player))
                {
                    // A prop under the crosshair always wins over walls/doors behind it: its glow
                    // replaces the structural glow and G is left to g. Without this both glows showed
                    // at once and the winner depended on which Update ran first.
                    ClearStructuralHighlight();
                }
                else
                {
                    UpdateStructuralHighlight();
                    if (PickupPressed())
                    {
                        TryBeginDragThisFrame(_player);
                    }
                }
            }
            else
            {
                ClearStructuralHighlight();
            }
        }

        private void OnDestroy()
        {
            ClearStructuralHighlight();
            CancelDrag();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Called by g after its prop query found nothing under the crosshair.</summary>
        internal bool HandlePickupRequest(PlayerManager player)
        {
            if (_held != null)
            {
                return true;
            }

            return TryBeginDragThisFrame(player);
        }

        /// <summary>Called first by the existing PlayerClick hook so g and g2 can share G/LMB.</summary>
        internal bool HandlePlayerClick(PlayerClick clicker)
        {
            if (_held != null)
            {
                if (clicker != null)
                {
                    clicker.clickedThisFrame = null;
                }

                if (Time.timeScale > 0f && PlacePressed())
                {
                    if (_held.TargetValid)
                    {
                        TryCommitDrag();
                    }
                    else
                    {
                        Debug.LogWarning("[Rebuild the School] Structural placement blocked at the current tile edge.");
                    }
                }

                // In g2, RMB never throws a door/window/wall; all clicks are consumed while held.
                return true;
            }

            if (clicker == null || Time.timeScale <= 0f || !PickupPressed()
                || (g.Instance != null && g.Instance.IsHoldingObject))
            {
                return false;
            }

            // A glowing prop is the user's explicit target. Let the prop runtime consume G
            // before structural picking can claim the same input.
            if (g.Instance != null && g.Instance.TryPickupHighlighted(clicker.pm))
            {
                clicker.clickedThisFrame = null;
                return true;
            }

            if (TryBeginDragThisFrame(clicker.pm))
            {
                clicker.clickedThisFrame = null;
                return true;
            }

            return false;
        }

        private void UpdateStructuralHighlight()
        {
            if (Time.unscaledTime < _nextStructuralHighlightTime)
            {
                return;
            }

            _nextStructuralHighlightTime = Time.unscaledTime + StructuralHighlightRefreshInterval;
            Ray ray;
            if (!TryGetCameraRay(_player, out ray))
            {
                if (!KeepStructuralHighlightBriefly())
                {
                    ClearStructuralHighlight();
                }

                return;
            }

            RaycastHit hit;
            Door visibleDoor;
            if (!TryNearestStructuralHit(ray, _player, _player.ec, out hit, out visibleDoor))
            {
                if (!KeepStructuralHighlightBriefly())
                {
                    ClearStructuralHighlight();
                }

                return;
            }

            Door door = visibleDoor != null
                ? visibleDoor
                : (hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null);
            if (door != null)
            {
                if (IsElevatorDoor(door) || !CanGrabDoor(door, _player.ec))
                {
                    ClearStructuralHighlight();
                    return;
                }

                if (_hoveredDoorRoot != door.transform)
                {
                    ClearStructuralHighlight();
                    _hoveredDoorRoot = door.transform;
                    _doorHighlight = new g.GlowState(door.transform,
                        door.GetComponentsInChildren<Renderer>(true));
                }

                _hoveredDoor = door;
                _lastStructuralHighlightTime = Time.unscaledTime;
                if (_doorHighlight != null)
                {
                    _doorHighlight.SetColor(ValidColor);
                }

                return;
            }

            Cell wallCell;
            Direction wallDirection;
            if (!TryResolveWall(hit, _player.ec, out wallCell, out wallDirection)
                || !CanGrabWall(wallCell, wallDirection, _player.ec))
            {
                ClearStructuralHighlight();
                return;
            }

            if (_doorHighlight != null || _hoveredDoorRoot != null)
            {
                ClearStructuralHighlight();
            }

            if (_hoverWallHighlight == null)
            {
                _hoverWallHighlight = CreateWallGhost();
            }

            _hoveredWallPosition = wallCell.position;
            _hoveredWallDirection = wallDirection;
            _hasHoveredWall = true;
            _lastStructuralHighlightTime = Time.unscaledTime;
            PositionWallGhost(_hoverWallHighlight, wallCell, wallDirection);
            SetGhostColor(_hoverWallHighlight, ValidColor);
        }

        private bool KeepStructuralHighlightBriefly()
        {
            bool hasTarget = _hoveredDoor != null || _hasHoveredWall;
            if (!hasTarget || Time.unscaledTime - _lastStructuralHighlightTime > StructuralHighlightGraceTime)
            {
                return false;
            }

            if (_doorHighlight != null)
            {
                _doorHighlight.SetColor(ValidColor);
            }
            if (_hoverWallHighlight != null && _hasHoveredWall)
            {
                SetGhostColor(_hoverWallHighlight, ValidColor);
            }

            return true;
        }

        private void ClearStructuralHighlight()
        {
            if (_doorHighlight != null)
            {
                _doorHighlight.Restore();
                _doorHighlight = null;
            }

            _hoveredDoorRoot = null;
            _hoveredDoor = null;
            _hoveredWallPosition = default(IntVector2);
            _hoveredWallDirection = Direction.Null;
            _hasHoveredWall = false;
            if (_hoverWallHighlight != null)
            {
                Destroy(_hoverWallHighlight);
                _hoverWallHighlight = null;
            }
        }

        private bool TryBeginDragThisFrame(PlayerManager player)
        {
            if (_lastPickupFrame == Time.frameCount)
            {
                return _lastPickupWasStructural;
            }

            _lastPickupFrame = Time.frameCount;
            _lastPickupWasStructural = false;
            if (player == null || player.ec == null)
            {
                return false;
            }

            // Reuse the exact structural target that is currently glowing, rather than
            // rerunning selection and potentially choosing a different edge on G.
            if (_hoveredDoor != null && CanGrabDoor(_hoveredDoor, player.ec))
            {
                _lastPickupWasStructural = true;
                Door highlightedDoor = _hoveredDoor;
                ClearStructuralHighlight();
                BeginDoorDrag(highlightedDoor, player);
                return true;
            }

            if (_hasHoveredWall && player.ec.ContainsCoordinates(_hoveredWallPosition))
            {
                Cell highlightedWall = player.ec.CellFromPosition(_hoveredWallPosition);
                if (CanGrabWall(highlightedWall, _hoveredWallDirection, player.ec))
                {
                    _lastPickupWasStructural = true;
                    Direction highlightedDirection = _hoveredWallDirection;
                    ClearStructuralHighlight();
                    BeginWallDrag(highlightedWall, highlightedDirection, player);
                    return true;
                }
            }

            Ray ray;
            if (!TryGetCameraRay(player, out ray))
            {
                return false;
            }

            RaycastHit hit;
            Door visibleDoor;
            if (!TryNearestStructuralHit(ray, player, player.ec, out hit, out visibleDoor))
            {
                Debug.Log("[Rebuild the School] G: no structural target intersected the camera ray.");
                return false;
            }

            Door door = visibleDoor != null
                ? visibleDoor
                : (hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null);
            if (door != null)
            {
                _lastPickupWasStructural = true;
                if (IsElevatorDoor(door))
                {
                    ClearStructuralHighlight();
                    Debug.Log("[Rebuild the School] G: elevator doors are not movable.");
                }
                else if (door.ec == player.ec)
                {
                    ClearStructuralHighlight();
                    BeginDoorDrag(door, player);
                    if (_held != null)
                    {
                        Debug.Log("[Rebuild the School] G selected " + door.GetType().Name + ". Scroll moves it; R + scroll rotates it.");
                    }
                    else
                    {
                        Debug.LogWarning("[Rebuild the School] Door was hit, but its source edge is locked or invalid.");
                    }
                }

                return true;
            }

            Cell wallCell;
            Direction wallDirection;
            if (TryResolveWall(hit, player.ec, out wallCell, out wallDirection))
            {
                _lastPickupWasStructural = true;
                if (CanGrabWall(wallCell, wallDirection, player.ec))
                {
                    ClearStructuralHighlight();
                    BeginWallDrag(wallCell, wallDirection, player);
                    if (_held != null)
                    {
                        Debug.Log("[Rebuild the School] G selected a wall edge. Scroll moves it; R + scroll rotates it.");
                    }
                    else
                    {
                        Debug.LogWarning("[Rebuild the School] Wall edge was found, but its neighbouring cell is invalid.");
                    }
                }
                else
                {
                    Debug.LogWarning("[Rebuild the School] Wall edge is covered, occupied, locked, or adjacent to an elevator.");
                }

                return true;
            }

            return false;
        }

        private static bool CanGrabDoor(Door door, EnvironmentController environment)
        {
            if (door == null || environment == null || door.ec != environment || IsElevatorDoor(door))
            {
                return false;
            }

            Cell source = environment.CellFromPosition(door.position);
            Cell neighbour = GetNeighbour(environment, source, door.direction);
            return UsableCell(source) && UsableCell(neighbour)
                && !source.locked && !neighbour.locked;
        }

        private void BeginDoorDrag(Door door, PlayerManager player)
        {
            if (door == null || door.ec == null || IsElevatorDoor(door))
            {
                return;
            }

            Cell source = door.ec.CellFromPosition(door.position);
            Direction sourceDirection = door.direction;
            Cell neighbour = GetNeighbour(door.ec, source, sourceDirection);
            if (!UsableCell(source) || !UsableCell(neighbour) || source.locked || neighbour.locked)
            {
                return;
            }

            StructuralDragState state = new StructuralDragState();
            state.Kind = StructuralKind.Door;
            state.Environment = door.ec;
            state.Player = player;
            state.Door = door;
            state.SourceCell = source;
            state.SourceDirection = sourceDirection;
            state.SourceNeighbour = neighbour;
            state.OriginalParent = door.transform.parent;
            state.OriginalPosition = door.transform.position;
            state.OriginalRotation = door.transform.rotation;
            state.WasOpen = door.open;
            state.WasLocked = door.locked;
            Window window = door as Window;
            state.WasBrokenWindow = window != null && ReadWindowBroken(window);
            CaptureAppearance(state, door);
            InitializeAnchor(state, source, player);

            // Keep the actual door instance/type (Standard, Swing, automatic, Window, etc.);
            // disable its collision only while it is being moved as a grid preview.
            for (int i = 0; i < state.Colliders.Length; i++)
            {
                if (state.Colliders[i] != null)
                {
                    state.Colliders[i].enabled = false;
                }
            }

            door.transform.SetParent(door.ec.transform, true);
            _held = state;
            Ray ray;
            if (TryGetCameraRay(player, out ray))
            {
                UpdateTarget(state, ray);
            }
        }

        private void BeginWallDrag(Cell cell, Direction direction, PlayerManager player)
        {
            if (cell == null || cell.room == null || cell.room.ec == null)
            {
                return;
            }

            Cell neighbour = GetNeighbour(cell.room.ec, cell, direction);
            if (!UsableCell(neighbour))
            {
                return;
            }

            StructuralDragState state = new StructuralDragState();
            state.Kind = StructuralKind.Wall;
            state.Environment = cell.room.ec;
            state.Player = player;
            state.SourceCell = cell;
            state.SourceDirection = direction;
            state.SourceNeighbour = neighbour;
            InitializeAnchor(state, cell, player);
            state.Ghost = CreateWallGhost();
            _held = state;

            Ray ray;
            if (TryGetCameraRay(player, out ray))
            {
                UpdateTarget(state, ray);
            }
        }

        private static void CaptureAppearance(StructuralDragState state, Door door)
        {
            state.Renderers = door.GetComponentsInChildren<Renderer>(true);
            state.Colliders = door.GetComponentsInChildren<Collider>(true);
            state.RendererEnabled = new bool[state.Renderers.Length];
            state.ColliderEnabled = new bool[state.Colliders.Length];
            for (int i = 0; i < state.Renderers.Length; i++)
            {
                state.RendererEnabled[i] = state.Renderers[i] != null && state.Renderers[i].enabled;
            }

            for (int i = 0; i < state.Colliders.Length; i++)
            {
                state.ColliderEnabled[i] = state.Colliders[i] != null && state.Colliders[i].enabled;
            }
        }

        private void UpdateTarget(StructuralDragState state, Ray cameraRay)
        {
            Direction cameraDirection = CameraDirection(state, cameraRay);
            Direction sideDirection = RotateDirection(cameraDirection, 1);
            IntVector2 forwardOffset = cameraDirection.ToIntVector2();
            IntVector2 sideOffset = sideDirection.ToIntVector2();
            IntVector2 targetPosition = new IntVector2(
                state.AnchorPosition.x
                    + forwardOffset.x * state.ForwardSteps + sideOffset.x * state.SideSteps,
                state.AnchorPosition.z
                    + forwardOffset.z * state.ForwardSteps + sideOffset.z * state.SideSteps);
            if (!state.Environment.ContainsCoordinates(targetPosition))
            {
                state.TargetValid = false;
                SetGhostColor(state, InvalidColor);
                return;
            }

            Cell targetCell = state.Environment.CellFromPosition(targetPosition);
            Direction targetDirection = RotateDirection(cameraDirection, state.ManualRotationSteps);
            if (!UsableCell(targetCell) || targetDirection == Direction.Null)
            {
                state.TargetValid = false;
                SetGhostColor(state, InvalidColor);
                return;
            }

            state.TargetCell = targetCell;
            state.TargetDirection = targetDirection;
            state.TargetValid = IsValidTarget(state, targetCell, targetDirection);

            if (state.Kind == StructuralKind.Door && state.Door != null)
            {
                state.Door.transform.position = targetCell.FloorWorldPosition;
                state.Door.transform.rotation = targetDirection.ToRotation();
            }
            else if (state.Ghost != null)
            {
                PositionWallGhost(state.Ghost, targetCell, targetDirection);
            }

            SetGhostColor(state, state.TargetValid ? ValidColor : InvalidColor);
        }

        private static void InitializeAnchor(StructuralDragState state, Cell source, PlayerManager player)
        {
            Cell anchor = player != null && state.Environment != null
                ? CellAt(state.Environment, player.transform.position) : null;
            if (anchor == null)
            {
                anchor = source;
            }

            state.AnchorPosition = anchor.position;
            Direction facing = state.SourceDirection;
            Ray ray;
            if (TryGetCameraRay(player, out ray))
            {
                facing = CameraDirection(state, ray);
            }

            Vector3 offset = source.FloorWorldPosition - anchor.FloorWorldPosition;
            Direction side = RotateDirection(facing, 1);
            state.ForwardSteps = Mathf.RoundToInt(Vector3.Dot(offset, facing.ToVector3()) / 10f);
            state.SideSteps = Mathf.RoundToInt(Vector3.Dot(offset, side.ToVector3()) / 10f);

            // If the picked edge faces back toward the anchor, its owning cell is one step
            // closer than the cell stored on the door/wall component.
            if (state.SourceDirection == facing.GetOpposite())
            {
                state.ForwardSteps--;
            }
            else if (state.SourceDirection == side.GetOpposite())
            {
                state.SideSteps--;
            }
        }

        private void UpdateDragWheel(StructuralDragState state)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (state == null || Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }

            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(wheel)));
            int wheelDirection = wheel < 0f ? 1 : -1;
            if (Input.GetKey(KeyCode.R))
            {
                state.ManualRotationSteps = (state.ManualRotationSteps + wheelDirection * steps) % 4;
                if (state.ManualRotationSteps < 0)
                {
                    state.ManualRotationSteps += 4;
                }

                return;
            }

            Ray ray;
            Direction facing = state.SourceDirection;
            if (TryGetCameraRay(state.Player, out ray))
            {
                facing = CameraDirection(state, ray);
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Direction right = RotateDirection(facing, 1);
                // Like rotation: wheel down moves right, wheel up moves left.
                TryAdvanceOffset(state, right, wheelDirection, steps, true, facing);
            }
            else
            {
                // Wheel up moves forward, wheel down backward. There is no numeric distance
                // cap; each grid step is accepted until a wall or map boundary blocks it.
                int forwardDirection = wheel > 0f ? 1 : -1;
                TryAdvanceOffset(state, facing, forwardDirection, steps, false, facing);
            }
        }

        private void TryAdvanceOffset(StructuralDragState state, Direction axis, int sign,
            int steps, bool lateral, Direction facing)
        {
            Direction travelDirection = sign > 0 ? axis : axis.GetOpposite();
            for (int i = 0; i < steps; i++)
            {
                Cell current = CellAtOffset(state, facing);
                if (!UsableCell(current))
                {
                    break;
                }

                Cell next = GetNeighbour(state.Environment, current, travelDirection);
                if (!UsableCell(next) || current.locked || next.locked)
                {
                    break;
                }

                bool wallBlocks = current.HasWallInDirection(travelDirection)
                    || next.HasWallInDirection(travelDirection.GetOpposite());
                if (wallBlocks && !IsOriginalSourceEdge(state, current, travelDirection))
                {
                    break;
                }

                if (lateral)
                {
                    state.SideSteps += sign;
                }
                else
                {
                    state.ForwardSteps += sign;
                }
            }
        }

        private static Cell CellAtOffset(StructuralDragState state, Direction facing)
        {
            Direction side = RotateDirection(facing, 1);
            IntVector2 forward = facing.ToIntVector2();
            IntVector2 lateral = side.ToIntVector2();
            IntVector2 position = new IntVector2(
                state.AnchorPosition.x + forward.x * state.ForwardSteps + lateral.x * state.SideSteps,
                state.AnchorPosition.z + forward.z * state.ForwardSteps + lateral.z * state.SideSteps);
            return state.Environment.ContainsCoordinates(position)
                ? state.Environment.CellFromPosition(position)
                : null;
        }

        private static bool IsOriginalSourceEdge(StructuralDragState state, Cell cell, Direction direction)
        {
            if (state == null || cell == null || state.SourceCell == null)
            {
                return false;
            }

            return (cell.position == state.SourceCell.position && direction == state.SourceDirection)
                || (cell.position + direction.ToIntVector2() == state.SourceCell.position
                    && direction.GetOpposite() == state.SourceDirection);
        }

        private static Direction CameraDirection(StructuralDragState state, Ray cameraRay)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraRay.direction, Vector3.up);
            Direction direction = flatForward.sqrMagnitude > 0.001f
                ? Directions.DirFromVector3(flatForward.normalized, 45f)
                : state.SourceDirection;
            return direction == Direction.Null ? state.SourceDirection : direction;
        }

        private static Direction RotateDirection(Direction direction, int steps)
        {
            if (direction == Direction.Null)
            {
                return Direction.Null;
            }

            int rotated = ((int)direction + steps) % 4;
            if (rotated < 0)
            {
                rotated += 4;
            }

            return (Direction)rotated;
        }

        private bool IsValidTarget(StructuralDragState state, Cell targetCell, Direction targetDirection)
        {
            if (!UsableCell(targetCell) || targetCell.locked)
            {
                return false;
            }

            Cell neighbour = GetNeighbour(state.Environment, targetCell, targetDirection);
            if (!UsableCell(neighbour) || neighbour.locked || IsElevatorAdjacent(state.Environment, targetCell, targetDirection))
            {
                return false;
            }

            if (HasOtherDoorOnEdge(targetCell, targetDirection, state.Door)
                || HasOtherDoorOnEdge(neighbour, targetDirection.GetOpposite(), state.Door))
            {
                return false;
            }

            if (state.Kind == StructuralKind.Wall)
            {
                if ((targetCell == state.SourceCell && targetDirection == state.SourceDirection)
                    || (targetCell == state.SourceNeighbour
                        && targetDirection == state.SourceDirection.GetOpposite()))
                {
                    return false;
                }

                return !targetCell.HasWallInDirection(targetDirection)
                    && !neighbour.HasWallInDirection(targetDirection.GetOpposite())
                    && !targetCell.WallAnyCovered(targetDirection)
                    && !neighbour.WallAnyCovered(targetDirection.GetOpposite())
                    && !IsCellOccupied(state.Environment, targetCell, state.Player)
                    && !IsCellOccupied(state.Environment, neighbour, state.Player);
            }

            if (state.Door is Window)
            {
                bool isSourceEdge = (targetCell == state.SourceCell
                        && targetDirection == state.SourceDirection)
                    || (targetCell == state.SourceNeighbour
                        && targetDirection == state.SourceDirection.GetOpposite());
                return isSourceEdge || targetCell.HasWallInDirection(targetDirection)
                    || neighbour.HasWallInDirection(targetDirection.GetOpposite());
            }

            return true;
        }

        private void TryCommitDrag()
        {
            if (_held == null || !_held.TargetValid || _held.TargetCell == null)
            {
                return;
            }

            StructuralDragState state = _held;
            bool success = false;
            try
            {
                success = state.Kind == StructuralKind.Door
                    ? CommitDoorMove(state)
                    : CommitWallMove(state);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Rebuild the School] Structural drag failed; restoring source: " + exception);
                success = false;
            }

            if (success)
            {
                FinishDrag(state, false);
                Debug.Log("[Rebuild the School] Structural target moved to "
                    + state.TargetCell.position + " / " + state.TargetDirection + ".");
            }
            else
            {
                Debug.LogWarning("[Rebuild the School] Structural placement rejected; source was restored.");
                CancelDrag();
            }
        }

        private bool CommitDoorMove(StructuralDragState state)
        {
            Door door = state.Door;
            Cell target = state.TargetCell;
            Direction targetDirection = state.TargetDirection;
            Cell targetNeighbour = GetNeighbour(state.Environment, target, targetDirection);
            if (door == null || !UsableCell(target) || !UsableCell(targetNeighbour)
                || IsElevatorDoor(door) || IsElevatorAdjacent(state.Environment, target, targetDirection))
            {
                return false;
            }

            if (state.SourceCell == target && state.SourceDirection == targetDirection)
            {
                // Put back on its own edge: nothing structural changes. During the drag the door hung
                // under the environment root, so hand it back to the tile it was picked up from.
                door.transform.SetParent(state.OriginalParent, true);
                door.transform.position = state.OriginalPosition;
                door.transform.rotation = state.OriginalRotation;
                RestoreDoorAppearance(state);
                RestoreDoorState(state);
                return true;
            }

            if (HasOtherDoorOnEdge(target, targetDirection, door)
                || HasOtherDoorOnEdge(targetNeighbour, targetDirection.GetOpposite(), door))
            {
                return false;
            }

            IntVector2 sourcePosition = state.SourceCell.position;
            Direction sourceDirection = state.SourceDirection;
            IntVector2 sourceNeighbourPosition = state.SourceNeighbour.position;
            IntVector2 targetPosition = target.position;
            IntVector2 targetNeighbourPosition = targetNeighbour.position;
            bool targetHadWall = target.HasWallInDirection(targetDirection);
            bool targetNeighbourHadWall = targetNeighbour.HasWallInDirection(targetDirection.GetOpposite());
            Window movingWindow = door as Window;
            PurgeRemovedWalls(state.Environment);
            long sourceKey = EdgeKey(sourcePosition, sourceDirection);
            long targetKey = EdgeKey(targetPosition, targetDirection);
            // The same physical edge approached from its other side: the door only turns around.
            bool sameEdge = sourceKey == targetKey;
            RemovedWall remembered = null;
            if (!sameEdge)
            {
                RemovedWalls.TryGetValue(sourceKey, out remembered);
            }

            // The edge the door leaves:
            //  - a wall this door removed when it landed there is rebuilt, exactly the faces that stood;
            //  - a window with no record (a vanilla window) always stood in a wall, so it is walled up;
            //  - any other doorway (a vanilla door, or an edge that was open when the door arrived) stays open.
            bool restoreRemembered = remembered != null;
            bool sealOldEdge = !sameEdge && !restoreRemembered && movingWindow != null;
            string oldEdgeNote = sameEdge
                ? "same edge, the door only turned around"
                : (restoreRemembered
                    ? "wall rebuilt on " + (remembered.CellHadWall && remembered.NeighbourHadWall
                        ? "both faces" : "one face")
                    : (sealOldEdge ? "walled up (window)" : "left open (it was a doorway)"));
            bool windowMapUpdated = false;
            bool oneWayReleased = false;
            bool initializedAtTarget = false;
            int replacedMarkers = 0;
            List<CellSnapshot> snapshots = new List<CellSnapshot>();
            AddSnapshot(snapshots, state.SourceCell);
            AddSnapshot(snapshots, state.SourceNeighbour);
            AddSnapshot(snapshots, target);
            AddSnapshot(snapshots, targetNeighbour);

            try
            {
                // 1. Let go of everything the door holds on the SOURCE edge while its aTile/bTile still
                //    point there. Door.Block caches its last value: releasing after SetupDoor has moved
                //    the door would decrement the NEW cells and leave a permanent block at the old doorway.
                door.Block(false);
                SetOneWayDoorBlock(door, state.SourceNeighbour, sourceDirection, false);
                oneWayReleased = true;
                replacedMarkers = ClearDoorMapMarkers(door);
                UnregisterDoorFromCell(state.SourceCell, door);
                UnregisterDoorFromCell(state.SourceNeighbour, door);
                ClearDoorCoverage(state.SourceCell, sourceDirection, door);
                ClearDoorCoverage(state.SourceNeighbour, sourceDirection.GetOpposite(), door);

                // 2. The edge the door leaves (see oldEdgeNote above). UpdateCell must not run here: vanilla
                //    AssignDoor stores the direction pointing away from the doorway on the far cell, so
                //    UpdateCell re-walls the doorway and opens the far cell's other wall instead.
                if (sameEdge)
                {
                    // Nothing structural changes on the edge the door keeps.
                }
                else if (restoreRemembered)
                {
                    RebuildRemovedWall(state.Environment, remembered);
                }
                else if (sealOldEdge)
                {
                    state.Environment.CloseCell(sourcePosition, sourceDirection);
                    state.Environment.CloseCell(sourceNeighbourPosition, sourceDirection.GetOpposite());
                }
                else
                {
                    ConnectDoorEdge(state.Environment, sourcePosition, sourceDirection);
                }

                // 3. The edge the door lands on: take the wall away on BOTH faces, then register the door.
                if (targetHadWall || targetNeighbourHadWall)
                {
                    ConnectDoorEdge(state.Environment, targetPosition, targetDirection);
                }

                target = state.Environment.CellFromPosition(targetPosition);
                targetNeighbour = state.Environment.CellFromPosition(targetNeighbourPosition);
                state.Environment.SetupDoor(door, target, targetDirection);
                InitializeMovedDoor(door);
                initializedAtTarget = true;
                if (movingWindow != null)
                {
                    // Like BuildWindow, cover the wall slot on both faces (coverage only: the faces
                    // themselves stay open for pathing).
                    target.HardCoverWall(targetDirection, true);
                    targetNeighbour.HardCoverWall(targetDirection.GetOpposite(), true);
                }

                // Refresh every subtype's own renderer when it provides an UpdateTextures method.
                UpdateDoorTextures(door);
                EnsureDoorEdgeOpen(state.Environment, targetPosition, targetDirection);
                RefreshMapTiles(state.Environment, sourcePosition, sourceNeighbourPosition,
                    targetPosition, targetNeighbourPosition);
                SynchronizeDoorBlocking(state);
                RestoreDoorAppearance(state);
                RestoreDoorState(state);
                if (movingWindow != null)
                {
                    UpdateWindowMap(state.Environment, sourcePosition, sourceDirection,
                        targetPosition, targetDirection, movingWindow);
                    windowMapUpdated = true;
                }

                // Nothing below can fail any more, so the wall memory may change now (a rollback above
                // restores the wall bits from the snapshots and never touches the memory).
                if (!sameEdge)
                {
                    if (restoreRemembered)
                    {
                        RemovedWalls.Remove(sourceKey);
                    }

                    if (targetHadWall || targetNeighbourHadWall)
                    {
                        RemovedWalls[targetKey] = new RemovedWall(
                            state.Environment, targetPosition, targetDirection,
                            targetHadWall, targetNeighbourHadWall);
                    }
                    else
                    {
                        RemovedWalls.Remove(targetKey);
                    }
                }

                Debug.Log("[Rebuild the School] Door move (" + door.GetType().Name + "): "
                    + sourcePosition + "/" + sourceDirection + " -> " + targetPosition + "/" + targetDirection
                    + "; old edge: " + oldEdgeNote
                    + "; wall removed at the new edge: " + (targetHadWall || targetNeighbourHadWall)
                    + "; minimap markers replaced: " + replacedMarkers + ".");
                return true;
            }
            catch
            {
                try
                {
                    Cell failedTarget = state.Environment.CellFromPosition(targetPosition);
                    Cell failedTargetNeighbour = state.Environment.CellFromPosition(targetNeighbourPosition);
                    Cell restoredSource = state.Environment.CellFromPosition(sourcePosition);
                    Cell restoredSourceNeighbour = state.Environment.CellFromPosition(sourceNeighbourPosition);

                    // Release whatever the door holds right now (it may already point at the target).
                    door.Block(false);
                    if (initializedAtTarget)
                    {
                        SetOneWayDoorBlock(door, failedTargetNeighbour, targetDirection, false);
                    }
                    else if (!oneWayReleased)
                    {
                        SetOneWayDoorBlock(door, state.SourceNeighbour, sourceDirection, false);
                    }

                    ClearDoorMapMarkers(door);
                    UnregisterDoorFromCell(failedTarget, door);
                    UnregisterDoorFromCell(failedTargetNeighbour, door);
                    UnregisterDoorFromCell(restoredSource, door);
                    UnregisterDoorFromCell(restoredSourceNeighbour, door);
                    RestoreCellSnapshots(state.Environment, snapshots);

                    // Back on the source edge with exactly one pair of markers; Initialize re-applies
                    // the one-way block itself.
                    state.Environment.SetupDoor(door, restoredSource, sourceDirection);
                    InitializeMovedDoor(door);
                    UpdateDoorTextures(door);
                    RefreshMapTiles(state.Environment, sourcePosition, sourceNeighbourPosition,
                        targetPosition, targetNeighbourPosition);
                    SynchronizeDoorBlocking(state);
                    RestoreDoorAppearance(state);
                    RestoreDoorState(state);
                    if (movingWindow != null && windowMapUpdated)
                    {
                        UpdateWindowMap(state.Environment, targetPosition, targetDirection,
                            sourcePosition, sourceDirection, movingWindow);
                    }
                }
                catch (Exception rollbackException)
                {
                    Debug.LogError("[Rebuild the School] Door rollback failed: " + rollbackException);
                }

                throw;
            }
        }

        private bool CommitWallMove(StructuralDragState state)
        {
            Cell target = state.TargetCell;
            Cell targetNeighbour = GetNeighbour(state.Environment, target, state.TargetDirection);
            if (!IsValidTarget(state, target, state.TargetDirection)
                || target == null || targetNeighbour == null)
            {
                return false;
            }

            List<CellSnapshot> snapshots = new List<CellSnapshot>();
            AddSnapshot(snapshots, state.SourceCell);
            AddSnapshot(snapshots, state.SourceNeighbour);
            AddSnapshot(snapshots, target);
            AddSnapshot(snapshots, targetNeighbour);
            try
            {
                state.Environment.ConnectCells(state.SourceCell.position, state.SourceDirection);
                state.Environment.CloseCell(target.position, state.TargetDirection);
                state.Environment.CloseCell(targetNeighbour.position, state.TargetDirection.GetOpposite());
                // UpdateCellSafely, not UpdateCell: a wall that is moved next to a door must not
                // re-wall that doorway (see UpdateCellSafely).
                UpdateCellSafely(state.Environment, state.SourceCell.position);
                UpdateCellSafely(state.Environment, state.SourceNeighbour.position);
                UpdateCellSafely(state.Environment, target.position);
                UpdateCellSafely(state.Environment, targetNeighbour.position);

                Cell openedSource = state.Environment.CellFromPosition(state.SourceCell.position);
                Cell openedSourceNeighbour = state.Environment.CellFromPosition(state.SourceNeighbour.position);
                Cell closedTarget = state.Environment.CellFromPosition(target.position);
                Cell closedTargetNeighbour = state.Environment.CellFromPosition(targetNeighbour.position);
                if (openedSource.HasWallInDirection(state.SourceDirection)
                    || openedSourceNeighbour.HasWallInDirection(state.SourceDirection.GetOpposite())
                    || !closedTarget.HasWallInDirection(state.TargetDirection)
                    || !closedTargetNeighbour.HasWallInDirection(state.TargetDirection.GetOpposite()))
                {
                    RestoreCellSnapshots(state.Environment, snapshots);
                    return false;
                }

                // Never leave an isolated edge that disconnects its two cells after mutation.
                if (!state.Environment.CheckPath(closedTarget, closedTargetNeighbour, PathType.Nav))
                {
                    RestoreCellSnapshots(state.Environment, snapshots);
                    return false;
                }

                // The minimap only draws a tile's walls when the tile is first found, so redraw the four
                // tiles whose walls just changed.
                RefreshMapTiles(state.Environment, state.SourceCell.position, state.SourceNeighbour.position,
                    target.position, targetNeighbour.position);
                return true;
            }
            catch
            {
                RestoreCellSnapshots(state.Environment, snapshots);
                throw;
            }
        }

        private void CancelDrag()
        {
            if (_held == null)
            {
                return;
            }

            FinishDrag(_held, true);
        }

        private void FinishDrag(StructuralDragState state, bool restoreSourceTransform)
        {
            if (state == null)
            {
                return;
            }

            if (state.Kind == StructuralKind.Door && state.Door != null)
            {
                if (restoreSourceTransform)
                {
                    state.Door.transform.SetParent(state.OriginalParent, true);
                    state.Door.transform.position = state.OriginalPosition;
                    state.Door.transform.rotation = state.OriginalRotation;
                }

                RestoreDoorAppearance(state);
            }

            if (state.Ghost != null)
            {
                Destroy(state.Ghost);
            }

            if (_held == state)
            {
                _held = null;
                g.BlockGameMouseBriefly();
            }
        }

        private static void RestoreDoorAppearance(StructuralDragState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < state.Renderers.Length; i++)
            {
                if (state.Renderers[i] != null)
                {
                    state.Renderers[i].enabled = state.RendererEnabled[i];
                }
            }

            for (int i = 0; i < state.Colliders.Length; i++)
            {
                if (state.Colliders[i] != null)
                {
                    state.Colliders[i].enabled = state.ColliderEnabled[i];
                }
            }
        }

        private static void SynchronizeDoorBlocking(StructuralDragState state)
        {
            Door door = state != null ? state.Door : null;
            if (door == null)
            {
                return;
            }

            // CommitDoorMove released the old cells BEFORE the door moved, so Door's cached flag is
            // already false here and the new cells are blocked from scratch. (A Block(true)+Block(false)
            // pair after the move decremented the NEW cells while the old ones kept their counters, which
            // left a locked door's old doorway blocked for navigation for good.)
            door.Block(false);
            bool shouldBlock = (state.WasLocked && door.lockBlocks)
                || (!state.WasOpen && door.closeBlocks);
            if (shouldBlock)
            {
                door.Block(true);
            }
        }

        private static void RestoreDoorState(StructuralDragState state)
        {
            Door door = state.Door;
            if (door == null)
            {
                return;
            }

            if (state.WasLocked)
            {
                door.Lock(true);
            }
            else if (state.WasOpen)
            {
                door.OpenTimed(10f, false);
            }
            else
            {
                door.Shut();
            }

            Window window = door as Window;
            if (window != null && state.WasBrokenWindow)
            {
                if (WindowBrokenField != null)
                {
                    WindowBrokenField.SetValue(window, true);
                }

                window.Open(true, false);
            }

            // Preserve the exact collider enabled-state captured at pickup, including open windows.
            for (int i = 0; i < state.Colliders.Length; i++)
            {
                if (state.Colliders[i] != null)
                {
                    state.Colliders[i].enabled = state.ColliderEnabled[i];
                }
            }
        }

        private static void UpdateWindowMap(EnvironmentController environment, IntVector2 oldPosition,
            Direction oldDirection, IntVector2 newPosition, Direction newDirection, Window window)
        {
            if (environment == null || window == null || WindowMapPositionsField == null
                || WindowMapDirectionsField == null || WindowMapTilesField == null
                || WindowAMapTileField == null || WindowBMapTileField == null)
            {
                return;
            }

            try
            {
                List<IntVector2> positions = WindowMapPositionsField.GetValue(environment) as List<IntVector2>;
                List<Direction> directions = WindowMapDirectionsField.GetValue(environment) as List<Direction>;
                MapTile[] templates = WindowMapTilesField.GetValue(environment) as MapTile[];
                if (positions == null || directions == null || templates == null
                    || (int)oldDirection < 0 || (int)oldDirection >= templates.Length
                    || (int)newDirection < 0 || (int)newDirection >= templates.Length)
                {
                    return;
                }

                Map map = environment.map;
                MapTile oldAWindowMarker = WindowAMapTileField != null
                    ? WindowAMapTileField.GetValue(window) as MapTile : null;
                MapTile oldBWindowMarker = WindowBMapTileField != null
                    ? WindowBMapTileField.GetValue(window) as MapTile : null;
                Sprite oldWindowASprite = oldAWindowMarker != null
                    && oldAWindowMarker.SpriteRenderer != null
                    ? oldAWindowMarker.SpriteRenderer.sprite : null;
                Sprite oldWindowBSprite = oldBWindowMarker != null
                    && oldBWindowMarker.SpriteRenderer != null
                    ? oldBWindowMarker.SpriteRenderer.sprite : null;
                Sprite oldSourceSprite = templates[(int)oldDirection] != null
                    && templates[(int)oldDirection].SpriteRenderer != null
                    ? templates[(int)oldDirection].SpriteRenderer.sprite : null;
                Direction oldOpposite = oldDirection.GetOpposite();
                Sprite oldNeighbourSprite = templates[(int)oldOpposite] != null
                    && templates[(int)oldOpposite].SpriteRenderer != null
                    ? templates[(int)oldOpposite].SpriteRenderer.sprite : null;
                RemoveWindowMapExtra(oldAWindowMarker);
                RemoveWindowMapExtra(oldBWindowMarker);
                WindowAMapTileField.SetValue(window, null);
                WindowBMapTileField.SetValue(window, null);
                RemoveWindowMapMarker(map, oldPosition, oldSourceSprite);
                RemoveWindowMapMarker(map, oldPosition + oldDirection.ToIntVector2(), oldNeighbourSprite);

                for (int i = positions.Count - 1; i >= 0; i--)
                {
                    if (positions[i] == oldPosition && directions[i] == oldDirection)
                    {
                        positions.RemoveAt(i);
                        directions.RemoveAt(i);
                    }
                }

                positions.Add(newPosition);
                directions.Add(newDirection);

                if (map == null || map.tiles == null)
                {
                    return;
                }

                IntVector2 newNeighbour = newPosition + newDirection.ToIntVector2();
                if (!MapContains(map, newPosition) || !MapContains(map, newNeighbour))
                {
                    return;
                }

                UnityEngine.Object.Instantiate(templates[(int)newDirection], map.tiles[newPosition.x, newPosition.z].transform)
                    .gameObject.SetActive(true);
                UnityEngine.Object.Instantiate(templates[(int)newDirection.GetOpposite()],
                    map.tiles[newNeighbour.x, newNeighbour.z].transform).gameObject.SetActive(true);

                // Window.Initialize creates two per-window map tiles. Rebuild those too so
                // Break/Open state and the window's later callbacks point at the new cells.
                MapTile newAWindowMarker = map.AddExtraTile(newPosition);
                MapTile newBWindowMarker = map.AddExtraTile(newNeighbour);
                if (newAWindowMarker != null && newAWindowMarker.SpriteRenderer != null)
                {
                    MapTile newSourceTemplate = templates[(int)newDirection];
                    Sprite newSourceSprite = newSourceTemplate != null
                        && newSourceTemplate.SpriteRenderer != null
                        ? newSourceTemplate.SpriteRenderer.sprite : null;
                    newAWindowMarker.SpriteRenderer.sprite = oldWindowASprite != null
                        ? oldWindowASprite : newSourceSprite;
                    Cell newSourceCell = environment.CellFromPosition(newPosition);
                    if (newSourceCell != null && newSourceCell.room != null)
                    {
                        newAWindowMarker.SpriteRenderer.color = newSourceCell.room.color;
                    }
                    newAWindowMarker.transform.rotation = newDirection.ToUiRotation();
                    WindowAMapTileField.SetValue(window, newAWindowMarker);
                }

                if (newBWindowMarker != null && newBWindowMarker.SpriteRenderer != null)
                {
                    MapTile newNeighbourTemplate = templates[(int)newDirection.GetOpposite()];
                    Sprite newNeighbourSprite = newNeighbourTemplate != null
                        && newNeighbourTemplate.SpriteRenderer != null
                        ? newNeighbourTemplate.SpriteRenderer.sprite : null;
                    newBWindowMarker.SpriteRenderer.sprite = oldWindowBSprite != null
                        ? oldWindowBSprite : newNeighbourSprite;
                    Cell newNeighbourCell = environment.CellFromPosition(newNeighbour);
                    if (newNeighbourCell != null && newNeighbourCell.room != null)
                    {
                        newBWindowMarker.SpriteRenderer.color = newNeighbourCell.room.color;
                    }
                    newBWindowMarker.transform.rotation = newDirection.GetOpposite().ToUiRotation();
                    WindowBMapTileField.SetValue(window, newBWindowMarker);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Rebuild the School] Window map marker refresh failed: " + exception.Message);
            }
        }

        private static void RemoveWindowMapExtra(MapTile marker)
        {
            if (marker != null && marker.gameObject != null)
            {
                UnityEngine.Object.Destroy(marker.gameObject);
            }
        }

        private static void RemoveWindowMapMarker(Map map, IntVector2 position, Sprite sprite)
        {
            if (sprite == null || !MapContains(map, position))
            {
                return;
            }

            Transform parent = map.tiles[position.x, position.z].transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                MapTile marker = child.GetComponent<MapTile>();
                if (marker != null && marker.SpriteRenderer != null
                    && marker.SpriteRenderer.sprite == sprite)
                {
                    Destroy(marker.gameObject);
                    return;
                }
            }
        }

        private static bool MapContains(Map map, IntVector2 position)
        {
            return map != null && map.tiles != null && position.x >= 0 && position.z >= 0
                && position.x < map.tiles.GetLength(0) && position.z < map.tiles.GetLength(1)
                && map.tiles[position.x, position.z] != null;
        }

        private static void InitializeMovedDoor(Door door)
        {
            if (door == null)
            {
                return;
            }

            StandardDoor standard = door as StandardDoor;
            if (standard != null)
            {
                // StandardDoor.Initialize() only subscribes the "temporarily open/close locked doors"
                // events, so running it again would subscribe the door twice. Its textures and minimap
                // markers come from the private Start(), which already ran for this door and must not run
                // again (it reads startingLayer from the door, which may be open right now). Redo the part
                // that depends on the two rooms by hand.
                RefreshStandardDoorTextures(standard);
                EnsureDoorMapMarkers(door);
                return;
            }

            MethodInfo initialize = door.GetType().GetMethod("Initialize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (initialize != null && initialize.DeclaringType != typeof(Door))
            {
                // SwingDoor, FacultyOnlyDoor, LockdownDoor... create a NEW pair of minimap markers on
                // every call. CommitDoorMove removed the old pair first, so exactly one pair is left.
                initialize.Invoke(door, null);
            }

            // A door type that has marker fields but does not fill them in Initialize gets a pair here.
            EnsureDoorMapMarkers(door);
        }

        private static void SetOneWayDoorBlock(Door door, Cell neighbour,
            Direction doorDirection, bool blocked)
        {
            if (door != null && door.GetType().Name.IndexOf("OneWay",
                    StringComparison.OrdinalIgnoreCase) >= 0 && neighbour != null)
            {
                neighbour.Block(doorDirection.GetOpposite(), blocked);
            }
        }

        private static void UpdateDoorTextures(Door door)
        {
            if (door == null)
            {
                return;
            }

            // Door subclasses can provide their own no-argument visual refresh. Resolve it
            // dynamically instead of limiting moved doors to StandardDoor/SwingDoor/Window.
            MethodInfo update = door.GetType().GetMethod("UpdateTextures",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (update != null)
            {
                update.Invoke(door, null);
            }
        }

        private static void ConnectDoorEdge(EnvironmentController environment,
            IntVector2 position, Direction direction)
        {
            if (environment == null || direction == Direction.Null)
            {
                return;
            }

            environment.ConnectCells(position, direction);
            environment.ConnectCells(position + direction.ToIntVector2(), direction.GetOpposite());
        }

        /// <summary>One key per physical edge, whichever of its two cells it is addressed from.</summary>
        private static long EdgeKey(IntVector2 position, Direction direction)
        {
            if (direction == Direction.Null)
            {
                return long.MinValue;
            }

            int x = position.x;
            int z = position.z;
            if (direction == Direction.West)
            {
                x -= 1;
                direction = Direction.East;
            }
            else if (direction == Direction.South)
            {
                z -= 1;
                direction = Direction.North;
            }

            int axis = direction == Direction.North ? 1 : 0;
            return (((long)(x + 1000)) * 100000L + (long)(z + 1000)) * 2L + axis;
        }

        /// <summary>Forget the walls of other levels (an environment that was destroyed compares equal to null).</summary>
        private static void PurgeRemovedWalls(EnvironmentController environment)
        {
            if (RemovedWalls.Count == 0)
            {
                return;
            }

            List<long> stale = null;
            foreach (KeyValuePair<long, RemovedWall> pair in RemovedWalls)
            {
                if (pair.Value == null || pair.Value.Environment != environment)
                {
                    if (stale == null)
                    {
                        stale = new List<long>();
                    }

                    stale.Add(pair.Key);
                }
            }

            for (int i = 0; stale != null && i < stale.Count; i++)
            {
                RemovedWalls.Remove(stale[i]);
            }
        }

        /// <summary>Walls the edge back up on exactly the faces that stood before the door arrived.</summary>
        private static void RebuildRemovedWall(EnvironmentController environment, RemovedWall wall)
        {
            if (environment == null || wall == null)
            {
                return;
            }

            // The cells may have changed since the wall was taken away (the wall tool can remove a cell),
            // so a face is only rebuilt on a cell that still exists. CloseCell recreates the cell, hence the
            // fresh CellFromPosition before each face.
            IntVector2 neighbourPosition = wall.Position + wall.Direction.ToIntVector2();
            if (wall.CellHadWall && environment.ContainsCoordinates(wall.Position)
                && UsableCell(environment.CellFromPosition(wall.Position)))
            {
                environment.CloseCell(wall.Position, wall.Direction);
            }

            if (wall.NeighbourHadWall && environment.ContainsCoordinates(neighbourPosition)
                && UsableCell(environment.CellFromPosition(neighbourPosition)))
            {
                environment.CloseCell(neighbourPosition, wall.Direction.GetOpposite());
            }
        }

        private static void RefreshStandardDoorTextures(StandardDoor door)
        {
            if (door == null)
            {
                return;
            }

            try
            {
                Cell aCell = door.aTile;
                Cell bCell = door.bTile;
                if (aCell == null || bCell == null || aCell.room == null || bCell.room == null)
                {
                    return;
                }

                // Mirrors the first half of StandardDoor.Start(): the wall texture behind each door
                // panel and the door-hole materials belong to the room on that side.
                Texture2D[] background = StandardDoorBgField != null
                    ? StandardDoorBgField.GetValue(door) as Texture2D[] : null;
                if (background != null && background.Length >= 2)
                {
                    background[0] = aCell.room.wallTex;
                    background[1] = bCell.room.wallTex;
                }

                StandardDoorMats aMats = aCell.room.doorMats;
                StandardDoorMats bMats = bCell.room.doorMats;
                if (aMats != null && bMats != null && door.overlayShut != null && door.overlayOpen != null
                    && door.overlayShut.Length >= 2 && door.overlayOpen.Length >= 2)
                {
                    door.overlayShut[1] = aMats.shut;
                    door.overlayShut[0] = bMats.shut;
                    door.overlayOpen[1] = aMats.open;
                    door.overlayOpen[0] = bMats.open;
                }

                door.UpdateTextures();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Rebuild the School] Door texture refresh failed: " + exception.Message);
            }
        }

        private static FieldInfo[] GetDoorMapTileFields(Type doorType)
        {
            FieldInfo[] cached;
            if (DoorMapTileFields.TryGetValue(doorType, out cached))
            {
                return cached;
            }

            // Each Door family keeps its own pair of MapTile fields (SwingDoor: protected, StandardDoor:
            // private, LockdownDoor: its own), so collect every MapTile field along the type chain.
            List<FieldInfo> fields = new List<FieldInfo>();
            for (Type level = doorType; level != null && level != typeof(MonoBehaviour); level = level.BaseType)
            {
                FieldInfo[] declared = level.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    if (declared[i].FieldType == typeof(MapTile))
                    {
                        fields.Add(declared[i]);
                    }
                }
            }

            cached = fields.ToArray();
            DoorMapTileFields[doorType] = cached;
            return cached;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string name)
        {
            for (Type level = type; level != null && level != typeof(MonoBehaviour); level = level.BaseType)
            {
                FieldInfo field = level.GetField(name, BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static void DestroyMapMarker(MapTile marker)
        {
            if (marker == null || marker.gameObject == null)
            {
                return;
            }

            // Deactivate first: Destroy only takes effect at the end of the frame.
            marker.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(marker.gameObject);
        }

        /// <summary>
        /// Removes the minimap markers a door created for its old position. Map has no API for this, and
        /// the markers are plain children of the map tiles, so they stayed on the map after the move.
        /// Windows are left alone: UpdateWindowMap rebuilds theirs.
        /// </summary>
        private static int ClearDoorMapMarkers(Door door)
        {
            if (door == null || door is Window)
            {
                return 0;
            }

            int removed = 0;
            FieldInfo[] fields = GetDoorMapTileFields(door.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                MapTile marker = fields[i].GetValue(door) as MapTile;
                if (marker != null)
                {
                    DestroyMapMarker(marker);
                    removed++;
                }

                fields[i].SetValue(door, null);
            }

            return removed;
        }

        private static Sprite ReadDoorMapSprite(Door door)
        {
            FieldInfo field = FindFieldInHierarchy(door.GetType(),
                door.locked ? "mapLockedSprite" : "mapUnlockedSprite");
            return field != null ? field.GetValue(door) as Sprite : null;
        }

        private static void ConfigureDoorMarker(MapTile marker, Sprite sprite, Color color, Quaternion rotation)
        {
            if (marker == null)
            {
                return;
            }

            if (marker.SpriteRenderer != null)
            {
                if (sprite != null)
                {
                    marker.SpriteRenderer.sprite = sprite;
                }

                marker.SpriteRenderer.color = color;
            }

            marker.transform.rotation = rotation;
        }

        /// <summary>
        /// Makes sure the door owns exactly one A and one B minimap marker at its CURRENT cells. Doors whose
        /// Initialize builds the markers (SwingDoor family) already satisfy this; StandardDoor builds them
        /// in its private Start(), which must not run again, so they are created here the same way.
        /// </summary>
        private static void EnsureDoorMapMarkers(Door door)
        {
            if (door == null || door is Window || door.ec == null || door.ec.map == null)
            {
                return;
            }

            FieldInfo aField = null;
            FieldInfo bField = null;
            FieldInfo[] fields = GetDoorMapTileFields(door.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name == "aMapTile")
                {
                    aField = fields[i];
                }
                else if (fields[i].Name == "bMapTile")
                {
                    bField = fields[i];
                }
            }

            if (aField == null || bField == null)
            {
                return;
            }

            MapTile aMarker = aField.GetValue(door) as MapTile;
            MapTile bMarker = bField.GetValue(door) as MapTile;
            if (aMarker != null && bMarker != null)
            {
                return;
            }

            // Half a pair is as wrong as none: start over so exactly one pair exists afterwards.
            DestroyMapMarker(aMarker);
            DestroyMapMarker(bMarker);
            Cell aCell = door.aTile;
            Cell bCell = door.bTile;
            if (aCell == null || bCell == null || aCell.room == null || bCell.room == null)
            {
                return;
            }

            Sprite sprite = ReadDoorMapSprite(door);
            aMarker = door.ec.map.AddExtraTile(aCell.position);
            ConfigureDoorMarker(aMarker, sprite, aCell.room.color, door.direction.ToUiRotation());
            bMarker = door.ec.map.AddExtraTile(bCell.position);
            ConfigureDoorMarker(bMarker, sprite, bCell.room.color, door.direction.GetOpposite().ToUiRotation());
            aField.SetValue(door, aMarker);
            bField.SetValue(door, bMarker);
        }

        /// <summary>
        /// Takes the door out of the cell's bookkeeping. Vanilla AssignDoor stores the door's own direction
        /// in doorDirsSpace/doorDirs of BOTH cells of the doorway (not the opposite direction on the far
        /// cell), so removing one direction per cell left stale entries behind. The three fields are rebuilt
        /// from the doors that are still registered instead.
        /// </summary>
        private static void UnregisterDoorFromCell(Cell cell, Door door)
        {
            if (cell == null)
            {
                return;
            }

            RemoveAll(cell.doors, door);
            if (cell.room != null)
            {
                RemoveAll(cell.room.doors, door);
            }

            if (cell.doorDirsSpace != null)
            {
                cell.doorDirsSpace.Clear();
            }

            if (cell.doorDirs != null)
            {
                cell.doorDirs.Clear();
            }

            bool any = false;
            if (cell.doors != null)
            {
                for (int i = 0; i < cell.doors.Count; i++)
                {
                    Door other = cell.doors[i];
                    if (other == null)
                    {
                        continue;
                    }

                    any = true;
                    if (cell.doorDirsSpace != null)
                    {
                        cell.doorDirsSpace.Add(other.direction);
                    }

                    if (cell.doorDirs != null && !cell.doorDirs.Contains(other.direction))
                    {
                        cell.doorDirs.Add(other.direction);
                    }
                }
            }

            cell.doorHere = any;
        }

        /// <summary>The door must end up exactly where it was placed: no wall may remain on either face.</summary>
        private static void EnsureDoorEdgeOpen(EnvironmentController environment,
            IntVector2 position, Direction direction)
        {
            Cell cell = environment.CellFromPosition(position);
            Cell neighbour = environment.CellFromPosition(position + direction.ToIntVector2());
            if (cell != null && neighbour != null
                && (cell.HasWallInDirection(direction) || neighbour.HasWallInDirection(direction.GetOpposite())))
            {
                Debug.LogWarning("[Rebuild the School] A wall was still standing in the new doorway at "
                    + position + " / " + direction + "; removing it.");
                ConnectDoorEdge(environment, position, direction);
            }
        }

        /// <summary>
        /// UpdateCell for a cell that may have doors. UpdateCell only skips the wall on a face listed in the
        /// cell's doorDirs, but vanilla fills the FAR cell's doorDirs with the direction pointing away from
        /// the doorway, so a plain UpdateCell re-walls the doorway and opens that cell's far wall. The list is
        /// replaced by each cell's own view of its doors for the call and restored afterwards.
        /// </summary>
        private static void UpdateCellSafely(EnvironmentController environment, IntVector2 position)
        {
            Cell cell = environment.CellFromPosition(position);
            if (cell == null || cell.Null || cell.doors == null || cell.doors.Count == 0
                || cell.doorDirs == null)
            {
                environment.UpdateCell(position);
                return;
            }

            List<Direction> savedDirs = new List<Direction>(cell.doorDirs);
            bool savedHere = cell.doorHere;
            try
            {
                cell.doorDirs.Clear();
                for (int i = 0; i < cell.doors.Count; i++)
                {
                    Door door = cell.doors[i];
                    if (door == null)
                    {
                        continue;
                    }

                    Direction face = door.position == cell.position
                        ? door.direction : door.direction.GetOpposite();
                    if (!cell.doorDirs.Contains(face))
                    {
                        cell.doorDirs.Add(face);
                    }
                }

                cell.doorHere = cell.doorDirs.Count > 0;
                environment.UpdateCell(position);
            }
            finally
            {
                cell.doorDirs.Clear();
                cell.doorDirs.AddRange(savedDirs);
                cell.doorHere = savedHere;
            }
        }

        /// <summary>
        /// Map.Find draws a tile's walls once, when the player first sees it, so a wall that appears or
        /// disappears on an already-found tile has to be pushed to the minimap explicitly.
        /// </summary>
        private static void RefreshMapTiles(EnvironmentController environment, params IntVector2[] positions)
        {
            if (environment == null || environment.map == null || positions == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    Cell cell = environment.CellFromPosition(positions[i]);
                    if (cell == null || cell.Null || cell.hideFromMap || cell.room == null)
                    {
                        continue;
                    }

                    environment.map.UpdateTile(positions[i].x, positions[i].z, cell.ConstBin, cell.room);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Rebuild the School] Minimap tile refresh failed: " + exception.Message);
            }
        }

        private static bool HasWallRenderer(Cell cell, Direction direction, Door movingDoor)
        {
            if (cell == null || cell.renderers == null)
            {
                return false;
            }

            Vector3 edgeCenter = cell.FloorWorldPosition
                + direction.ToVector3() * 5f + Vector3.up * 5f;
            for (int i = 0; i < cell.renderers.Count; i++)
            {
                Renderer renderer = cell.renderers[i];
                if (renderer != null && renderer.GetComponentInParent<Door>() != movingDoor
                    && renderer.enabled
                    && Vector3.Distance(renderer.bounds.ClosestPoint(edgeCenter), edgeCenter) <= 3f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPosterOnWall(Cell cell, Direction direction)
        {
            if (cell == null || CellTileField == null)
            {
                return false;
            }

            Tile tile = CellTileField.GetValue(cell) as Tile;
            if (tile == null || tile.MeshRenderer == null)
            {
                return false;
            }

            string propertyName;
            switch (direction)
            {
                case Direction.North: propertyName = "_PosterN"; break;
                case Direction.East: propertyName = "_PosterE"; break;
                case Direction.South: propertyName = "_PosterS"; break;
                case Direction.West: propertyName = "_PosterW"; break;
                default: return false;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            tile.MeshRenderer.GetPropertyBlock(block);
            Texture texture = block.GetTexture(propertyName);
            return texture != null && (cell.room == null || cell.room.defaultPoster == null
                || texture != cell.room.defaultPoster);
        }

        private static void ClearDoorCoverage(Cell cell, Direction direction, Door movingDoor)
        {
            if (cell == null)
            {
                return;
            }

            CellCoverage bit = (CellCoverage)(1 << (int)direction);
            bool otherWallObject = HasWallRenderer(cell, direction, movingDoor);
            bool posterOnWall = HasPosterOnWall(cell, direction);
            if (!otherWallObject && HardCoverageField != null)
            {
                CellCoverage hard = (CellCoverage)HardCoverageField.GetValue(cell);
                HardCoverageField.SetValue(cell, hard & ~bit);
            }

            // Doors contribute soft cover too. Preserve it when a poster or another wall object
            // is still using this face, since HardCoverWall(false) is not implemented by the game.
            if (!otherWallObject && !posterOnWall && SoftCoverageField != null)
            {
                CellCoverage soft = (CellCoverage)SoftCoverageField.GetValue(cell);
                SoftCoverageField.SetValue(cell, soft & ~bit);
            }
        }

        private static bool HasOtherDoorOnEdge(Cell cell, Direction direction, Door ignored)
        {
            if (cell == null || cell.doors == null)
            {
                return false;
            }

            for (int i = 0; i < cell.doors.Count; i++)
            {
                Door door = cell.doors[i];
                if (door == null || door == ignored)
                {
                    continue;
                }

                if ((door.position == cell.position && door.direction == direction)
                    || (door.position + door.bOffset == cell.position
                        && door.direction.GetOpposite() == direction))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveAll<T>(List<T> list, T value)
        {
            if (list == null)
            {
                return;
            }

            while (list.Remove(value))
            {
            }
        }

        private bool CanGrabWall(Cell cell, Direction direction, EnvironmentController environment)
        {
            if (!UsableCell(cell) || cell.locked || !cell.HasWallInDirection(direction)
                || cell.WallAnyCovered(direction) || HasOtherDoorOnEdge(cell, direction, null)
                || IsElevatorAdjacent(environment, cell, direction))
            {
                return false;
            }

            Cell neighbour = GetNeighbour(environment, cell, direction);
            return UsableCell(neighbour) && !neighbour.locked
                && neighbour.HasWallInDirection(direction.GetOpposite())
                && !neighbour.WallAnyCovered(direction.GetOpposite())
                && !HasOtherDoorOnEdge(neighbour, direction.GetOpposite(), null);
        }

        private static bool TryResolveWall(RaycastHit hit, EnvironmentController environment,
            out Cell cell, out Direction direction)
        {
            cell = null;
            direction = Direction.Null;
            if (hit.collider == null || hit.collider.GetComponentInParent<Elevator>() != null)
            {
                return false;
            }

            Tile tile = hit.collider.GetComponentInParent<Tile>();
            if (tile == null)
            {
                return false;
            }

            IntVector2 position = IntVector2.GetGridPosition(tile.transform.position);
            if (!environment.ContainsCoordinates(position))
            {
                return false;
            }

            cell = environment.CellFromPosition(position);
            if (!UsableCell(cell))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                Direction candidate = (Direction)i;
                GameObject wallCollider = tile.Collider(candidate);
                if (wallCollider != null && wallCollider.activeInHierarchy
                    && (hit.collider.gameObject == wallCollider
                        || hit.collider.transform.IsChildOf(wallCollider.transform)))
                {
                    direction = candidate;
                    return true;
                }
            }

            // Some builds attach the actual collider to the tile root rather than to the
            // per-edge child. Infer the crossed edge from the hit point as a fallback.
            if (Mathf.Abs(hit.normal.y) > 0.5f)
            {
                return false;
            }

            Vector3 fromCenter = hit.point - cell.CenterWorldPosition;
            if (Mathf.Abs(fromCenter.x) > Mathf.Abs(fromCenter.z))
            {
                direction = fromCenter.x >= 0f ? Direction.East : Direction.West;
            }
            else
            {
                direction = fromCenter.z >= 0f ? Direction.North : Direction.South;
            }

            return cell.HasWallInDirection(direction);
        }

        private static Quaternion SelectionStripRotation(Vector3 rayDirection)
        {
            Vector3 forward = rayDirection.normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < 0.001f)
            {
                up = Vector3.ProjectOnPlane(Vector3.forward, forward);
            }

            return Quaternion.LookRotation(forward, up.normalized);
        }

        private bool TryNearestStructuralHit(Ray ray, PlayerManager player,
            EnvironmentController environment, out RaycastHit nearest, out Door visibleDoor)
        {
            nearest = new RaycastHit();
            visibleDoor = null;
            Quaternion stripRotation = SelectionStripRotation(ray.direction);
            RaycastHit[] hits = Physics.BoxCastAll(ray.origin,
                new Vector3(g.PickupStripHalfThickness, g.PickupHalfHeight,
                    g.PickupStripHalfThickness), ray.direction, stripRotation,
                StructuralPickupRange, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float nearestStructuralDistance = float.MaxValue;
            float nearestBlockingDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)
                    || hit.collider.GetComponentInParent<PlayerManager>() != null
                    || (player != null && hit.collider.transform.IsChildOf(player.transform)))
                {
                    continue;
                }

                if (hit.distance < nearestBlockingDistance)
                {
                    nearestBlockingDistance = hit.distance;
                }

                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null && door.ec == environment && hit.distance < nearestStructuralDistance)
                {
                    nearest = hit;
                    visibleDoor = door;
                    nearestStructuralDistance = hit.distance;
                    found = true;
                    continue;
                }

                Cell wallCell;
                Direction wallDirection;
                if (TryResolveWall(hit, environment, out wallCell, out wallDirection)
                    && hit.distance < nearestStructuralDistance)
                {
                    nearest = hit;
                    visibleDoor = null;
                    nearestStructuralDistance = hit.distance;
                    found = true;
                }
            }

            // Open/broken windows and some open door variants disable their colliders. Use the
            // visible render bounds as a fallback, but only if the view ray is not occluded.
            Door[] doors = FindObjectsOfType<Door>();
            float nearestRenderedDoorDistance = float.MaxValue;
            Door nearestRenderedDoor = null;
            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                if (door == null || door.ec != environment || !door.gameObject.activeInHierarchy
                    || IsElevatorDoor(door))
                {
                    continue;
                }

                Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
                float distance;
                if (g.BoundsIntersectPickupCorridor(renderers, ray, out distance)
                    && distance >= 0f && distance <= StructuralPickupRange
                    && distance < nearestRenderedDoorDistance)
                {
                    nearestRenderedDoorDistance = distance;
                    nearestRenderedDoor = door;
                }
            }

            if (nearestRenderedDoor != null
                && nearestRenderedDoorDistance <= nearestBlockingDistance + 0.15f
                && nearestRenderedDoorDistance < nearestStructuralDistance + 0.15f)
            {
                nearest = new RaycastHit();
                visibleDoor = nearestRenderedDoor;
                nearestStructuralDistance = nearestRenderedDoorDistance;
                found = true;
            }

            if (found && nearestStructuralDistance > nearestBlockingDistance + 0.15f)
            {
                return false;
            }

            return found;
        }

        private static bool IsElevatorDoor(Door door)
        {
            return door == null || door is ElevatorDoor
                || door.GetComponentInParent<Elevator>() != null
                || door.GetComponentInChildren<Elevator>(true) != null;
        }

        private static bool IsElevatorAdjacent(EnvironmentController environment, Cell cell, Direction direction)
        {
            if (environment == null || cell == null)
            {
                return true;
            }

            if (ElevatorCache == null || Time.unscaledTime >= NextElevatorRefreshTime)
            {
                ElevatorCache = FindObjectsOfType<Elevator>();
                NextElevatorRefreshTime = Time.unscaledTime + 0.5f;
            }

            for (int i = 0; i < ElevatorCache.Length; i++)
            {
                Elevator elevator = ElevatorCache[i];
                Door elevatorDoor = elevator != null ? elevator.Door : null;
                if (elevatorDoor == null || elevatorDoor.ec != environment)
                {
                    continue;
                }

                bool sameEdge = elevatorDoor.position == cell.position
                    && elevatorDoor.direction == direction;
                bool reverseEdge = elevatorDoor.position + elevatorDoor.bOffset == cell.position
                    && elevatorDoor.direction.GetOpposite() == direction;
                if (sameEdge || reverseEdge)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreateWallGhost()
        {
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "g2 structural wall preview";
            Collider collider = ghost.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            ghost.transform.localScale = new Vector3(10f, 10f, WallPanelThickness);
            Renderer renderer = ghost.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader != null)
                {
                    Material material = new Material(shader);
                    if (material.HasProperty("_Mode"))
                    {
                        material.SetFloat("_Mode", 3f);
                        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                    }

                    material.color = ValidColor;
                    renderer.material = material;
                }
            }

            return ghost;
        }

        private static void PositionWallGhost(GameObject ghost, Cell cell, Direction direction)
        {
            if (ghost == null || cell == null)
            {
                return;
            }

            ghost.transform.position = cell.FloorWorldPosition
                + direction.ToVector3() * 5f + Vector3.up * 5f;
            ghost.transform.rotation = direction.ToRotation();
        }

        private static void SetGhostColor(StructuralDragState state, Color color)
        {
            SetGhostColor(state != null ? state.Ghost : null, color);
        }

        private static void SetGhostColor(GameObject ghost, Color color)
        {
            if (ghost == null)
            {
                return;
            }

            Renderer renderer = ghost.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null && renderer.material.HasProperty("_Color"))
            {
                renderer.material.color = color;
            }
        }

        private static bool IsCellOccupied(EnvironmentController environment, Cell cell, PlayerManager ignorePlayer)
        {
            if (environment == null || cell == null)
            {
                return true;
            }

            try
            {
                CoreGameManager manager = Singleton<CoreGameManager>.Instance;
                if (manager != null)
                {
                    for (int i = 0; i < manager.setPlayers; i++)
                    {
                        PlayerManager player = manager.GetPlayer(i);
                        if (player != null && player != ignorePlayer && player.ec == environment
                            && CellAt(environment, player.transform.position) == cell)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            List<NPC> npcs = environment.Npcs;
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Count; i++)
                {
                    NPC npc = npcs[i];
                    if (npc != null && CellAt(environment, npc.transform.position) == cell)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddSnapshot(List<CellSnapshot> snapshots, Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].Position == cell.position)
                {
                    return;
                }
            }

            snapshots.Add(new CellSnapshot(cell, cell.ConstBin, cell.shape,
                ReadCoverage(HardCoverageField, cell), ReadCoverage(SoftCoverageField, cell)));
        }

        private static void RestoreCellSnapshots(EnvironmentController environment, List<CellSnapshot> snapshots)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                CellSnapshot snapshot = snapshots[i];
                Cell current = environment.CellFromPosition(snapshot.Position);
                if (current != null && !current.Null)
                {
                    current.SetShape(snapshot.ConstBin, snapshot.Shape);
                    WriteCoverage(HardCoverageField, current, snapshot.HardCoverage);
                    WriteCoverage(SoftCoverageField, current, snapshot.SoftCoverage);
                }
            }
        }

        private static bool ReadWindowBroken(Window window)
        {
            return window != null && WindowBrokenField != null
                && (bool)WindowBrokenField.GetValue(window);
        }

        private static CellCoverage ReadCoverage(FieldInfo field, Cell cell)
        {
            if (field == null || cell == null)
            {
                return CellCoverage.None;
            }

            return (CellCoverage)field.GetValue(cell);
        }

        private static void WriteCoverage(FieldInfo field, Cell cell, CellCoverage coverage)
        {
            if (field != null && cell != null)
            {
                field.SetValue(cell, coverage);
            }
        }

        private static bool TryGetCameraRay(PlayerManager player, out Ray ray)
        {
            ray = new Ray();
            if (player == null)
            {
                return false;
            }

            Camera camera = null;
            try
            {
                CoreGameManager manager = Singleton<CoreGameManager>.Instance;
                GameCamera gameCamera = manager != null ? manager.GetCamera(player.playerNumber) : null;
                camera = gameCamera != null ? gameCamera.camCom : null;
            }
            catch
            {
                camera = null;
            }

            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera == null)
            {
                return false;
            }

            Ray viewportRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            ray = new Ray(camera.transform.position, viewportRay.direction.normalized);
            return true;
        }

        private static bool TryGetCell(EnvironmentController environment, Vector3 position, out Cell cell)
        {
            cell = null;
            if (environment == null)
            {
                return false;
            }

            IntVector2 grid = IntVector2.GetGridPosition(position);
            if (!environment.ContainsCoordinates(grid))
            {
                return false;
            }

            cell = environment.CellFromPosition(grid);
            return cell != null && !cell.Null && cell.room != null;
        }

        private static Cell CellAt(EnvironmentController environment, Vector3 position)
        {
            Cell cell;
            return TryGetCell(environment, position, out cell) ? cell : null;
        }

        private static Cell GetNeighbour(EnvironmentController environment, Cell cell, Direction direction)
        {
            if (environment == null || cell == null)
            {
                return null;
            }

            Cell neighbour;
            return TryGetCell(environment,
                cell.FloorWorldPosition + direction.ToVector3() * 10f,
                out neighbour) ? neighbour : null;
        }

        private static bool UsableCell(Cell cell)
        {
            return cell != null && !cell.Null && cell.room != null;
        }

        private void RefreshPlayer()
        {
            if (_held != null && _held.Player != null)
            {
                _player = _held.Player;
                return;
            }

            try
            {
                CoreGameManager manager = Singleton<CoreGameManager>.Instance;
                _player = manager != null ? manager.GetPlayer(0) : null;
            }
            catch
            {
                _player = null;
            }
        }

        private static bool PickupPressed()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                return true;
            }

            string input = Input.inputString;
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            input = input.ToLowerInvariant();
            return input.Contains("g") || input.Contains("п");
        }

        private static bool PlacePressed()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

            try
            {
                InputManager inputManager = Singleton<InputManager>.Instance;
                return inputManager != null
                    && (inputManager.GetDigitalInput("Interact", true)
                        || inputManager.GetDigitalInput("MouseSubmit", true));
            }
            catch
            {
                return false;
            }
        }

        private enum StructuralKind
        {
            Door,
            Wall
        }

        private sealed class StructuralDragState
        {
            public StructuralKind Kind;
            public EnvironmentController Environment;
            public PlayerManager Player;
            public Door Door;
            public Cell SourceCell;
            public Cell SourceNeighbour;
            public Direction SourceDirection;
            public Transform OriginalParent;
            public Vector3 OriginalPosition;
            public Quaternion OriginalRotation;
            public Renderer[] Renderers = new Renderer[0];
            public Collider[] Colliders = new Collider[0];
            public bool[] RendererEnabled = new bool[0];
            public bool[] ColliderEnabled = new bool[0];
            public bool WasOpen;
            public bool WasLocked;
            public bool WasBrokenWindow;
            public IntVector2 AnchorPosition;
            public int ForwardSteps;
            public int SideSteps;
            public int ManualRotationSteps;
            public Cell TargetCell;
            public Direction TargetDirection;
            public bool TargetValid;
            public GameObject Ghost;
        }

        private sealed class RemovedWall
        {
            public readonly EnvironmentController Environment;
            public readonly IntVector2 Position;
            public readonly Direction Direction;
            public readonly bool CellHadWall;
            public readonly bool NeighbourHadWall;

            public RemovedWall(EnvironmentController environment, IntVector2 position, Direction direction,
                bool cellHadWall, bool neighbourHadWall)
            {
                Environment = environment;
                Position = position;
                Direction = direction;
                CellHadWall = cellHadWall;
                NeighbourHadWall = neighbourHadWall;
            }
        }

        private sealed class CellSnapshot
        {
            public readonly IntVector2 Position;
            public readonly int ConstBin;
            public readonly TileShapeMask Shape;
            public readonly CellCoverage HardCoverage;
            public readonly CellCoverage SoftCoverage;

            public CellSnapshot(Cell cell, int constBin, TileShapeMask shape,
                CellCoverage hardCoverage, CellCoverage softCoverage)
            {
                Position = cell.position;
                ConstBin = constBin;
                Shape = shape;
                HardCoverage = hardCoverage;
                SoftCoverage = softCoverage;
            }
        }

        private static readonly Color ValidColor = new Color(0.1f, 0.9f, 0.4f, 0.4f);
        private static readonly Color InvalidColor = new Color(1f, 0.12f, 0.1f, 0.4f);
    }
}
