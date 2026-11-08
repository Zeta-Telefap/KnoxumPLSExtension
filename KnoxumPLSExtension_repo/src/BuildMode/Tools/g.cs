using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RebuildTheSchool
{
    /// <summary>
    /// Runtime for picking up, carrying, placing, and throwing movable school props.
    /// </summary>
    public sealed class g : MonoBehaviour
    {
        private const float PickupRange = 16f;
        private const float CarryOrbitRadius = 6f;
        private const float PickupHalfWidth = 0f;
        internal const float PickupStripHalfThickness = 0.03f;
        internal const float PickupHalfHeight = 2.25f;
        private const float HitPadding = 0.2f;
        private const float ThrowSpeed = 20f;
        private const float CarryRotationSpeed = 720f;
        private const float CarryMoveSmoothTime = 0.08f;
        private const float DoorWidthTolerance = 0.05f;
        private static readonly Color CarryHighlightColor = new Color(0.15f, 0.65f, 1f, 1f);
        private static readonly Color ValidPlacementColor = new Color(0.15f, 0.9f, 0.35f, 1f);
        private static readonly Color InvalidPlacementColor = new Color(1f, 0.12f, 0.08f, 1f);
        private const float GroundContactTolerance = 0.12f;
        private const float GroundSupportNormal = 0.65f;
        private const float PlacementSearchFineStep = 0.25f;
        private const float PlacementSearchFineRadius = 2.5f;
        private const float PlacementSearchCoarseStep = 0.5f;
        private const float PlacementSearchRadius = 5f;
        private const float HighlightRefreshInterval = 0.25f;
        private const float PickupHighlightGraceTime = 0.35f;
        private const float HighlightTargetHysteresis = 0.15f;
        private const float PlacementPreviewRefreshInterval = 0.25f;
        private const float ReleaseTimeout = 8f;
        private const float WorldTileWidth = 10f;
        private const float RotationSwitchAngle = 50f;
        private const float DoorLookAhead = 7f;
        private const float DoorLateralTolerance = 3f;
        private const float DoorOpenDuration = 2.5f;
        private const float DoorSafeCloseDistance = 2f;
        private const float DoorContactPadding = 0.35f;
        private const float DoorCheckInterval = 0.12f;

        private static readonly FieldInfo HardCoverageField = typeof(Cell).GetField(
            "hardCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SoftCoverageField = typeof(Cell).GetField(
            "softCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly RaycastHit[] RayHits = new RaycastHit[128];
        private static readonly Collider[] WalkabilityHits = new Collider[128];
        private static readonly RayHitComparer RayHitOrder = new RayHitComparer();

        private readonly List<CarryState> _airborne = new List<CarryState>();
        private CarryState _held;
        private PlayerManager _player;
        private Transform _hoveredRoot;
        private Candidate _hoveredCandidate;
        private Transform _suppressHoverRoot;
        private GlowState _hoverGlow;
        private int _lastPickupFrame = -1;
        private float _nextDoorCheckTime;
        private float _nextHighlightRefreshTime;
        private float _lastPickupHighlightTime;

        public static g Instance { get; private set; }
        internal bool IsHoldingObject { get { return _held != null; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            Debug.Log("[Rebuild the School] Carry runtime started. Controls: G, LMB, RMB, R + mouse wheel.");
        }

        private void Update()
        {
            RefreshPlayer();
            UpdateThrownProps();

            if (_held != null && !IsInCurrentLevel(_held))
            {
                ReturnToOrigin(_held);
                _held = null;
            }

            if (_held != null && Time.timeScale > 0f)
            {
                ClearPickupHighlight();
                UpdateHeldManualRotation(_held);
                UpdatePlacementPreview(_held);
                UpdateDoorForCarriedProp();
            }
            else if (_held == null && Time.timeScale > 0f
                && (g2.Instance == null || !g2.Instance.IsDragging))
            {
                UpdatePickupHighlight();
            }
            else if (_held == null)
            {
                ClearPickupHighlight();
            }

            if (_held == null && Time.timeScale > 0f && PickupPressed()
                && (g2.Instance == null || !g2.Instance.IsDragging))
            {
                // If a prop is glowing, G must pick that exact prop before structural selection
                // or a fresh ray query can choose a different object than the visible highlight.
                if (TryPickupHighlighted(_player))
                {
                    return;
                }

                if (g2.Instance != null && g2.Instance.HandlePickupRequest(_player))
                {
                    return;
                }

                TryPickup(_player);
            }
        }

        private void FixedUpdate()
        {
            if (_held == null || _held.Root == null || _held.Body == null)
            {
                return;
            }

            Ray ray;
            if (!TryGetCameraRay(_held.Player, out ray))
            {
                return;
            }

            UpdateHeldFacing(_held, ray);
            // The camera center ray defines a fixed-radius orbit around the camera/player head;
            // manual object yaw changes facing only and never displaces its visual center.
            Ray carryRay = ray;

            float distance = CarryOrbitRadius;
            RaycastHit obstruction;
            if (TryNearestHit(carryRay, CarryOrbitRadius, _held, _held.Player, out obstruction))
            {
                Bounds bounds;
                if (CombinedBounds(_held.Renderers, out bounds))
                {
                    Vector3 worldAnchor = _held.Root.TransformPoint(_held.LocalGrabPoint);
                    Vector3 absDirection = new Vector3(
                        Mathf.Abs(carryRay.direction.x),
                        Mathf.Abs(carryRay.direction.y),
                        Mathf.Abs(carryRay.direction.z));
                    float objectDepth = Vector3.Dot(bounds.center - worldAnchor, carryRay.direction)
                        + Vector3.Dot(bounds.extents, absDirection);
                    distance = Mathf.Max(0.65f, obstruction.distance - objectDepth - 0.25f);
                }
            }

            Vector3 anchorPosition = carryRay.origin + carryRay.direction * distance;
            Vector3 anchorOffset = _held.Root.TransformVector(_held.LocalGrabPoint);
            // The renderer bounds center is the orbit anchor. A short damped follow removes
            // fixed-step camera jitter without the long lag of the previous spring.
            Vector3 desiredPosition = anchorPosition - anchorOffset;
            Vector3 smoothedPosition = Vector3.SmoothDamp(
                _held.Body.position,
                desiredPosition,
                ref _held.PositionSmoothVelocity,
                CarryMoveSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);
            _held.Body.MovePosition(smoothedPosition);
            _held.Body.MoveRotation(_held.CarryRotation);
            BreakWindowsTouchingProp(_held);
        }

        private void UpdateHeldFacing(CarryState state, Ray ray)
        {
            // Unlike pickup, continuous camera movement must not snap through four cardinal directions.
            Quaternion desiredCameraRotation = CameraFacingRotation(-ray.direction, state.CameraRotation);
            if (Quaternion.Angle(state.CameraRotation, desiredCameraRotation) >= 0.1f)
            {
                state.CameraRotation = desiredCameraRotation;
            }

            Quaternion desiredRotation = Quaternion.AngleAxis(
                state.ManualRotationSteps * 90f, Vector3.up) * state.CameraRotation;
            if (Quaternion.Angle(state.TargetRotation, desiredRotation) >= 0.5f)
            {
                state.TargetRotation = desiredRotation;
                if (state.EnvironmentObject != null)
                {
                    state.Coverage = state.EnvironmentObject.coverage.Rotated(
                        DirectionFromRotation(desiredRotation));
                }
                else if (state.IsLocker)
                {
                    state.Coverage = LockerCoverage(desiredRotation);
                }

                Bounds bounds;
                if (CombinedBounds(state.Renderers, out bounds))
                {
                    state.LocalGrabPoint = VisualCenterAnchor(state.Root, bounds);
                }
            }

            // Camera orientation itself stays continuous; rotate quickly but smoothly toward it.
            state.CarryRotation = Quaternion.RotateTowards(
                state.CarryRotation,
                state.TargetRotation,
                CarryRotationSpeed * Time.fixedDeltaTime);
        }

        private void UpdateHeldManualRotation(CarryState state)
        {
            if (state == null || !Input.GetKey(KeyCode.R))
            {
                return;
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }

            // Wheel down turns clockwise/right; wheel up turns counter-clockwise/left.
            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(wheel)));
            int direction = wheel < 0f ? 1 : -1;
            state.ManualRotationSteps = (state.ManualRotationSteps + direction * steps) % 4;
            if (state.ManualRotationSteps < 0)
            {
                state.ManualRotationSteps += 4;
            }
        }

        private void UpdatePickupHighlight()
        {
            if (Time.unscaledTime < _nextHighlightRefreshTime)
            {
                return;
            }

            _nextHighlightRefreshTime = Time.unscaledTime + HighlightRefreshInterval;
            Ray ray;
            Candidate candidate;
            if (_player == null || !TryGetCameraRay(_player, out ray))
            {
                if (Time.unscaledTime - _lastPickupHighlightTime > PickupHighlightGraceTime)
                {
                    ClearPickupHighlight();
                }

                return;
            }

            if (!FindCandidate(_player, ray, out candidate)
                || !CanBeginCarry(candidate, _player))
            {
                if (KeepCurrentPickupHighlight(null, ray))
                {
                    return;
                }

                ClearPickupHighlight();
                _suppressHoverRoot = null;
                return;
            }

            if (_suppressHoverRoot != null)
            {
                if (_suppressHoverRoot == candidate.Root)
                {
                    ClearPickupHighlight();
                    return;
                }

                // Once the player looks away, the placed prop may be highlighted again on a later aim.
                _suppressHoverRoot = null;
            }

            if (_hoveredRoot != candidate.Root && KeepCurrentPickupHighlight(candidate, ray))
            {
                return;
            }

            if (_hoveredRoot != candidate.Root)
            {
                ClearPickupHighlight();
                _hoveredRoot = candidate.Root;
                _hoverGlow = new GlowState(candidate.Root, candidate.Renderers);
            }

            _hoveredCandidate = candidate;
            _lastPickupHighlightTime = Time.unscaledTime;
            if (_hoverGlow != null)
            {
                _hoverGlow.SetColor(ValidPlacementColor);
            }
        }

        private bool KeepCurrentPickupHighlight(Candidate nextCandidate, Ray ray)
        {
            Candidate current = _hoveredCandidate;
            if (current == null || current.Root == null || _hoveredRoot != current.Root
                || !CanBeginCarry(current, _player))
            {
                return false;
            }

            float currentDistance;
            bool currentOnRay = TryGetCenterRayBoundsDistance(
                current.Renderers, ray, out currentDistance) && currentDistance <= PickupRange;
            if (nextCandidate != null)
            {
                float nextDistance;
                bool nextOnRay = TryGetCenterRayBoundsDistance(
                    nextCandidate.Renderers, ray, out nextDistance) && nextDistance <= PickupRange;
                if (nextOnRay && (!currentOnRay
                    || currentDistance > nextDistance + HighlightTargetHysteresis))
                {
                    return false;
                }
            }

            if (!currentOnRay)
            {
                if (Time.unscaledTime - _lastPickupHighlightTime > PickupHighlightGraceTime)
                {
                    return false;
                }

                if (_hoverGlow != null)
                {
                    _hoverGlow.SetColor(ValidPlacementColor);
                }

                return true;
            }

            RaycastHit blocker;
            bool hasBlocker = TryNearestHit(ray, PickupRange, null, _player, out blocker);
            if (!VisibleBeforeBlocker(currentDistance, blocker, hasBlocker, current.Root))
            {
                return false;
            }

            _hoveredCandidate = current;
            _lastPickupHighlightTime = Time.unscaledTime;
            if (_hoverGlow != null)
            {
                _hoverGlow.SetColor(ValidPlacementColor);
            }

            return true;
        }

        private void ClearPickupHighlight()
        {
            if (_hoverGlow != null)
            {
                _hoverGlow.Restore();
                _hoverGlow = null;
            }

            _hoveredRoot = null;
            _hoveredCandidate = null;
        }

        internal bool TryPickupHighlighted(PlayerManager player)
        {
            Candidate candidate = _hoveredCandidate;
            if (candidate == null || candidate.Root == null || _hoveredRoot != candidate.Root)
            {
                return false;
            }

            TryPickup(player, candidate);
            return true;
        }

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

        private void OnDestroy()
        {
            ClearPickupHighlight();
            if (_held != null)
            {
                ReturnToOrigin(_held);
                _held = null;
            }

            for (int i = _airborne.Count - 1; i >= 0; i--)
            {
                ReturnToOrigin(_airborne[i]);
            }

            _airborne.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Consumes PlayerClick only for this mechanic's pickup/carry actions.</summary>
        internal bool HandlePlayerClick(PlayerClick clicker)
        {
            if (clicker == null)
            {
                return false;
            }

            if (_held == null)
            {
                if (Time.timeScale <= 0f || !PickupPressed())
                {
                    return false;
                }

                clicker.clickedThisFrame = null;
                if (!TryPickupHighlighted(clicker.pm))
                {
                    TryPickup(clicker.pm);
                }

                return true;
            }

            clicker.clickedThisFrame = null;
            if (Time.timeScale <= 0f)
            {
                return true;
            }

            // Prefer LMB when both buttons are pressed in the same frame: LMB drops, RMB throws.
            if (PlacePressed())
            {
                DropHeld();
            }
            else if (Input.GetMouseButtonDown(1))
            {
                ThrowHeld();
            }

            return true;
        }

        private static bool PickupPressed()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                return true;
            }

            // Input.inputString also covers the physical G key on non-Latin keyboard layouts.
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

        private void RefreshPlayer()
        {
            if (_held != null && _held.Player != null)
            {
                _player = _held.Player;
                return;
            }

            try
            {
                CoreGameManager gameManager = Singleton<CoreGameManager>.Instance;
                _player = gameManager != null ? gameManager.GetPlayer(0) : null;
            }
            catch
            {
                _player = null;
            }

            if (_player == null || _player.ec == null)
            {
                PlayerClick clicker = FindObjectOfType<PlayerClick>();
                if (clicker != null && clicker.pm != null && clicker.pm.ec != null)
                {
                    _player = clicker.pm;
                }
            }
        }

        private bool IsInCurrentLevel(CarryState state)
        {
            return state != null && state.Root != null && state.Environment != null
                && _player != null && _player.ec == state.Environment;
        }

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
            Door[] doors = FindObjectsOfType<Door>();

            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                if (door == null || door is Window || door.ec != _held.Environment || door.locked)
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

        private void UpdateDoorForThrownProp(CarryState state)
        {
            if (state == null || state.Root == null || Time.unscaledTime < _nextDoorCheckTime)
            {
                return;
            }

            _nextDoorCheckTime = Time.unscaledTime + DoorCheckInterval;
            Bounds propBounds;
            if (!CombinedBounds(state.Renderers, out propBounds))
            {
                return;
            }

            Bounds contactBounds = propBounds;
            contactBounds.Expand(DoorContactPadding * 2f);
            Door[] doors = FindObjectsOfType<Door>();
            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                if (door != null && !(door is Window) && door.ec == state.Environment && !door.locked
                    && DoorBoundsTouchProp(door, contactBounds)
                    && PropFitsDoorway(state.Root, propBounds, door))
                {
                    door.OpenTimed(DoorOpenDuration, false);
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

            // Tiny geometric tolerance avoids missed contacts from floating-point bounds; no cooldown is used.
            propBounds.Expand(0.1f);
            Window[] windows = FindObjectsOfType<Window>();
            for (int i = 0; i < windows.Length; i++)
            {
                Window window = windows[i];
                Bounds windowBounds;
                if (window != null && window.ec == state.Environment
                    && CombinedBounds(window.windows, out windowBounds)
                    && propBounds.Intersects(windowBounds))
                {
                    window.Break(false);
                }
            }
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

        private void TryPickup(PlayerManager player)
        {
            TryPickup(player, null);
        }

        private void TryPickup(PlayerManager player, Candidate preferredCandidate)
        {
            if (_lastPickupFrame == Time.frameCount)
            {
                return;
            }

            _lastPickupFrame = Time.frameCount;
            try
            {
                Debug.Log("[Rebuild the School] G pickup input received.");
                if (player == null || player.ec == null)
                {
                    Debug.LogWarning("[Rebuild the School] Pickup blocked: player or environment is not ready.");
                    return;
                }

                _player = player;
                Ray ray;
                if (!TryGetCameraRay(player, out ray))
                {
                    Debug.LogWarning("[Rebuild the School] Pickup blocked: camera ray is unavailable.");
                    return;
                }

                Candidate candidate = preferredCandidate;
                if (candidate == null)
                {
                    if (!FindCandidate(player, ray, out candidate))
                    {
                        Debug.Log("[Rebuild the School] No supported object intersects the pickup corridor.");
                        return;
                    }
                }
                else if (!CanBeginCarry(candidate, player))
                {
                    Debug.LogWarning("[Rebuild the School] Highlighted pickup is no longer available.");
                    return;
                }

                if (!CanBeginCarry(candidate, player))
                {
                    Debug.LogWarning("[Rebuild the School] Selected prop is not currently movable: "
                        + (candidate.Root != null ? candidate.Root.name : "<missing>"));
                    return;
                }

                // Restore hover blocks before constructing the carry glow; otherwise the carry
                // state would capture the temporary hover tint as its supposed original state.
                ClearPickupHighlight();
                CarryState state;
                if (!BeginCarry(candidate, player, ray, out state))
                {
                    Debug.LogWarning("[Rebuild the School] Pickup setup rejected: " + candidate.Root.name + ".");
                    return;
                }

                _held = state;
                Debug.Log("[Rebuild the School] Picked up: " + state.Root.name
                    + "; visual center locked to the camera orbit.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Rebuild the School] Pickup failed: " + exception);
            }
        }

        private bool TryCreateCandidateFromHit(PlayerManager player, RaycastHit hit, out Candidate candidate)
        {
            candidate = null;
            if (player == null || player.ec == null || hit.collider == null)
            {
                return false;
            }

            EnvironmentObject environmentObject = hit.collider.GetComponentInParent<EnvironmentObject>();
            if (environmentObject != null)
            {
                Cell cell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out cell))
                {
                    return false;
                }

                candidate = new Candidate
                {
                    Root = environmentObject.transform,
                    EnvironmentObject = environmentObject,
                    Renderers = environmentObject.GetComponentsInChildren<Renderer>(true),
                    Cell = cell,
                    ParentKind = ParentKind.RoomObjects
                };
                return CanBeginCarry(candidate, player);
            }

            Transform root = FindSupportedPropRoot(hit.collider.transform);
            if (root == null || IsForbiddenPropHierarchy(root))
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds;
            bool hasBounds = CombinedBounds(renderers, out bounds);
            Cell propCell;
            bool hasCell = TryGetCell(player.ec, root.position, out propCell);
            if (!hasCell && (!hasBounds || !TryGetCell(player.ec, bounds.center, out propCell)))
            {
                return false;
            }

            candidate = new Candidate
            {
                Root = root,
                EnvironmentObject = null,
                Renderers = renderers,
                Cell = propCell,
                ParentKind = IsChildOfCellObjectBase(root, propCell)
                    ? ParentKind.CellObjectBase
                    : ParentKind.Room
            };
            return CanBeginCarry(candidate, player);
        }

        private bool FindCandidate(PlayerManager player, Ray ray, out Candidate best)
        {
            best = null;
            RaycastHit nearestHit;
            bool hasHit = TryNearestHit(ray, PickupRange, null, player, out nearestHit);
            if (hasHit && TryCreateCandidateFromHit(player, nearestHit, out best))
            {
                return true;
            }

            float bestDistance = float.MaxValue;
            EnvironmentObject[] environmentObjects = FindObjectsOfType<EnvironmentObject>();
            for (int i = 0; i < environmentObjects.Length; i++)
            {
                EnvironmentObject environmentObject = environmentObjects[i];
                Cell cell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out cell))
                {
                    continue;
                }

                Renderer[] renderers = environmentObject.GetComponentsInChildren<Renderer>(true);
                float distance;
                if (!TryGetCenterRayBoundsDistance(renderers, ray, out distance)
                    || distance > PickupRange || distance >= bestDistance
                    || !VisibleBeforeBlocker(distance, nearestHit, hasHit,
                        environmentObject.transform))
                {
                    continue;
                }

                Candidate candidate = new Candidate
                {
                    Root = environmentObject.transform,
                    EnvironmentObject = environmentObject,
                    Renderers = renderers,
                    Cell = cell,
                    ParentKind = ParentKind.RoomObjects
                };
                if (CanBeginCarry(candidate, player))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            Renderer[] propRenderers = FindObjectsOfType<Renderer>();
            HashSet<Transform> visitedRoots = new HashSet<Transform>();
            for (int i = 0; i < propRenderers.Length; i++)
            {
                Renderer propRenderer = propRenderers[i];
                Transform root = propRenderer != null
                    ? FindSupportedPropRoot(propRenderer.transform) : null;
                if (root == null || IsForbiddenPropHierarchy(root) || !visitedRoots.Add(root))
                {
                    continue;
                }

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                float distance;
                if (!TryGetCenterRayBoundsDistance(renderers, ray, out distance)
                    || distance > PickupRange || distance >= bestDistance
                    || !VisibleBeforeBlocker(distance, nearestHit, hasHit, root))
                {
                    continue;
                }

                Bounds bounds;
                bool hasBounds = CombinedBounds(renderers, out bounds);
                Cell cell;
                bool hasCell = TryGetCell(player.ec, root.position, out cell);
                if (!hasCell && (!hasBounds || !TryGetCell(player.ec, bounds.center, out cell)))
                {
                    continue;
                }

                Candidate candidate = new Candidate
                {
                    Root = root,
                    EnvironmentObject = null,
                    Renderers = renderers,
                    Cell = cell,
                    ParentKind = IsChildOfCellObjectBase(root, cell)
                        ? ParentKind.CellObjectBase
                        : ParentKind.Room
                };
                if (CanBeginCarry(candidate, player))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best != null;
        }

        private bool CanPickupEnvironmentObject(
            EnvironmentObject environmentObject,
            PlayerManager player,
            out Cell cell)
        {
            cell = null;
            if (environmentObject == null || player == null || environmentObject.Ec != player.ec
                || IsForbiddenPickupRoot(environmentObject.transform)
                || environmentObject.GetComponentInParent<Door>() != null
                || environmentObject.GetComponentInParent<TileBasedObject>() != null
                || environmentObject.GetComponentInParent<NPC>() != null
                || environmentObject.GetComponentInParent<Activity>() != null)
            {
                return false;
            }

            if (HardCoverageField == null || SoftCoverageField == null)
            {
                return false;
            }

            if (!TryGetCell(player.ec, environmentObject.transform.position, out cell))
            {
                Renderer[] renderers = environmentObject.GetComponentsInChildren<Renderer>(true);
                Bounds visualBounds;
                if (!CombinedBounds(renderers, out visualBounds)
                    || !TryGetCell(player.ec, visualBounds.center, out cell))
                {
                    return false;
                }
            }

            // Do not reject a visible prop just because the cell's coverage bookkeeping is incomplete.
            // BeginCarry removes only the coverage bits that are actually present in the source cell.
            return true;
        }

        private static bool CanBeginCarry(Candidate candidate, PlayerManager player)
        {
            if (candidate == null || player == null || player.ec == null
                || candidate.Root == null || !candidate.Root.gameObject.activeInHierarchy
                || IsForbiddenPickupRoot(candidate.Root) || candidate.Cell == null
                || candidate.Renderers == null || candidate.Renderers.Length == 0
                || DestinationParent(candidate.ParentKind, candidate.Cell) == null
                || (candidate.EnvironmentObject != null
                    && candidate.EnvironmentObject.Ec != player.ec))
            {
                return false;
            }

            bool isLocker = candidate.EnvironmentObject == null && IsLocker(candidate.Root);
            if ((candidate.EnvironmentObject != null || isLocker)
                && (HardCoverageField == null || SoftCoverageField == null))
            {
                return false;
            }

            Rigidbody[] childBodies = candidate.Root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < childBodies.Length; i++)
            {
                if (childBodies[i] != null && childBodies[i].transform != candidate.Root)
                {
                    return false;
                }
            }

            Bounds bounds;
            return CombinedBounds(candidate.Renderers, out bounds);
        }

        private bool BeginCarry(Candidate candidate, PlayerManager player, Ray ray, out CarryState state)
        {
            state = null;
            if (candidate == null || candidate.Root == null || IsForbiddenPickupRoot(candidate.Root)
                || candidate.Cell == null || candidate.Renderers == null || candidate.Renderers.Length == 0
                || DestinationParent(candidate.ParentKind, candidate.Cell) == null)
            {
                return false;
            }

            Rigidbody[] childBodies = candidate.Root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < childBodies.Length; i++)
            {
                if (childBodies[i] != null && childBodies[i].transform != candidate.Root)
                {
                    return false;
                }
            }

            Bounds bounds;
            if (!CombinedBounds(candidate.Renderers, out bounds))
            {
                return false;
            }

            Quaternion originalRotation = candidate.Root.rotation;
            Quaternion cardinalFallback = DirectionFromRotation(originalRotation).ToRotation();
            // Quantize camera facing once on pickup; while carried, facing follows camera yaw continuously.
            Quaternion carryRotation = CameraCardinalRotation(-ray.direction, cardinalFallback);
            Direction originDirection = DirectionFromRotation(originalRotation);
            Direction carryDirection = DirectionFromRotation(carryRotation);
            bool isLocker = candidate.EnvironmentObject == null && IsLocker(candidate.Root);
            bool usesCoverage = candidate.EnvironmentObject != null || isLocker;
            CellCoverage originCoverage = candidate.EnvironmentObject != null
                ? candidate.EnvironmentObject.coverage.Rotated(originDirection)
                : (isLocker ? LockerCoverage(originalRotation) : CellCoverage.None);
            CellCoverage coverage = candidate.EnvironmentObject != null
                ? candidate.EnvironmentObject.coverage.Rotated(carryDirection)
                : (isLocker ? LockerCoverage(carryRotation) : CellCoverage.None);

            CellCoverage removedHard = CellCoverage.None;
            CellCoverage removedSoft = CellCoverage.None;
            if (usesCoverage)
            {
                if (HardCoverageField == null || SoftCoverageField == null)
                {
                    return false;
                }

                CellCoverage protectedBits = OtherCoverageProviders(
                    player.ec, candidate.Cell, candidate.Root, originCoverage);
                CellCoverage hard = ReadCoverage(HardCoverageField, candidate.Cell);
                CellCoverage soft = ReadCoverage(SoftCoverageField, candidate.Cell);
                removedHard = hard & originCoverage & ~protectedBits;
                removedSoft = soft & originCoverage & ~protectedBits;
            }

            List<MeshCollider> nonConvex = new List<MeshCollider>();
            Collider[] colliders = candidate.Root.GetComponentsInChildren<Collider>(true);
            bool hasDynamicSolidCollider = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                MeshCollider mesh = collider as MeshCollider;
                if (mesh != null && !mesh.convex)
                {
                    nonConvex.Add(mesh);
                }
                else
                {
                    hasDynamicSolidCollider = true;
                }
            }

            BoxCollider generatedCollider = null;
            if (!hasDynamicSolidCollider)
            {
                generatedCollider = AddBoundsCollider(candidate.Root, bounds);
                if (generatedCollider == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < nonConvex.Count; i++)
            {
                nonConvex[i].enabled = false;
            }

            Rigidbody body = candidate.Root.GetComponent<Rigidbody>();
            bool addedBody = body == null;
            try
            {
                if (addedBody)
                {
                    body = candidate.Root.gameObject.AddComponent<Rigidbody>();
                }
            }
            catch
            {
                RestoreNonConvex(nonConvex);
                if (generatedCollider != null)
                {
                    Destroy(generatedCollider);
                }

                throw;
            }

            if (body == null)
            {
                RestoreNonConvex(nonConvex);
                if (generatedCollider != null)
                {
                    Destroy(generatedCollider);
                }

                return false;
            }

            state = new CarryState();
            state.Root = candidate.Root;
            state.EnvironmentObject = candidate.EnvironmentObject;
            state.Environment = player.ec;
            state.Player = player;
            state.OriginCell = candidate.Cell;
            state.OriginParent = candidate.Root.parent;
            state.OriginPosition = candidate.Root.position;
            state.InitialY = candidate.Root.position.y;
            state.OriginFloorY = candidate.Cell.FloorWorldPosition.y;
            state.CellOffset = candidate.Root.position - candidate.Cell.FloorWorldPosition;
            state.OriginalRotation = originalRotation;
            state.CameraRotation = carryRotation;
            state.CarryRotation = carryRotation;
            state.TargetRotation = carryRotation;
            state.LocalGrabPoint = Vector3.zero;
            state.Coverage = coverage;
            state.UsesCoverage = usesCoverage;
            state.IsLocker = isLocker;
            state.RemovedHardCoverage = removedHard;
            state.RemovedSoftCoverage = removedSoft;
            state.ParentKind = candidate.ParentKind;
            state.Renderers = candidate.Renderers;
            state.RegisteredRenderers = RegisteredRenderers(candidate.Cell, candidate.Renderers);
            state.Body = body;
            state.AddedBody = addedBody;
            state.AddedCollider = generatedCollider;
            state.PlacementColliders = candidate.Root.GetComponentsInChildren<Collider>(true);
            state.NonConvexColliders = nonConvex;
            SaveBodySettings(state);

            try
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.constraints = RigidbodyConstraints.None;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                if (addedBody)
                {
                    body.mass = EstimateMass(bounds);
                }

                body.rotation = carryRotation;
                Bounds carryBounds;
                if (!CombinedBounds(candidate.Renderers, out carryBounds))
                {
                    throw new InvalidOperationException("The carried prop has no active renderers after rotation.");
                }

                state.LocalGrabPoint = VisualCenterAnchor(candidate.Root, carryBounds);
                state.Glow = new GlowState(state.Root, state.Renderers);
                state.Glow.SetColor(CarryHighlightColor);
                IgnorePlayerCollisions(state);

                RemoveRendererReferences(candidate.Cell, state.RegisteredRenderers);
                if (usesCoverage)
                {
                    WriteCoverage(candidate.Cell,
                        ReadCoverage(HardCoverageField, candidate.Cell) & ~removedHard,
                        ReadCoverage(SoftCoverageField, candidate.Cell) & ~removedSoft);
                }

                return true;
            }
            catch
            {
                ReturnToOrigin(state);
                state = null;
                throw;
            }
        }

        private void DropHeld()
        {
            ReleaseHeld(false);
        }

        private void ThrowHeld()
        {
            ReleaseHeld(true);
        }

        private void ReleaseHeld(bool throwForward)
        {
            if (_held == null || _held.Root == null || _held.Body == null)
            {
                return;
            }

            CarryState state = _held;
            try
            {
                Ray ray;
                if (!TryGetCameraRay(state.Player, out ray))
                {
                    ray = new Ray(state.Player.transform.position, state.Player.transform.forward);
                }

                state.Root.SetParent(state.Environment.transform, true);
                RestorePlayerCollisions(state);
                RestoreVisualGlow(state);
                _suppressHoverRoot = state.Root;
                Rigidbody body = state.Body;
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.drag = throwForward ? 0.03f : 0.15f;
                body.angularDrag = 0.18f;
                body.angularVelocity = Vector3.zero;
                body.velocity = throwForward ? PlayerVelocity(state.Player) * 0.35f : Vector3.zero;

                if (throwForward)
                {
                    float mass = Mathf.Max(0.5f, body.mass);
                    body.AddForce(ray.direction * ThrowSpeed * mass, ForceMode.Impulse);
                }

                body.WakeUp();
                state.Age = 0f;
                _airborne.Add(state);
                _held = null;
                Debug.Log(throwForward
                    ? "[Rebuild the School] Prop thrown with collision enabled."
                    : "[Rebuild the School] Prop dropped; it will settle on contact.");
            }
            catch (Exception exception)
            {
                ReturnToOrigin(state);
                _held = null;
                Debug.LogError("[Rebuild the School] Release failed; object returned to origin: " + exception);
            }
        }

        private void UpdateThrownProps()
        {
            for (int i = _airborne.Count - 1; i >= 0; i--)
            {
                CarryState state = _airborne[i];
                if (state == null || state.Root == null || state.Body == null
                    || _player == null || _player.ec != state.Environment)
                {
                    ReturnToOrigin(state);
                    _airborne.RemoveAt(i);
                    continue;
                }

                state.Age += Time.deltaTime;
                UpdateDoorForThrownProp(state);
                BreakWindowsTouchingProp(state);

                Cell supportCell;
                if (TryGetGroundContact(state, out supportCell))
                {
                    SettleThrown(state, supportCell);
                    _airborne.RemoveAt(i);
                }
                else if (state.Age >= ReleaseTimeout)
                {
                    ReturnToOrigin(state);
                    _airborne.RemoveAt(i);
                }
            }
        }

        private bool TryGetGroundContact(CarryState state, out Cell supportCell)
        {
            supportCell = null;
            Bounds bounds;
            if (state == null || state.Root == null || state.Body == null
                || !CombinedBounds(state.Renderers, out bounds))
            {
                return false;
            }

            // BB+ floor tiles do not provide a horizontal physics collider (they expose wall colliders only).
            // Use the environment's floor plane as a fallback so released props cannot fall through it.
            Cell floorCell;
            if (TryGetCell(state.Environment, state.Root.position, out floorCell))
            {
                float floorGap = bounds.min.y - floorCell.FloorWorldPosition.y;
                if (floorGap <= GroundContactTolerance)
                {
                    Vector3 landingPosition = state.Root.position;
                    landingPosition.y -= floorGap;
                    state.Body.position = landingPosition;
                    state.Root.position = landingPosition;
                    state.Body.velocity = Vector3.zero;
                    state.Body.angularVelocity = Vector3.zero;
                    supportCell = floorCell;
                    return true;
                }
            }

            Ray groundRay = new Ray(bounds.center, Vector3.down);
            RaycastHit hit;
            float rayLength = bounds.extents.y + GroundContactTolerance + 0.1f;
            if (!TryNearestHit(groundRay, rayLength, state, state.Player, out hit)
                || Vector3.Dot(hit.normal, Vector3.up) < GroundSupportNormal)
            {
                return false;
            }

            float bottomGap = hit.distance - bounds.extents.y;
            return bottomGap <= GroundContactTolerance
                && bottomGap >= -0.35f
                && TryGetCell(state.Environment, state.Root.position, out supportCell);
        }

        private void SettleThrown(CarryState state, Cell target)
        {
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
            RoomController placementRoom = target.room;
            Cell playerCell;
            if (state.Player != null
                && TryGetCell(state.Environment, state.Player.transform.position, out playerCell))
            {
                placementRoom = playerCell.room;
            }

            state.PreferredRoom = placementRoom;
            try
            {
                state.PlacementWalkabilityCache = new Dictionary<Cell, bool>();
                // If physics carried the prop through a wall, keep it on the player's side of that wall.
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
                if (TryPlace(state, target) || TryPlaceNearby(state, landingPosition))
                {
                    return;
                }
            }
            finally
            {
                state.PlacementWalkabilityCache = null;
                state.PreferredRoom = null;
            }

            ReturnToOrigin(state);
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
                || target.room == null
                || (state.PreferredRoom != null && target.room != state.PreferredRoom)
                || DestinationParent(state.ParentKind, target) == null)
            {
                return false;
            }

            if (!PlayerCanReachCell(state, target))
            {
                return false;
            }

            // Player-sized capsule/path tests decide reachability; this separate physics test
            // prevents the actual prop colliders from being committed through a wall or solid prop.
            Vector3 proposedPosition = target.FloorWorldPosition + state.CellOffset;
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

                Debug.LogError("[Rebuild the School] Placement transaction rolled back: " + exception);
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
            }
            catch (Exception exception)
            {
                Debug.LogError("[Rebuild the School] Failed to restore prop: " + exception);
            }
        }

        private static void SaveBodySettings(CarryState state)
        {
            Rigidbody body = state.Body;
            state.OriginalMass = body.mass;
            state.OriginalDrag = body.drag;
            state.OriginalAngularDrag = body.angularDrag;
            state.OriginalIsKinematic = body.isKinematic;
            state.OriginalUseGravity = body.useGravity;
            state.OriginalDetectCollisions = body.detectCollisions;
            state.OriginalConstraints = body.constraints;
            state.OriginalCollisionMode = body.collisionDetectionMode;
            state.OriginalInterpolation = body.interpolation;
            state.OriginalVelocity = body.velocity;
            state.OriginalAngularVelocity = body.angularVelocity;
        }

        private static void IgnorePlayerCollisions(CarryState state)
        {
            if (state == null || state.Root == null || state.Player == null)
            {
                return;
            }

            Collider[] playerColliders = state.Player.GetComponentsInChildren<Collider>(true);
            Collider[] propColliders = state.Root.GetComponentsInChildren<Collider>(true);
            if (state.IgnoredCollisionPairs == null)
            {
                state.IgnoredCollisionPairs = new List<IgnoredCollisionPair>();
            }

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                {
                    continue;
                }

                for (int j = 0; j < propColliders.Length; j++)
                {
                    Collider propCollider = propColliders[j];
                    if (propCollider == null || !propCollider.enabled || propCollider.isTrigger)
                    {
                        continue;
                    }

                    try
                    {
                        Physics.IgnoreCollision(playerCollider, propCollider, true);
                        state.IgnoredCollisionPairs.Add(new IgnoredCollisionPair
                        {
                            PlayerCollider = playerCollider,
                            PropCollider = propCollider
                        });
                    }
                    catch
                    {
                        // Ignore pairs that Unity considers invalid; the prop still collides with the world.
                    }
                }
            }
        }

        private static void RestoreVisualGlow(CarryState state)
        {
            if (state != null && state.Glow != null)
            {
                state.Glow.Restore();
                state.Glow = null;
            }
        }

        private static void RestorePlayerCollisions(CarryState state)
        {
            if (state == null || state.IgnoredCollisionPairs == null)
            {
                return;
            }

            for (int i = state.IgnoredCollisionPairs.Count - 1; i >= 0; i--)
            {
                IgnoredCollisionPair pair = state.IgnoredCollisionPairs[i];
                if (pair != null && pair.PlayerCollider != null && pair.PropCollider != null)
                {
                    try
                    {
                        Physics.IgnoreCollision(pair.PlayerCollider, pair.PropCollider, false);
                    }
                    catch
                    {
                        // Either collider may have been destroyed while the prop was carried.
                    }
                }
            }

            state.IgnoredCollisionPairs.Clear();
        }

        private static void RestoreBody(CarryState state, bool restoreVelocity)
        {
            if (state == null)
            {
                return;
            }

            RestorePlayerCollisions(state);
            RestoreVisualGlow(state);
            if (state.Body != null && !state.Body.isKinematic)
            {
                state.Body.isKinematic = true;
            }

            RestoreNonConvex(state.NonConvexColliders);
            if (state.Body != null)
            {
                if (state.AddedBody)
                {
                    Destroy(state.Body);
                    state.Body = null;
                }
                else
                {
                    state.Body.mass = state.OriginalMass;
                    state.Body.drag = state.OriginalDrag;
                    state.Body.angularDrag = state.OriginalAngularDrag;
                    state.Body.useGravity = state.OriginalUseGravity;
                    state.Body.detectCollisions = state.OriginalDetectCollisions;
                    state.Body.constraints = state.OriginalConstraints;
                    state.Body.collisionDetectionMode = state.OriginalCollisionMode;
                    state.Body.interpolation = state.OriginalInterpolation;
                    state.Body.isKinematic = state.OriginalIsKinematic;
                    state.Body.velocity = restoreVelocity ? state.OriginalVelocity : Vector3.zero;
                    state.Body.angularVelocity = restoreVelocity ? state.OriginalAngularVelocity : Vector3.zero;
                }
            }

            if (state.AddedCollider != null)
            {
                Destroy(state.AddedCollider);
                state.AddedCollider = null;
            }
        }

        private static void RestoreNonConvex(List<MeshCollider> colliders)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = true;
                }
            }
        }

        private static bool TryGetCell(
            EnvironmentController environment,
            Vector3 worldPosition,
            out Cell cell)
        {
            cell = null;
            if (environment == null)
            {
                return false;
            }

            IntVector2 position = IntVector2.GetGridPosition(worldPosition);
            if (!environment.ContainsCoordinates(position))
            {
                return false;
            }

            cell = environment.CellFromPosition(position);
            return cell != null && !cell.Null && cell.room != null;
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
            // Keep the virtual carry/pickup origin at the camera, never at the player transform
            // or the near clipping plane. Retain the viewport ray's aiming direction.
            ray = new Ray(camera.transform.position, viewportRay.direction.normalized);
            return true;
        }

        private static bool TryNearestHit(
            Ray ray,
            float maxDistance,
            CarryState ignored,
            PlayerManager player,
            out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            int count = Physics.RaycastNonAlloc(
                ray, RayHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return false;
            }

            Array.Sort(RayHits, 0, count, RayHitOrder);
            for (int i = 0; i < count; i++)
            {
                Collider collider = RayHits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                Transform hit = collider.transform;
                bool isIgnored = ignored != null && ignored.Root != null
                    && (hit == ignored.Root || hit.IsChildOf(ignored.Root));
                bool isPlayer = player != null
                    && (hit == player.transform || hit.IsChildOf(player.transform));
                if (isIgnored || isPlayer)
                {
                    continue;
                }

                nearest = RayHits[i];
                return true;
            }

            return false;
        }

        private static bool TryGetCenterRayBoundsDistance(
            Renderer[] renderers, Ray ray, out float distance)
        {
            distance = float.MaxValue;
            if (renderers == null)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float rendererDistance;
                if (renderer.bounds.IntersectRay(ray, out rendererDistance)
                    && rendererDistance >= 0f && rendererDistance < distance)
                {
                    distance = rendererDistance;
                    found = true;
                }
            }

            return found;
        }

        internal static bool BoundsIntersectPickupCorridor(
            Renderer[] renderers,
            Ray ray,
            out float distance)
        {
            distance = float.MaxValue;
            Bounds bounds;
            return CombinedBounds(renderers, out bounds)
                && BoundsIntersectsPickupStrip(bounds, ray, out distance);
        }

        private static bool BoundsIntersectsPickupStrip(Bounds bounds, Ray ray, out float distance)
        {
            distance = float.MaxValue;
            Vector3 direction = ray.direction.normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.Cross(direction, Vector3.forward);
            }

            if (right.sqrMagnitude < 0.001f)
            {
                return false;
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(right, direction).normalized;
            Vector3 delta = bounds.center - ray.origin;
            Vector3 extents = bounds.extents;
            Vector3 absRight = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            Vector3 absUp = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            Vector3 absDirection = new Vector3(
                Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));

            float sideReach = Vector3.Dot(extents, absRight) + PickupHalfWidth;
            float verticalReach = Vector3.Dot(extents, absUp) + PickupHalfHeight;
            if (Mathf.Abs(Vector3.Dot(delta, right)) > sideReach
                || Mathf.Abs(Vector3.Dot(delta, up)) > verticalReach)
            {
                return false;
            }

            float centerDistance = Vector3.Dot(delta, direction);
            float depth = Vector3.Dot(extents, absDirection);
            float entryDistance = centerDistance - depth;
            float exitDistance = centerDistance + depth;
            if (exitDistance < 0f || entryDistance > PickupRange)
            {
                return false;
            }

            distance = Mathf.Max(0f, entryDistance);
            return true;
        }

        private static bool VisibleBeforeBlocker(
            float candidateDistance,
            RaycastHit nearestHit,
            bool hasHit,
            Transform candidate)
        {
            if (!hasHit || nearestHit.collider == null)
            {
                return true;
            }

            Transform blocker = nearestHit.collider.transform;
            if (candidate != null && (blocker == candidate || blocker.IsChildOf(candidate)))
            {
                return true;
            }

            return candidateDistance <= nearestHit.distance + HitPadding;
        }

        private static bool CombinedBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            if (renderers == null)
            {
                return false;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static Vector3 VisualCenterAnchor(Transform root, Bounds worldBounds)
        {
            // Track the visible bounds center, not a selected side or the object's pivot.
            return root.InverseTransformPoint(worldBounds.center);
        }

        private static Transform FindSupportedPropRoot(Transform transform)
        {
            // Use the outermost recognized prop in the local hierarchy. Picking the nearest
            // named child made cabinet doors/plant branches appear as separate hover targets,
            // which could switch the glow and make G appear to miss the object.
            Transform current = transform;
            Transform propRoot = null;
            while (current != null)
            {
                if (current.GetComponent<EnvironmentObject>() != null
                    || current.GetComponent<TileBasedObject>() != null
                    || current.GetComponent<RoomController>() != null
                    || current.GetComponent<NPC>() != null
                    || current.GetComponent<Activity>() != null)
                {
                    break;
                }

                if (IsSupportedPropName(current.name))
                {
                    propRoot = current;
                }

                current = current.parent;
            }

            return propRoot;
        }

        private static bool IsSupportedPropName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string value = name.ToLowerInvariant();
            if (value.Contains("bedroom") || value.Contains("classroom") || value.Contains("bathroom"))
            {
                return false;
            }

            return value.Contains("desk") || value.Contains("table") || value.Contains("chair")
                || value.Contains("locker") || value.Contains("cabinet") || value.Contains("vending")
                || value.Contains("soda") || value.Contains("zesty") || value.Contains("bed")
                || value.Contains("bench") || value.Contains("bookshelf") || value.Contains("bookcase")
                || value.Contains("shelf") || value.Contains("trash") || value.Contains("bin")
                || value.Contains("plant") || value.Contains("potted") || value.Contains("planter")
                || value.Contains("flower") || value.Contains("fern") || value.Contains("palm")
                || value.Contains("foliage") || value.Contains("sofa") || value.Contains("couch")
                || value.Contains("stool") || value.Contains("piano")
                || value.Contains("cart") || value.Contains("crate") || value.Contains("counter")
                || value.Contains("drawer") || value.Contains("filing");
        }

        private static bool IsForbiddenPropHierarchy(Transform root)
        {
            return root == null || IsForbiddenPickupRoot(root)
                || root.GetComponentInParent<EnvironmentObject>() != null
                || root.GetComponentInParent<Door>() != null
                || root.GetComponentInParent<TileBasedObject>() != null
                || root.GetComponentInParent<NPC>() != null
                || root.GetComponentInParent<Activity>() != null;
        }

        private static bool IsForbiddenPickupRoot(Transform root)
        {
            if (root == null
                || root.GetComponentInParent<Elevator>() != null
                || root.GetComponentInChildren<Elevator>(true) != null
                || root.GetComponentInParent<Item>() != null
                || root.GetComponentInChildren<Item>(true) != null
                || root.GetComponentInParent<CoverCloud>() != null
                || root.GetComponentInChildren<CoverCloud>(true) != null
                || root.GetComponentInChildren<Light>(true) != null)
            {
                return true;
            }

            string name = root.name.ToLowerInvariant();
            return name.Contains("elevator") || name.Contains("lift")
                || name.Contains("lamp") || name.Contains("light")
                || name.Contains("chandelier") || name.Contains("sconce")
                || name.Contains("bsoda") || name.Contains("no squee") || name.Contains("nosquee")
                || name.Contains("covercloud") || name.Contains("chalkcloud");
        }

        private static bool IsLocker(Transform root)
        {
            return root != null && root.name.ToLowerInvariant().Contains("locker");
        }

        private static CellCoverage LockerCoverage(Quaternion rotation)
        {
            Direction wallDirection = DirectionFromRotation(rotation);
            return CellCoverage.Down
                | (wallDirection == Direction.Null ? CellCoverage.None : wallDirection.ToCoverage());
        }

        private static bool IsChildOfCellObjectBase(Transform root, Cell cell)
        {
            return root != null && cell != null && cell.ObjectBase != null
                && (root == cell.ObjectBase || root.IsChildOf(cell.ObjectBase));
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

        private static BoxCollider AddBoundsCollider(Transform root, Bounds worldBounds)
        {
            if (root == null)
            {
                return null;
            }

            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = root.InverseTransformPoint(worldBounds.center);
            Vector3 localBounds = LocalBoundsExtents(root, worldBounds);
            collider.size = new Vector3(
                Mathf.Max(0.1f, localBounds.x * 2f),
                Mathf.Max(0.1f, localBounds.y * 2f),
                Mathf.Max(0.1f, localBounds.z * 2f));
            return collider;
        }

        private static float EstimateMass(Bounds bounds)
        {
            float volume = Mathf.Max(0.1f, bounds.size.x * bounds.size.y * bounds.size.z);
            return Mathf.Clamp(volume * 0.08f, 1f, 30f);
        }

        private static List<Renderer> RegisteredRenderers(Cell cell, Renderer[] renderers)
        {
            List<Renderer> registered = new List<Renderer>();
            if (cell == null || cell.renderers == null || renderers == null)
            {
                return registered;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && cell.renderers.Contains(renderers[i]))
                {
                    registered.Add(renderers[i]);
                }
            }

            return registered;
        }

        private static void MoveRendererReferences(Cell from, Cell to, List<Renderer> renderers)
        {
            RemoveRendererReferences(from, renderers);
            AddRendererReferences(to, renderers);
        }

        private static void RemoveRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (cell == null || cell.renderers == null || renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    cell.renderers.Remove(renderers[i]);
                }
            }
        }

        private static void AddRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (cell == null || cell.renderers == null || renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null && !cell.renderers.Contains(renderers[i]))
                {
                    cell.renderers.Add(renderers[i]);
                }
            }
        }

        private static CellCoverage OtherCoverageProviders(
            EnvironmentController environment,
            Cell cell,
            Transform excludedRoot,
            CellCoverage coverage)
        {
            CellCoverage provided = CellCoverage.None;
            EnvironmentObject[] objects = FindObjectsOfType<EnvironmentObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                EnvironmentObject item = objects[i];
                if (item == null || item.transform == excludedRoot || item.Ec != environment)
                {
                    continue;
                }

                Cell itemCell;
                if (TryGetCell(environment, item.transform.position, out itemCell) && itemCell == cell)
                {
                    provided |= item.coverage.Rotated(DirectionFromRotation(item.transform.rotation));
                }
            }

            MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
            HashSet<Transform> visited = new HashSet<Transform>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Transform root = renderers[i] != null
                    ? FindSupportedPropRoot(renderers[i].transform) : null;
                if (root == null || root == excludedRoot || !IsLocker(root) || !visited.Add(root))
                {
                    continue;
                }

                Cell itemCell;
                if (TryGetCell(environment, root.position, out itemCell) && itemCell == cell)
                {
                    provided |= LockerCoverage(root.rotation);
                }
            }

            return provided & coverage;
        }

        private static CellCoverage ReadCoverage(FieldInfo field, Cell cell)
        {
            if (field == null || cell == null)
            {
                throw new InvalidOperationException("Cell coverage data is unavailable.");
            }

            return (CellCoverage)field.GetValue(cell);
        }

        private static void WriteCoverage(Cell cell, CellCoverage hard, CellCoverage soft)
        {
            if (cell == null || HardCoverageField == null || SoftCoverageField == null)
            {
                throw new InvalidOperationException("Cell coverage fields are unavailable.");
            }

            HardCoverageField.SetValue(cell, hard);
            SoftCoverageField.SetValue(cell, soft);
        }

        private static Quaternion CameraFacingRotation(Vector3 towardCamera, Quaternion fallback)
        {
            Vector3 flatFacing = Vector3.ProjectOnPlane(towardCamera, Vector3.up);
            if (flatFacing.sqrMagnitude < 0.001f)
            {
                return fallback;
            }

            return Quaternion.LookRotation(flatFacing.normalized, Vector3.up);
        }

        private static Quaternion CameraCardinalRotation(Vector3 cameraForward, Quaternion fallback)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
            {
                return fallback;
            }

            flatForward.Normalize();
            Vector3 currentForward = Vector3.ProjectOnPlane(fallback * Vector3.forward, Vector3.up);
            if (currentForward.sqrMagnitude > 0.001f
                && Vector3.Angle(currentForward.normalized, flatForward) < RotationSwitchAngle)
            {
                return fallback;
            }

            Direction direction = Directions.DirFromVector3(flatForward, 45f);
            return direction == Direction.Null ? fallback : direction.ToRotation();
        }

        private static Direction DirectionFromRotation(Quaternion rotation)
        {
            Direction direction = Directions.DirFromVector3(rotation * Vector3.forward, 45f);
            return direction == Direction.Null ? Direction.North : direction;
        }

        private static Vector3 PlayerVelocity(PlayerManager player)
        {
            return player != null && player.plm != null && player.plm.Entity != null
                ? player.plm.Entity.Velocity
                : Vector3.zero;
        }

        private static Vector3 LocalBoundsExtents(Transform root, Bounds worldBounds)
        {
            Vector3 x = root.InverseTransformVector(new Vector3(worldBounds.extents.x, 0f, 0f));
            Vector3 y = root.InverseTransformVector(new Vector3(0f, worldBounds.extents.y, 0f));
            Vector3 z = root.InverseTransformVector(new Vector3(0f, 0f, worldBounds.extents.z));
            return new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        }

        private sealed class Candidate
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public Renderer[] Renderers;
            public Cell Cell;
            public ParentKind ParentKind;
        }

        private enum ParentKind
        {
            Room,
            RoomObjects,
            CellObjectBase
        }

        internal sealed class GlowState
        {
            // BB+ shaders use _TextureColor (MaterialModifier.SetColor writes this property).
            private static readonly int TextureColorId = Shader.PropertyToID("_TextureColor");
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

            private readonly Transform _root;
            private readonly Renderer[] _renderers;
            private readonly SpriteRenderer[] _spriteRenderers;
            private readonly Color[] _originalSpriteColors;
            private readonly MaterialPropertyBlock[][] _originalBlocks;
            private readonly int[][] _tintPropertyIds;
            private readonly bool[][] _hasEmission;
            private readonly Color[][] _baseColors;
            private readonly GameObject _lightObject;
            private readonly Light _light;
            private bool _restored;

            public GlowState(Transform root, Renderer[] renderers)
            {
                _root = root;
                _renderers = renderers ?? new Renderer[0];
                _spriteRenderers = new SpriteRenderer[_renderers.Length];
                _originalSpriteColors = new Color[_renderers.Length];
                _originalBlocks = new MaterialPropertyBlock[_renderers.Length][];
                _tintPropertyIds = new int[_renderers.Length][];
                _hasEmission = new bool[_renderers.Length][];
                _baseColors = new Color[_renderers.Length][];

                Bounds bounds;
                bool hasBounds = CombinedBounds(_renderers, out bounds);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                    if (spriteRenderer != null)
                    {
                        _spriteRenderers[i] = spriteRenderer;
                        _originalSpriteColors[i] = spriteRenderer.color;
                        // SpriteRenderer owns per-sprite texture/color state; never snapshot or
                        // reapply a material property block for it.
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    int materialCount = materials != null ? materials.Length : 0;
                    _originalBlocks[i] = new MaterialPropertyBlock[materialCount];
                    _tintPropertyIds[i] = new int[materialCount];
                    _hasEmission[i] = new bool[materialCount];
                    _baseColors[i] = new Color[materialCount];

                    for (int j = 0; j < materialCount; j++)
                    {
                        MaterialPropertyBlock original = new MaterialPropertyBlock();
                        // BB+ MaterialModifier stores per-material blocks, so capture/restore those too.
                        renderer.GetPropertyBlock(original, j);
                        _originalBlocks[i][j] = original;

                        Material material = materials[j];
                        if (material == null)
                        {
                            continue;
                        }

                        if (material.HasProperty(TextureColorId))
                        {
                            _tintPropertyIds[i][j] = TextureColorId;
                        }
                        else if (material.HasProperty(BaseColorId))
                        {
                            _tintPropertyIds[i][j] = BaseColorId;
                        }
                        else if (material.HasProperty(ColorId))
                        {
                            _tintPropertyIds[i][j] = ColorId;
                        }

                        if (_tintPropertyIds[i][j] != 0)
                        {
                            _baseColors[i][j] = material.GetColor(_tintPropertyIds[i][j]);
                        }

                        _hasEmission[i][j] = material.HasProperty(EmissionColorId);
                    }
                }

                if (_root != null && hasBounds)
                {
                    _lightObject = new GameObject("Carry placement glow");
                    _lightObject.transform.SetParent(_root, false);
                    _lightObject.transform.localPosition = _root.InverseTransformPoint(bounds.center);
                    _light = _lightObject.AddComponent<Light>();
                    _light.type = LightType.Point;
                    _light.range = Mathf.Clamp(bounds.extents.magnitude * 2.5f, 3f, 8f);
                    _light.intensity = 0.25f;
                    _light.shadows = LightShadows.None;
                }
            }

            public void SetColor(Color color)
            {
                if (_restored)
                {
                    return;
                }

                if (_light != null)
                {
                    _light.color = color;
                }

                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    // SpriteRenderer's visible tint is its color property (including BB+ sprite shaders),
                    // not necessarily a material color property. Avoid rewriting sprite material blocks.
                    if (_spriteRenderers[i] != null)
                    {
                        _spriteRenderers[i].color = Color.Lerp(
                            _originalSpriteColors[i], color, 0.22f);
                        continue;
                    }

                    if (_originalBlocks[i] == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < _originalBlocks[i].Length; j++)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, j);
                        int tintPropertyId = _tintPropertyIds[i][j];
                        if (tintPropertyId != 0)
                        {
                            // A restrained tint keeps the original image/texture visible instead of bleaching it white.
                            block.SetColor(tintPropertyId, Color.Lerp(_baseColors[i][j], color, 0.22f));
                        }

                        if (_hasEmission[i][j])
                        {
                            block.SetColor(EmissionColorId, color * 0.12f);
                        }

                        renderer.SetPropertyBlock(block, j);
                    }
                }
            }

            public void Restore()
            {
                if (_restored)
                {
                    return;
                }

                _restored = true;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null)
                    {
                        continue;
                    }

                    if (_spriteRenderers[i] != null)
                    {
                        _spriteRenderers[i].color = _originalSpriteColors[i];
                        continue;
                    }

                    if (_originalBlocks[i] == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < _originalBlocks[i].Length; j++)
                    {
                        _renderers[i].SetPropertyBlock(_originalBlocks[i][j], j);
                    }
                }

                if (_lightObject != null)
                {
                    UnityEngine.Object.Destroy(_lightObject);
                }
            }
        }

        private sealed class IgnoredCollisionPair
        {
            public Collider PlayerCollider;
            public Collider PropCollider;
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

        private sealed class CarryState
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public EnvironmentController Environment;
            public PlayerManager Player;
            public Cell OriginCell;
            public Transform OriginParent;
            public Vector3 OriginPosition;
            public float InitialY;
            public float OriginFloorY;
            public Vector3 CellOffset;
            public Vector3 LocalGrabPoint;
            public Vector3 PositionSmoothVelocity;
            public Quaternion CameraRotation;
            public Quaternion CarryRotation;
            public Quaternion TargetRotation;
            public int ManualRotationSteps;
            public Quaternion OriginalRotation;
            public CellCoverage Coverage;
            public CellCoverage RemovedHardCoverage;
            public CellCoverage RemovedSoftCoverage;
            public bool UsesCoverage;
            public bool IsLocker;
            public ParentKind ParentKind;
            public Renderer[] Renderers;
            public List<Renderer> RegisteredRenderers;
            public Dictionary<Cell, bool> PlacementWalkabilityCache;
            public List<IgnoredCollisionPair> IgnoredCollisionPairs;
            public RoomController PreferredRoom;
            public GlowState Glow;
            public float NextPlacementPreviewTime;
            public Rigidbody Body;
            public Collider[] PlacementColliders;
            public BoxCollider AddedCollider;
            public List<MeshCollider> NonConvexColliders;
            public bool AddedBody;
            public float OriginalMass;
            public float OriginalDrag;
            public float OriginalAngularDrag;
            public bool OriginalIsKinematic;
            public bool OriginalUseGravity;
            public bool OriginalDetectCollisions;
            public RigidbodyConstraints OriginalConstraints;
            public CollisionDetectionMode OriginalCollisionMode;
            public RigidbodyInterpolation OriginalInterpolation;
            public Vector3 OriginalVelocity;
            public Vector3 OriginalAngularVelocity;
            public float Age;
        }

        private sealed class RayHitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }

        [HarmonyPatch(typeof(PlayerClick), "Update")]
        private static class PlayerClickUpdatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerClick __instance)
            {
                g2 structuralRuntime = g2.Instance;
                if (structuralRuntime != null)
                {
                    try
                    {
                        if (structuralRuntime.HandlePlayerClick(__instance))
                        {
                            return false;
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[Rebuild the School] Structural PlayerClick input hook failed: " + exception);
                        if (structuralRuntime.IsDragging)
                        {
                            return false;
                        }
                    }
                }

                g runtime = Instance;
                if (runtime == null)
                {
                    return true;
                }

                try
                {
                    return !runtime.HandlePlayerClick(__instance);
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Rebuild the School] PlayerClick input hook failed: " + exception);
                    return runtime._held == null;
                }
            }
        }
    }
}
