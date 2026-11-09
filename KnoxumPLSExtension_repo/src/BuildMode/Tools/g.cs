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
    public sealed partial class g : MonoBehaviour
    {
        private const float PickupRange = 16f;
        private const float CarryOrbitRadius = 6f;
        // RMB throw and wheel-click kick (20 in the first version): a long, hard throw.
        private const float ThrowSpeed = 40f;
        private const float CarryRotationSpeed = 720f;
        // Gravity Chaos: how fast a carried prop turns over with its carrier (degrees per second).
        private const float CarryRollRate = 540f;
        private const float CarryMoveSmoothTime = 0.08f;
        private const float DoorWidthTolerance = 0.05f;
        private static readonly Color CarryHighlightColor = new Color(0.15f, 0.65f, 1f, 1f);
        private static readonly Color ValidPlacementColor = new Color(0.15f, 0.9f, 0.35f, 1f);
        private static readonly Color InvalidPlacementColor = new Color(1f, 0.12f, 0.08f, 1f);
        private const float GroundSupportNormal = 0.65f;
        private const float PlacementSearchFineStep = 0.25f;
        private const float PlacementSearchFineRadius = 2.5f;
        private const float PlacementSearchCoarseStep = 0.5f;
        private const float PlacementSearchRadius = 5f;
        // Aim resolution: ONE fresh query per frame feeds both the glow and the G key, so the glowing
        // prop is by construction the prop G picks. The grace only bridges one-frame gaps; it never
        // overrides a fresh target.
        private const float HighlightGraceTime = 0.12f;
        private const float SuppressReleaseTime = 0.3f;
        private const float AimCandidateReuseTime = 0.2f;
        private const float ProxyDepthEpsilon = 0.02f;
        private const float ProxyPruneDistance = PickupRange + 20f;
        // The aim-assist registry is built from the places where props live (see BeginProxyBuild), a little per frame,
        // once per floor and again every ProxyRefreshInterval seconds; positions are kept up to date a few per frame.
        private const float ProxyRefreshInterval = 60f;
        private const float ProxyScanBudgetMilliseconds = 1f;
        private const int ProxyMaxDepth = 4;
        private const int ProxyRoomDepth = 2;
        private const int ProxyPositionsPerFrame = 64;
        private const string GlowObjectName = "Carry placement glow";
        // Set to true to log every change of the aimed prop (off by default; G always logs its result).
        private static readonly bool VerboseAimLog = false;
        private const float PlacementPreviewRefreshInterval = 0.25f;
        private const float ReleaseTimeout = 8f;
        private const float WorldTileWidth = 10f;
        private const float DoorLookAhead = 7f;
        private const float DoorLateralTolerance = 3f;
        private const float DoorOpenDuration = 2.5f;
        private const float DoorSafeCloseDistance = 2f;
        private const float DoorContactPadding = 0.35f;
        private const float DoorCheckInterval = 0.12f;
        // Without a CoreGameManager the player is looked up in the scene, at most this often (seconds).
        private const float PlayerSearchInterval = 2f;
        // Thrown and dropped props fly on a KINEMATIC body that this class integrates itself (StepFlight):
        // a swept box against the cell grid (walls, void, closed doors and windows), the floor and the
        // ceiling, plus a box cast for loose props. Nothing in the guarantee depends on the Unity layer
        // matrix, on dynamic rigidbody collision or on continuous detection - those are what let the
        // earlier versions fly through BB+ walls and the floor.
        private const float FlightSkin = 0.02f;
        private const float FlightWallHalfThickness = 0.1f;
        private const float FlightCeilingHeight = 10f;
        private const float FlightGravityFallback = -9.81f;
        private const float FlightMaxSpeed = 80f;
        private const int FlightMaxBounces = 4;
        private const int FlightMaxDoorRetries = 6;
        private const float FlightBounceRestitution = 0.35f;
        private const float FlightBounceFriction = 0.85f;
        private const float FlightBounceMinNormalSpeed = 0.6f;
        private const float FlightScanStep = 0.1f;
        private const float FlightMinHalfExtent = 0.05f;
        private const float FlightGrowFactor = 1.3f;
        // The doors of a floor do not change once it is built; the list is renewed only when it is this old and asked for.
        private const float LevelDoorsInterval = 120f;
        private const float FlightParallelEpsilon = 1e-7f;
        // A thrown prop that hits a character pushes it with the call the Bully uses in Bully.Push:
        // Entity.AddForce(new Force(direction, speed, acceleration)). The Bully's own values are speed 5 and
        // acceleration -5. Here the speed grows with the prop's speed (acceleration = -speed, like the Bully)
        // and is multiplied by NpcPushStrength; the Bang sound (1st Prize's audBang) is played. With
        // acceleration = -speed the force lasts 1 s and moves the character speed / 2, so the strength
        // multiplies the speed and the distance alike (1 = the first version; 8 = eight times stronger).
        private const float NpcPushMinSpeed = 5f;
        private const float NpcPushMaxSpeed = 25f;
        private const float NpcPushPerPropSpeed = 0.5f;
        private const float NpcPushStrength = 8f;
        private const float NpcMinHitSpeed = 3f;
        private const float NpcHitBounce = 0.3f;
        // A thrown prop that touches a character breaks the player's "Bullying" rule (PlayerManager.RuleBreak, the
        // way drinking or entering a faculty room do): the Principal scolds the player when he sees it within
        // BullyingLinger seconds, for at least BullyingSensitivity seconds in total.
        private const string BullyingRule = "Bullying";
        private const float BullyingLinger = 1f;
        private const float BullyingSensitivity = 0.25f;
        // While a prop is carried, and for a moment after it was thrown or placed, the game does not see the
        // mouse: LMB (PlayerClick), RMB (ItemManager.UseItem) and the wheel (item selection).
        private const float GameMouseBlockAfterRelease = 0.3f;
        // Sounds (SoundObject asset names). Bang is also 1st Prize's audBang, Ben_Gum_Whoosh is also the audWhoosh of
        // the Gum. The whoosh loops while a thrown prop flies, Bang plays on every wall impact, LockDoorStop (quiet)
        // when the prop touches the floor.
        private const string BangSoundName = "Bang";
        private const string WhooshSoundName = "Ben_Gum_Whoosh";
        private const string FloorSoundName = "LockDoorStop";
        private const float FloorSoundVolume = 0.25f;
        private const float SoundMinDistance = 10f;
        private const float SoundMaxDistance = 70f;
        private const float ImpactSoundMinSpeed = 2.5f;
        private const float ImpactSoundInterval = 0.2f;
        private const float SoundRescanInterval = 30f;
        private static readonly float[] FlightFitScales = { 1f, 0.85f, 0.7f, 0.55f, 0.4f, 0.3f, 0.2f, 0.12f, 0.06f };

        private static readonly FieldInfo HardCoverageField = typeof(Cell).GetField(
            "hardCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SoftCoverageField = typeof(Cell).GetField(
            "softCoverage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly RaycastHit[] RayHits = new RaycastHit[128];
        private static readonly RaycastHit[] SweepHits = new RaycastHit[64];
        private static readonly Collider[] WalkabilityHits = new Collider[128];
        private static readonly List<Collider> NpcColliders = new List<Collider>();
        private static readonly RayHitComparer RayHitOrder = new RayHitComparer();
        private static readonly Dictionary<string, SoundObject> SoundsByName =
            new Dictionary<string, SoundObject>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SoundsReported = new HashSet<string>();
        private static float _nextSoundScanTime;
        private static float _nextSoundErrorLogTime;
        private static float _gameMouseBlockedUntil;

        private readonly List<CarryState> _airborne = new List<CarryState>();
        private CarryState _held;
        private PlayerManager _player;
        private Transform _hoveredRoot;
        private Candidate _hoveredCandidate;
        private Transform _suppressHoverRoot;
        private GlowState _hoverGlow;
        private int _lastPickupFrame = -1;
        private float _nextDoorCheckTime;
        private float _nextPlayerSearchTime;
        private float _lastAimTime;
        private int _aimFrame = -1;
        private PlayerManager _aimPlayer;
        private Candidate _aimResult;
        private Collider _aimBlocker;
        private float _aimBlockerDistance;
        private string _aimNote;
        private Candidate _aimCache;
        private float _aimCacheTime;
        private int _lastNoTargetLogFrame = -1;
        private float _nextHighlightErrorLogTime;
        private float _nextAimErrorLogTime;
        private float _nextSweepErrorLogTime;
        private readonly Dictionary<long, List<FlightDoor>> _flightDoors = new Dictionary<long, List<FlightDoor>>();
        private EnvironmentController _flightDoorsEnvironment;
        private int _flightDoorsVersion = -1;
        private Door[] _levelDoors = new Door[0];
        private Window[] _levelWindows = new Window[0];
        private Bounds[] _levelWindowBounds = new Bounds[0];
        private LockdownDoor[] _levelSuperDoors = new LockdownDoor[0];
        private EnvironmentController _levelDoorsEnvironment;
        private float _levelDoorsBuiltAt = -1000f;
        private int _levelDoorsVersion;
        private readonly List<ProxyProp> _proxies = new List<ProxyProp>();
        private readonly Dictionary<Transform, ProxyProp> _proxyByRoot = new Dictionary<Transform, ProxyProp>();
        private readonly HashSet<Transform> _proxyVisited = new HashSet<Transform>();
        private readonly List<ProxyContainer> _proxyContainers = new List<ProxyContainer>();
        private readonly Stack<ProxyNode> _proxyStack = new Stack<ProxyNode>();
        private List<ProxyProp> _proxyBuilding;
        private int _proxyContainerIndex;
        private int _proxyCursor;
        private EnvironmentController _proxyEnvironment;
        private float _proxyBuiltAt = -1000f;
        private bool _proxyErrorLogged;

        public static g Instance { get; private set; }

        /// <summary>
        /// True while a prop is carried and for GameMouseBlockAfterRelease seconds after it was thrown or placed.
        /// The game then ignores LMB (PlayerClick), RMB (ItemManager.UseItem) and the wheel (item selection).
        /// This mod reads the mouse itself, through UnityEngine.Input, and is not affected.
        /// </summary>
        internal static bool GameMouseBlocked
        {
            get
            {
                g props = Instance;
                return Time.unscaledTime < _gameMouseBlockedUntil || (props != null && props._held != null);
            }
        }

        internal static void BlockGameMouseBriefly()
        {
            _gameMouseBlockedUntil = Time.unscaledTime + GameMouseBlockAfterRelease;
        }

        /// <summary>
        /// Stops the game from using an item (RMB) or switching items (wheel) while the mouse is blocked. The
        /// plugin calls it once after PatchAll; a method that cannot be patched only leaves that input unblocked.
        /// </summary>
        internal static void TryPatchGameInput(Harmony harmony)
        {
            string[] methods = { "UseItem", "IncreaseItemSelection", "DecreaseItemSelection" };
            for (int i = 0; i < methods.Length; i++)
            {
                try
                {
                    MethodInfo target = AccessTools.Method(typeof(ItemManager), methods[i]);
                    MethodInfo skip = typeof(g).GetMethod(
                        nameof(SkipItemInputWhileBlocked), BindingFlags.Static | BindingFlags.NonPublic);
                    if (target == null || skip == null)
                    {
                        Debug.LogWarning("[Rebuild the School] ItemManager." + methods[i]
                            + " was not found; the game keeps reading that input while a prop is carried.");
                        continue;
                    }

                    harmony.Patch(target, prefix: new HarmonyMethod(skip));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Rebuild the School] ItemManager." + methods[i]
                        + " could not be blocked while a prop is carried: " + exception.Message);
                }
            }
        }

        private static bool SkipItemInputWhileBlocked()
        {
            return !GameMouseBlocked;
        }

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
            Debug.Log("[Rebuild the School] Carry runtime started. Controls: G, LMB, RMB, wheel click (kick), R + mouse wheel.");
        }

        private void Update()
        {
            RefreshPlayer();
            UpdateThrownProps();

            // A carried prop whose root or body is gone can neither follow the camera nor be released:
            // without this it would block every later pickup, drop and throw.
            if (_held != null && (!IsInCurrentLevel(_held) || _held.Body == null))
            {
                ReturnToOrigin(_held);
                _held = null;
            }

            // No floor to work in (menus, a floor that is still being built): nothing below has anything to do,
            // and the mod stays out of the way while the level loads.
            if (_player == null || _player.ec == null)
            {
                if (_hoverGlow != null || _hoveredRoot != null)
                {
                    ClearPickupHighlight();
                }

                return;
            }

            UpdateProxyRegistry(_player);
            if (_held != null && Time.timeScale > 0f)
            {
                ClearPickupHighlight();
                UpdateHeldManualRotation(_held);
                UpdatePlacementPreview(_held);
                UpdateDoorForCarriedProp();
            }
            else if (_held == null && Time.timeScale > 0f)
            {
                UpdatePickupHighlight();
            }
            else if (_held == null)
            {
                ClearPickupHighlight();
            }

            if (_held == null && Time.timeScale > 0f && PickupPressed())
            {
                // The prop comes from the same fresh query that drives the glow.
                TryPickupHighlighted(_player);
            }

            // Wheel-click kick, event squishers, characters that walk around props (g.Events.cs).
            UpdateEvents();
        }

        private void FixedUpdate()
        {
            StepAirborneProps();

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
            // Gravity Chaos: a prop carried turned over (feet up) is shown in RolledCarryRotation.
            Quaternion carryPose = RolledCarryRotation(_held);
            Vector3 anchorOffset = _held.CarryRoll == 0f
                ? _held.Root.TransformVector(_held.LocalGrabPoint)
                : carryPose * Vector3.Scale(_held.Root.lossyScale, _held.LocalGrabPoint);
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
            _held.Body.MoveRotation(carryPose);
            BreakWindowsTouchingProp(_held);
        }

        private void UpdateHeldFacing(CarryState state, Ray ray)
        {
            // Continuous camera movement turns the prop smoothly; the aimed side stays the one toward the player.
            Quaternion side = Quaternion.AngleAxis(state.SideYaw, Vector3.up);
            Quaternion desiredCameraRotation = CameraFacingRotation(
                -ray.direction, state.CameraRotation * Quaternion.Inverse(side)) * side;
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

            // Gravity Chaos: a player who stands on the ceiling carries the prop turned over, feet up, about the
            // line toward himself (the side that faces him stays toward him); the turn follows his own flip.
            // The prop takes the gravity of whoever carries it, so it is let go that way (FxOnRelease).
            Vector3 towardPlayer = Vector3.ProjectOnPlane(-ray.direction, Vector3.up);
            if (towardPlayer.sqrMagnitude > 0.001f)
            {
                state.CarryRollAxis = towardPlayer.normalized;
            }

            state.CarryRoll = Mathf.MoveTowards(
                state.CarryRoll,
                PlayerIsFlipped(state.Player) ? 180f : 0f,
                CarryRollRate * Time.fixedDeltaTime);
        }

        /// <summary>The pose the carried prop is shown in: the upright CarryRotation turned over by CarryRoll.</summary>
        private static Quaternion RolledCarryRotation(CarryState state)
        {
            if (Mathf.Abs(state.CarryRoll) < 0.05f)
            {
                return state.CarryRotation;
            }

            Vector3 axis = state.CarryRollAxis;
            if (axis.sqrMagnitude < 0.5f)
            {
                axis = Vector3.ProjectOnPlane(state.CarryRotation * Vector3.forward, Vector3.up);
            }

            if (axis.sqrMagnitude < 0.001f)
            {
                return state.CarryRotation;
            }

            return Quaternion.AngleAxis(state.CarryRoll, axis.normalized) * state.CarryRotation;
        }

        /// <summary>
        /// A prop carried turned over is let go in exactly that pose (not the one a step behind), about the same
        /// grab point, so the flight starts from what the player sees.
        /// </summary>
        private static void SnapCarryPose(CarryState state)
        {
            if (Mathf.Abs(state.CarryRoll) < 0.05f)
            {
                return;
            }

            Quaternion pose = RolledCarryRotation(state);
            Vector3 anchor = state.Root.TransformPoint(state.LocalGrabPoint);
            Vector3 position = anchor - pose * Vector3.Scale(state.Root.lossyScale, state.LocalGrabPoint);
            state.Body.position = position;
            state.Body.rotation = pose;
            state.Root.SetPositionAndRotation(position, pose);
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
            try
            {
                UpdatePickupHighlightCore();
            }
            catch (Exception exception)
            {
                // Runs every frame now, so one bad prop must not flood the log or break Update.
                if (Time.unscaledTime >= _nextHighlightErrorLogTime)
                {
                    _nextHighlightErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Rebuild the School] Pickup highlight failed: " + exception);
                }

                try
                {
                    ClearPickupHighlight();
                }
                catch (Exception)
                {
                    _hoverGlow = null;
                    _hoveredRoot = null;
                    _hoveredCandidate = null;
                }
            }
        }

        private void UpdatePickupHighlightCore()
        {
            // Fresh every frame: no polling interval, no hysteresis against the real target.
            Candidate aim = ResolveAimThisFrame(_player);
            float now = Time.unscaledTime;
            if (aim == null)
            {
                // The grace only bridges single-frame gaps (seams between meshes); it never
                // overrides a fresh target, which is handled below.
                if (_hoveredCandidate != null && _hoveredCandidate.Root != null
                    && now - _lastAimTime <= HighlightGraceTime)
                {
                    return;
                }

                ClearPickupHighlight();
                if (_suppressHoverRoot != null && now - _lastAimTime > SuppressReleaseTime)
                {
                    _suppressHoverRoot = null;
                }

                return;
            }

            _lastAimTime = now;
            if (_suppressHoverRoot != null)
            {
                if (_suppressHoverRoot == aim.Root)
                {
                    // Just placed: keep the prop un-tinted while the crosshair is still on it.
                    ClearPickupHighlight();
                    return;
                }

                // Once the player looks at something else, the placed prop may glow again later.
                _suppressHoverRoot = null;
            }

            if (_hoveredRoot != aim.Root)
            {
                ClearPickupHighlight();
                _hoveredRoot = aim.Root;
                _hoverGlow = new GlowState(aim.Root, aim.Renderers);
                if (VerboseAimLog)
                {
                    Debug.Log("[Rebuild the School] Aim -> " + DescribePath(aim.Root) + " ("
                        + aim.AimSource + ", " + aim.AimDistance.ToString("0.0") + " units).");
                }
            }

            _hoveredCandidate = aim;
            if (_hoverGlow != null)
            {
                _hoverGlow.SetColor(ValidPlacementColor);
            }
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

        /// <summary>
        /// The single G entry point shared by g.Update and the PlayerClick hook. It runs the same
        /// per-frame aim query as the glow. Returns true when a prop is under the crosshair (picked, or
        /// refused with a logged reason); false when there is no prop target at all.
        /// </summary>
        internal bool TryPickupHighlighted(PlayerManager player)
        {
            if (_held != null || player == null)
            {
                return false;
            }

            // The fresh query wins. The glow is only a fallback for the instant the crosshair slips off
            // the prop that was glowing a moment ago, and it is re-validated before use.
            Candidate target = ResolveAimThisFrame(player);
            if (target == null && _hoveredCandidate != null && _hoveredCandidate.Root != null
                && Time.unscaledTime - _lastAimTime <= HighlightGraceTime
                && CanBeginCarry(_hoveredCandidate, player))
            {
                target = _hoveredCandidate;
            }

            if (target == null)
            {
                if (_lastNoTargetLogFrame != Time.frameCount)
                {
                    _lastNoTargetLogFrame = Time.frameCount;
                    Debug.Log("[Rebuild the School] G: no prop under the crosshair (" + DescribeAimInfo() + ").");
                }

                return false;
            }

            TryPickup(player, target);
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

                // Same fresh aim query as g.Update. With no prop under the crosshair this returns
                // false and the game's own click handling runs untouched.
                if (!TryPickupHighlighted(clicker.pm))
                {
                    return false;
                }

                clicker.clickedThisFrame = null;
                return true;
            }

            clicker.clickedThisFrame = null;
            if (Time.timeScale <= 0f)
            {
                return true;
            }

            // Prefer LMB when both buttons are pressed in the same frame: LMB drops, RMB (or the wheel
            // click, the kick) throws.
            if (PlacePressed())
            {
                DropHeld();
            }
            else if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
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

            CoreGameManager gameManager = null;
            try
            {
                gameManager = Singleton<CoreGameManager>.Instance;
                if (gameManager != null)
                {
                    _player = gameManager.GetPlayer(0);
                }
            }
            catch
            {
                _player = null;
            }

            // The player has no environment (ec) until the floor is built: EnvironmentController.AssignPlayers
            // runs after LevelBuilder.Generate. Searching the scene for a PlayerClick in every frame of that time
            // never succeeds and costs more with every object the builder has made, which is what made big floors
            // load for ages. The scene is only searched when there is no CoreGameManager to ask, and only now
            // and then.
            if (gameManager == null && (_player == null || _player.ec == null)
                && Time.unscaledTime >= _nextPlayerSearchTime)
            {
                _nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;
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

        /// <summary>
        /// The world box of a renderer's mesh, worked out from its transform: right whether or not the renderer is
        /// enabled at the moment (the game's culling switches the renderers of far chunks off, and the bounds of a
        /// disabled renderer are not to be trusted).
        /// </summary>
        private static bool TryGetMeshWorldBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = new Bounds();
            Bounds local;
            if (renderer == null || !TryGetLocalMeshBounds(renderer, out local))
            {
                return false;
            }

            Transform transform = renderer.transform;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + new Vector3(
                    (i & 1) == 0 ? -local.extents.x : local.extents.x,
                    (i & 2) == 0 ? -local.extents.y : local.extents.y,
                    (i & 4) == 0 ? -local.extents.z : local.extents.z);
                Vector3 world = transform.TransformPoint(corner);
                if (i == 0)
                {
                    bounds = new Bounds(world, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }

            return true;
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

        private void TryPickup(PlayerManager player, Candidate candidate)
        {
            if (_lastPickupFrame == Time.frameCount)
            {
                return;
            }

            _lastPickupFrame = Time.frameCount;
            try
            {
                if (player == null || player.ec == null || candidate == null || candidate.Root == null)
                {
                    Debug.LogWarning("[Rebuild the School] Pickup blocked: player, environment or target is not ready.");
                    return;
                }

                _player = player;
                Ray ray;
                if (!TryGetCameraRay(player, out ray))
                {
                    Debug.LogWarning("[Rebuild the School] Pickup blocked: camera ray is unavailable.");
                    return;
                }

                string targetName = DescribePath(candidate.Root);
                if (!CanBeginCarry(candidate, player))
                {
                    Debug.LogWarning("[Rebuild the School] Selected prop is not currently movable: " + targetName);
                    return;
                }

                // Restore hover blocks before constructing the carry glow; otherwise the carry
                // state would capture the temporary hover tint as its supposed original state.
                ClearPickupHighlight();
                CarryState state;
                if (!BeginCarry(candidate, player, ray, out state))
                {
                    Debug.LogWarning("[Rebuild the School] Pickup setup rejected: " + targetName + ".");
                    return;
                }

                _held = state;
                InvalidateAim();
                Debug.Log("[Rebuild the School] G picked up " + targetName + " ("
                    + (candidate.AimSource ?? "aim") + ", " + candidate.AimDistance.ToString("0.0")
                    + " units); visual center locked to the camera orbit.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Rebuild the School] Pickup failed: " + exception);
            }
        }

        // ------------------------------------------------------------------------------------
        // Aim resolution. Everything that decides "which prop is under the crosshair" lives here.
        // ------------------------------------------------------------------------------------

        private void InvalidateAim()
        {
            _aimFrame = -1;
            _aimResult = null;
            _aimCache = null;
        }

        private string DescribeAimInfo()
        {
            if (_aimNote != null)
            {
                return _aimNote;
            }

            if (_aimBlocker == null)
            {
                return "nothing solid within " + PickupRange.ToString("0") + " units";
            }

            string text = "blocked by " + DescribePath(_aimBlocker.transform) + " (layer "
                + _aimBlocker.gameObject.layer + ") at " + _aimBlockerDistance.ToString("0.0");
            EnvironmentObject environmentObject = _aimBlocker.GetComponentInParent<EnvironmentObject>();
            Transform propRoot = environmentObject != null
                ? environmentObject.transform
                : FindSupportedPropRoot(_aimBlocker.transform);
            if (propRoot != null)
            {
                text += "; it looks like a prop (" + DescribePath(propRoot) + ") but cannot be carried: "
                    + DescribeCarryRejection(propRoot, environmentObject, _aimPlayer);
            }

            return text;
        }

        /// <summary>
        /// Diagnostic only (runs when G found nothing to pick): names the first check that makes a
        /// prop-looking object uncarriable, mirroring CanBeginCarry / TryBuildCandidate.
        /// </summary>
        private string DescribeCarryRejection(
            Transform root, EnvironmentObject environmentObject, PlayerManager player)
        {
            if (root == null || player == null || player.ec == null)
            {
                return "player or object missing";
            }

            string forbidden = ForbiddenPickupReason(root);
            if (forbidden != null)
            {
                return "excluded (" + forbidden + ")";
            }

            if (IsAirborneRoot(root))
            {
                return "still in flight";
            }

            if (!root.gameObject.activeInHierarchy)
            {
                return "inactive";
            }

            if (environmentObject != null)
            {
                Cell environmentCell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out environmentCell))
                {
                    return "environment object is not carriable (other level, door/tile/NPC/activity parent, or no cell)";
                }
            }
            else if (IsForbiddenPropHierarchy(root))
            {
                return "it sits inside a door, tile object, NPC, activity or environment object";
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds;
            if (renderers.Length == 0 || !CombinedBounds(renderers, out bounds))
            {
                return "no renderers with usable bounds";
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null && bodies[i].transform != root)
                {
                    return "a child has its own Rigidbody (" + DescribePath(bodies[i].transform) + ")";
                }
            }

            Cell cell;
            if (!TryGetCell(player.ec, root.position, out cell)
                && !TryGetCell(player.ec, bounds.center, out cell))
            {
                return "it is not inside any cell";
            }

            return "parent type, cell or coverage data do not allow carrying";
        }

        /// <summary>
        /// What the crosshair is on, computed at most once per frame and shared by the glow and G.
        /// Rule: the first solid thing on the camera's center ray decides. A prop must really be hit by
        /// the ray; a wall, door, window, NPC or immovable prop in front blocks it, so nothing is ever
        /// picked through geometry. Props the ray does not touch physically (no solid collider at all, or
        /// gaps in the collider) are matched by the boxes of their meshes as aim assist: collider-less props
        /// compete with a physical hit by distance, props that do have colliders are only assisted when the
        /// ray hit no movable prop. In every case the prop must be in front of the first solid hit.
        /// </summary>
        private Candidate ResolveAimThisFrame(PlayerManager player)
        {
            if (_aimFrame == Time.frameCount && _aimPlayer == player)
            {
                return _aimResult;
            }

            _aimFrame = Time.frameCount;
            _aimPlayer = player;
            _aimResult = null;
            _aimBlocker = null;
            _aimBlockerDistance = 0f;
            _aimNote = null;
            if (player == null || player.ec == null || _held != null)
            {
                _aimNote = "not available right now";
                return null;
            }

            Ray ray;
            if (!TryGetCameraRay(player, out ray))
            {
                _aimNote = "camera ray unavailable";
                return null;
            }

            try
            {
                _aimResult = ResolveAim(player, ray, out _aimBlocker, out _aimBlockerDistance);
            }
            catch (Exception exception)
            {
                _aimResult = null;
                _aimNote = "aim query failed: " + exception.Message;
                if (Time.unscaledTime >= _nextAimErrorLogTime)
                {
                    _nextAimErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Rebuild the School] Aim query failed: " + exception);
                }
            }

            return _aimResult;
        }

        private Candidate ResolveAim(
            PlayerManager player, Ray ray, out Collider blocker, out float blockerDistance)
        {
            blocker = null;
            blockerDistance = 0f;
            int count = Physics.RaycastNonAlloc(
                ray, RayHits, PickupRange, ~0, QueryTriggerInteraction.Ignore);
            if (count > 1)
            {
                Array.Sort(RayHits, 0, count, RayHitOrder);
            }

            Candidate physical = null;
            float solidDistance = PickupRange;
            for (int i = 0; i < count; i++)
            {
                Collider collider = RayHits[i].collider;
                if (collider == null || IsIgnoredForAim(collider, player))
                {
                    continue;
                }

                // The first solid hit decides: either it belongs to a movable prop (that prop is the
                // target) or it blocks everything behind it. No second chances further along the ray.
                solidDistance = RayHits[i].distance;
                if (TryCandidateFromCollider(player, collider, out physical))
                {
                    physical.AimDistance = solidDistance;
                    physical.AimSource = "collider";
                    physical.AimNormal = RayHits[i].normal;
                }
                else
                {
                    blocker = collider;
                    blockerDistance = solidDistance;
                }

                break;
            }

            Candidate assisted;
            float assistedDistance;
            if (TryFindAssistProp(player, ray, solidDistance - ProxyDepthEpsilon, physical != null,
                out assisted, out assistedDistance))
            {
                assisted.AimDistance = assistedDistance;
                assisted.AimSource = "mesh box";
                assisted.AimNormal = Vector3.zero;
                blocker = null;
                return assisted;
            }

            return physical;
        }

        private bool IsIgnoredForAim(Collider collider, PlayerManager player)
        {
            Transform hit = collider.transform;
            if (player != null && (hit == player.transform || hit.IsChildOf(player.transform)))
            {
                return true;
            }

            // Props that were just dropped or thrown are neither targets nor blockers while airborne.
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state != null && state.Root != null && !FxResumable(state)
                    && (hit == state.Root || hit.IsChildOf(state.Root)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAirborneRoot(Transform root)
        {
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state != null && state.Root == root)
                {
                    // A prop pinned to the ceiling or riding a student is not flying: it can be picked up again.
                    return !FxResumable(state);
                }
            }

            return false;
        }

        private bool TryCandidateFromCollider(PlayerManager player, Collider collider, out Candidate candidate)
        {
            candidate = null;
            if (player == null || player.ec == null || collider == null)
            {
                return false;
            }

            EnvironmentObject environmentObject = collider.GetComponentInParent<EnvironmentObject>();
            Transform root = environmentObject != null
                ? environmentObject.transform
                : FindSupportedPropRoot(collider.transform);
            return root != null && TryBuildCandidate(player, root, environmentObject, out candidate);
        }

        private bool TryBuildCandidate(
            PlayerManager player, Transform root, EnvironmentObject environmentObject, out Candidate candidate)
        {
            candidate = null;
            if (player == null || player.ec == null || root == null || IsAirborneRoot(root))
            {
                return false;
            }

            // The same prop stays the target for many frames; do not rebuild its renderer arrays
            // and re-run every component query each frame.
            Candidate cached = _aimCache;
            if (cached != null && cached.Root == root && cached.Root.gameObject.activeInHierarchy
                && Time.unscaledTime - _aimCacheTime < AimCandidateReuseTime)
            {
                candidate = cached;
                return true;
            }

            Candidate built;
            if (environmentObject != null)
            {
                Cell cell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out cell))
                {
                    return false;
                }

                built = new Candidate
                {
                    Root = environmentObject.transform,
                    EnvironmentObject = environmentObject,
                    Renderers = environmentObject.GetComponentsInChildren<Renderer>(true),
                    Cell = cell,
                    ParentKind = ParentKind.RoomObjects
                };
            }
            else
            {
                if (IsForbiddenPropHierarchy(root))
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

                built = new Candidate
                {
                    Root = root,
                    EnvironmentObject = null,
                    Renderers = renderers,
                    Cell = propCell,
                    ParentKind = IsChildOfCellObjectBase(root, propCell)
                        ? ParentKind.CellObjectBase
                        : ParentKind.Room
                };
            }

            if (!CanBeginCarry(built, player))
            {
                return false;
            }

            candidate = built;
            _aimCache = built;
            _aimCacheTime = Time.unscaledTime;
            return true;
        }

        // ------------------------------------------------------------------------------------
        // Aim assist. A physics ray can miss a prop that is clearly under the crosshair: some props have
        // no solid collider at all, others have meshy colliders with gaps (an open chair back). Those are
        // matched by the oriented bounding box of each of their meshes (never the inflated world AABB),
        // only when they are nearer than the first solid hit, from a registry that is built a little per
        // frame instead of scanning every renderer in the scene on every query.
        // ------------------------------------------------------------------------------------

        private bool TryFindAssistProp(
            PlayerManager player, Ray ray, float maxDistance, bool physicalFound,
            out Candidate candidate, out float distance)
        {
            candidate = null;
            distance = float.MaxValue;
            if (maxDistance <= 0f || _proxies.Count == 0)
            {
                return false;
            }

            float pruneSqr = ProxyPruneDistance * ProxyPruneDistance;
            ProxyProp best = null;
            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                // Physics is authoritative for props that have colliders: when the ray already hit a
                // movable prop, only collider-less props (which physics cannot see) may still beat it.
                if (physicalFound && proxy.HasSolidCollider)
                {
                    continue;
                }

                // The far props are dropped on the position kept in the registry, before anything is asked of Unity.
                if ((proxy.Position - ray.origin).sqrMagnitude > pruneSqr)
                {
                    continue;
                }

                if (proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float proxyDistance = 0f;
                if (!RayHitsProxy(proxy, ray, out proxyDistance)
                    || proxyDistance > maxDistance || proxyDistance >= distance)
                {
                    continue;
                }

                best = proxy;
                distance = proxyDistance;
            }

            return best != null
                && TryBuildCandidate(player, best.Root, best.EnvironmentObject, out candidate);
        }

        /// <summary>
        /// Builds the aim-assist registry from the places where the level builder puts props: the objects of every
        /// room (EnvironmentObject), the base of every cell that has one (lockers, vending machines) and, shallow,
        /// the room itself. It walks those trees a little each frame (ProxyScanBudgetMilliseconds) instead of
        /// asking Unity for every renderer in the scene, which on a floor with many objects took tens of
        /// thousands of lookups, again and again. Once built, the positions are refreshed a few per frame.
        /// </summary>
        private void UpdateProxyRegistry(PlayerManager player)
        {
            if (_proxyEnvironment != player.ec)
            {
                _proxyEnvironment = player.ec;
                _proxies.Clear();
                _proxyByRoot.Clear();
                _proxyBuilding = null;
                _proxyStack.Clear();
                _proxyCursor = 0;
                _proxyBuiltAt = -ProxyRefreshInterval - 1f;
            }

            float now = Time.unscaledTime;
            if (_proxyBuilding == null)
            {
                RefreshProxyPositions();
                if (now - _proxyBuiltAt < ProxyRefreshInterval)
                {
                    return;
                }

                BeginProxyBuild(player.ec);
            }

            float start = Time.realtimeSinceStartup;
            while (_proxyStack.Count > 0 || _proxyContainerIndex < _proxyContainers.Count)
            {
                if (_proxyStack.Count == 0)
                {
                    ProxyContainer container = _proxyContainers[_proxyContainerIndex];
                    _proxyContainerIndex++;
                    PushProxyChildren(container.Root, 1, container.MaxDepth);
                    continue;
                }

                ScanProxyNode(player, _proxyStack.Pop());
                if ((Time.realtimeSinceStartup - start) * 1000f >= ProxyScanBudgetMilliseconds)
                {
                    return;
                }
            }

            _proxies.Clear();
            _proxies.AddRange(_proxyBuilding);
            _proxyByRoot.Clear();
            for (int i = 0; i < _proxies.Count; i++)
            {
                _proxyByRoot[_proxies[i].Root] = _proxies[i];
            }

            _proxyBuilding = null;
            _proxyVisited.Clear();
            _proxyContainers.Clear();
            _proxyBuiltAt = now;
        }

        private void BeginProxyBuild(EnvironmentController environment)
        {
            _proxyBuilding = new List<ProxyProp>();
            _proxyVisited.Clear();
            _proxyStack.Clear();
            _proxyContainers.Clear();
            _proxyContainerIndex = 0;
            List<RoomController> rooms = environment.rooms;
            for (int i = 0; rooms != null && i < rooms.Count; i++)
            {
                RoomController room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                if (room.objectObject != null)
                {
                    AddProxyContainer(room.objectObject.transform, ProxyMaxDepth);
                }

                AddProxyContainer(room.transform, ProxyRoomDepth);
            }

            Cell[,] cells = environment.cells;
            if (cells == null)
            {
                return;
            }

            int width = cells.GetLength(0);
            int length = cells.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    // ObjectBase creates itself when asked: only the cells that already have one are looked at.
                    Cell cell = cells[x, z];
                    if (cell != null && cell.HasObjectBase)
                    {
                        AddProxyContainer(cell.ObjectBase, ProxyMaxDepth);
                    }
                }
            }
        }

        private void AddProxyContainer(Transform root, int maxDepth)
        {
            if (root == null)
            {
                return;
            }

            ProxyContainer container;
            container.Root = root;
            container.MaxDepth = maxDepth;
            _proxyContainers.Add(container);
        }

        private void PushProxyChildren(Transform parent, int depth, int maxDepth)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                ProxyNode item;
                item.Node = parent.GetChild(i);
                item.Depth = depth;
                item.MaxDepth = maxDepth;
                _proxyStack.Push(item);
            }
        }

        /// <summary>
        /// One transform of the walk. An EnvironmentObject is a prop root; so is the outermost transform with a
        /// supported name (FindSupportedPropRoot, from the top down); tiles, doors, characters and activities end
        /// the branch, as they end the climb of FindSupportedPropRoot.
        /// </summary>
        private void ScanProxyNode(PlayerManager player, ProxyNode item)
        {
            Transform node = item.Node;
            if (node == null)
            {
                return;
            }

            try
            {
                EnvironmentObject environmentObject = node.GetComponent<EnvironmentObject>();
                if (environmentObject != null)
                {
                    AddProxyRoot(player, node, environmentObject);
                    return;
                }

                if (node.GetComponent<TileBasedObject>() != null || node.GetComponent<RoomController>() != null
                    || node.GetComponent<NPC>() != null || node.GetComponent<Activity>() != null
                    || node.GetComponent<Door>() != null)
                {
                    return;
                }

                if (IsSupportedPropName(node.name))
                {
                    AddProxyRoot(player, node, null);
                    return;
                }

                if (item.Depth < item.MaxDepth)
                {
                    PushProxyChildren(node, item.Depth + 1, item.MaxDepth);
                }
            }
            catch (Exception exception)
            {
                if (!_proxyErrorLogged)
                {
                    _proxyErrorLogged = true;
                    Debug.LogWarning("[Rebuild the School] Prop scan skipped an object: " + exception.Message);
                }
            }
        }

        private void AddProxyRoot(PlayerManager player, Transform root, EnvironmentObject environmentObject)
        {
            if (!_proxyVisited.Add(root))
            {
                return;
            }

            Cell cell;
            bool eligible = environmentObject != null
                ? CanPickupEnvironmentObject(environmentObject, player, out cell)
                : !IsForbiddenPropHierarchy(root);
            if (!eligible)
            {
                return;
            }

            ProxyProp proxy = BuildProxy(root, environmentObject, HasSolidCollider(root));
            if (proxy.Renderers.Length == 0)
            {
                return;
            }

            proxy.Position = root.position;
            _proxyBuilding.Add(proxy);
        }

        /// <summary>The positions of a few props per frame, round and round (a moved prop also reports itself, NoteProxyMoved).</summary>
        private void RefreshProxyPositions()
        {
            int count = _proxies.Count;
            int steps = Mathf.Min(count, ProxyPositionsPerFrame);
            for (int i = 0; i < steps; i++)
            {
                if (_proxyCursor >= count)
                {
                    _proxyCursor = 0;
                }

                ProxyProp proxy = _proxies[_proxyCursor];
                _proxyCursor++;
                if (proxy.Root != null)
                {
                    Vector3 position = proxy.Root.position;
                    if ((position - proxy.Position).sqrMagnitude > 0.0001f)
                    {
                        proxy.Position = position;
                        proxy.HasBox = false;
                    }
                }
            }
        }

        /// <summary>A prop of the registry that was put somewhere (landed, placed, sent home) says where it is now.</summary>
        private void NoteProxyMoved(Transform root)
        {
            ProxyProp proxy;
            if (root != null && _proxyByRoot.TryGetValue(root, out proxy) && proxy.Root != null)
            {
                proxy.Position = root.position;
                proxy.HasBox = false;
            }
        }

        /// <summary>
        /// The world box of a registry prop, worked out from its meshes (so a prop whose renderers are culled at the
        /// moment has one too) and kept until the prop moves.
        /// </summary>
        private static bool ProxyBox(ProxyProp proxy, out Bounds bounds)
        {
            if (proxy.HasBox)
            {
                bounds = proxy.Box;
                return true;
            }

            bounds = new Bounds();
            bool found = false;
            Renderer[] renderers = proxy.Renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds one = new Bounds();
                bool has = proxy.HasLocalBounds[i] && TryGetMeshWorldBounds(renderer, out one);
                if (!has)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    one = renderer.bounds;
                }

                if (found)
                {
                    bounds.Encapsulate(one);
                }
                else
                {
                    bounds = one;
                    found = true;
                }
            }

            if (found)
            {
                proxy.Box = bounds;
                proxy.HasBox = true;
            }

            return found;
        }

        private static bool HasSolidCollider(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static ProxyProp BuildProxy(
            Transform root, EnvironmentObject environmentObject, bool hasSolidCollider)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            ProxyProp proxy = new ProxyProp
            {
                Root = root,
                EnvironmentObject = environmentObject,
                HasSolidCollider = hasSolidCollider,
                Renderers = renderers,
                LocalBounds = new Bounds[renderers.Length],
                HasLocalBounds = new bool[renderers.Length]
            };
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local;
                if (TryGetLocalMeshBounds(renderers[i], out local))
                {
                    proxy.LocalBounds[i] = local;
                    proxy.HasLocalBounds[i] = true;
                }
            }

            return proxy;
        }

        private static bool TryGetLocalMeshBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = new Bounds();
            if (renderer == null)
            {
                return false;
            }

            try
            {
                SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    if (spriteRenderer.sprite == null)
                    {
                        return false;
                    }

                    bounds = spriteRenderer.sprite.bounds;
                    return true;
                }

                MeshRenderer meshRenderer = renderer as MeshRenderer;
                if (meshRenderer != null)
                {
                    MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        bounds = filter.sharedMesh.bounds;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to the renderer's world bounds when a mesh cannot report local bounds.
            }

            return false;
        }

        private static bool RayHitsProxy(ProxyProp proxy, Ray ray, out float distance)
        {
            distance = float.MaxValue;
            bool hit = false;
            for (int i = 0; i < proxy.Renderers.Length; i++)
            {
                Renderer renderer = proxy.Renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float rendererDistance;
                bool intersects = proxy.HasLocalBounds[i]
                    ? RayHitsLocalBox(renderer.transform, proxy.LocalBounds[i], ray, out rendererDistance)
                    : renderer.bounds.IntersectRay(ray, out rendererDistance);
                if (intersects && rendererDistance >= 0f && rendererDistance < distance)
                {
                    distance = rendererDistance;
                    hit = true;
                }
            }

            return hit;
        }

        /// <summary>
        /// Ray against a mesh's local-space box. The ray is moved into the transform's local space
        /// (InverseTransformVector keeps the scale, so the parameter t stays a world distance), then a
        /// standard slab test is run. Unlike renderer.bounds this stays tight for rotated props.
        /// </summary>
        private static bool RayHitsLocalBox(Transform transform, Bounds box, Ray ray, out float distance)
        {
            distance = 0f;
            Vector3 origin = transform.InverseTransformPoint(ray.origin);
            Vector3 direction = transform.InverseTransformVector(ray.direction);
            Vector3 min = box.min;
            Vector3 max = box.max;
            float tMin = 0f;
            float tMax = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis];
                float d = direction[axis];
                float lo = min[axis];
                float hi = max[axis];
                if (Mathf.Abs(d) < 1e-9f)
                {
                    if (o < lo || o > hi)
                    {
                        return false;
                    }

                    continue;
                }

                float t1 = (lo - o) / d;
                float t2 = (hi - o) / d;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                }

                if (t2 < tMax)
                {
                    tMax = t2;
                }

                if (tMin > tMax)
                {
                    return false;
                }
            }

            distance = tMin;
            return true;
        }

        private static string DescribePath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            string path = transform.name;
            Transform parent = transform.parent;
            for (int depth = 0; parent != null && depth < 3; depth++)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
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

        /// <summary>
        /// Takes the prop out of the world. keepRotation (the kick) leaves it exactly as it stands: it is not
        /// turned toward the camera, so its bounds, footprint and landing orientation stay the ones it had.
        /// </summary>
        private bool BeginCarry(
            Candidate candidate, PlayerManager player, Ray ray, out CarryState state, bool keepRotation = false)
        {
            state = null;
            if (candidate == null || candidate.Root == null || IsForbiddenPickupRoot(candidate.Root)
                || candidate.Cell == null || candidate.Renderers == null || candidate.Renderers.Length == 0
                || DestinationParent(candidate.ParentKind, candidate.Cell) == null)
            {
                return false;
            }

            // A prop pinned to the ceiling (or riding a student) is a released prop with a state of its own:
            // carrying it again goes on with that state (g.Events.cs).
            bool resumable;
            CarryState resumed = TryResumeCarry(candidate, player, ray, keepRotation, out resumable);
            if (resumable)
            {
                state = resumed;
                return resumed != null;
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
            // The side under the crosshair turns toward the player (the front of a locker or a vending machine
            // when it is aimed at) and stays toward the player while the prop is carried.
            float sideYaw = 0f;
            Quaternion carryRotation = keepRotation
                ? originalRotation
                : AimedSideRotation(originalRotation, -ray.direction, candidate.AimNormal, cardinalFallback, out sideYaw);
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
            state.SideYaw = sideYaw;
            state.LocalGrabPoint = Vector3.zero;
            state.Coverage = coverage;
            state.UsesCoverage = usesCoverage;
            state.IsLocker = isLocker;
            state.RemovedHardCoverage = removedHard;
            state.RemovedSoftCoverage = removedSoft;
            state.ParentKind = candidate.ParentKind;
            // A locker is shown with a back from now on (the red and blue ones have none).
            state.Renderers = isLocker
                ? EnsureLockerBack(candidate.Cell, candidate.Root, candidate.Renderers)
                : candidate.Renderers;
            state.RegisteredRenderers = RegisteredRenderers(player.ec, state.Renderers);
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

                RemoveRendererReferencesEverywhere(player.ec, state.RegisteredRenderers);
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

        private void ReleaseHeld(bool throwForward, bool kick = false)
        {
            if (_held == null)
            {
                return;
            }

            if (_held.Root == null || _held.Body == null)
            {
                // Nothing left to release; leaving it set would block every later pickup, drop and throw.
                ReturnToOrigin(_held);
                _held = null;
                return;
            }

            CarryState state = _held;
            if (!kick)
            {
                BlockGameMouseBriefly();
            }

            try
            {
                Ray ray;
                if (!TryGetCameraRay(state.Player, out ray))
                {
                    ray = new Ray(state.Player.transform.position, state.Player.transform.forward);
                }

                state.Root.SetParent(state.Environment.transform, true);
                RestoreVisualGlow(state);
                _suppressHoverRoot = state.Root;
                // The body stays kinematic and keeps ignoring the player (both set up while carried); the
                // landing (RestoreBody) gives the prop its original body settings back.
                // A kick starts where the prop stands (atRest); a carried prop starts at the camera.
                if (!kick)
                {
                    SnapCarryPose(state);
                }

                BeginFlight(state, ray, throwForward, kick);
                // The event state comes first: it needs to know where the prop was (on the ceiling or not), and the
                // kick needs the way the prop is pulled.
                bool restedOnCeiling = state.Fx != null && state.Fx.Pinned;
                FxOnRelease(state, kick);
                if (kick)
                {
                    // A prop would land again in the first step: the kick goes a little away from its surface.
                    state.FlightVelocity = KickVelocity(state, ray, restedOnCeiling);
                }

                state.Age = 0f;
                state.Thrown = throwForward;
                state.WallHits = 0;
                state.PushedNpcs = null;
                state.NextImpactSoundTime = 0f;
                _lastReleaseFrame = Time.frameCount;
                _airborne.Add(state);
                if (throwForward)
                {
                    StartFlightSound(state);
                }

                _held = null;
                InvalidateAim();
                Debug.Log(kick
                    ? "[Rebuild the School] Prop kicked (wheel click); it flies like a thrown one."
                    : throwForward
                        ? "[Rebuild the School] Prop thrown; it flies on a swept path that cannot pass walls, doors or the floor."
                        : "[Rebuild the School] Prop dropped; it falls on a swept path and settles on contact.");
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
                if (FxKeepsAirborne(state))
                {
                    // On the ceiling, on a student's head or in a whirlpool: not a lost prop.
                    state.Age = 0f;
                }

                BreakWindowsTouchingProp(state);

                if (state.Landed)
                {
                    // StepFlight put the prop on the floor (or on a support); settle it into the world.
                    _airborne.RemoveAt(i);
                    FxBeforeSettle(state);
                    try
                    {
                        Cell supportCell;
                        if (FinishFlight(state, out supportCell))
                        {
                            SettleThrown(state, supportCell);
                        }
                        else
                        {
                            Debug.LogWarning("[Rebuild the School] A prop came to rest outside every room; it went back to where it came from.");
                            ReturnToOrigin(state);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[Rebuild the School] Settling a landed prop failed; it went back to where it came from: " + exception);
                        ReturnToOrigin(state);
                    }
                }
                else if (state.Age >= ReleaseTimeout)
                {
                    Debug.LogWarning("[Rebuild the School] A prop was still airborne after "
                        + ReleaseTimeout + " s; it went back to where it came from.");
                    ReturnToOrigin(state);
                    _airborne.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------------------------
        // Flight of released props (RMB throw and LMB drop)
        // ------------------------------------------------------------------------------------------

        private enum FlightHitKind
        {
            Wall,
            Door,
            Floor,
            Ceiling,
            Collider,
            Support
        }

        private enum FlightEdgeKind
        {
            None,
            Wall,
            Door
        }

        private struct FlightHit
        {
            public float T;
            public Vector3 Normal;
            public FlightHitKind Kind;
            public bool Inside;
            public FlightDoor Door;
            public Collider Collider;
            public int EdgeX;
            public int EdgeZ;
            public int EdgeAxis;
        }

        private sealed class FlightDoor
        {
            public Door Door;
            public Collider[] Colliders;
        }

        /// <summary>
        /// Starts the flight of a released prop. The body stays kinematic, exactly as while it was carried,
        /// and StepFlight moves it from FixedUpdate. The start is resolved against the walls first: the
        /// camera is in free space, so the prop never starts on the far side of a wall, and a prop that
        /// does not fit between the camera and a wall gets a smaller collision box until it has room.
        /// atRest (the kick) skips that: the prop stands where the level or the player put it, a place that
        /// is valid by definition, so it keeps its place and its full-size box. The wall sweeps never move
        /// a box that already touches a wall deeper into it, so it can only leave.
        /// </summary>
        private void BeginFlight(CarryState state, Ray ray, bool throwForward, bool atRest = false)
        {
            Rigidbody body = state.Body;
            Bounds bounds;
            if (!CombinedBounds(state.Renderers, out bounds))
            {
                throw new InvalidOperationException("The released prop has no active renderers.");
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            body.constraints = RigidbodyConstraints.None;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Cell floorCell;
            state.FlightFloorY = TryGetCell(state.Environment, ray.origin, out floorCell)
                ? floorCell.FloorWorldPosition.y
                : state.OriginFloorY;
            state.FlightCenterOffset = bounds.center - state.Root.position;
            Vector3 fullHalf = Vector3.Max(
                bounds.extents, new Vector3(FlightMinHalfExtent, FlightMinHalfExtent, FlightMinHalfExtent));
            Vector3 desiredCenter = body.position + state.FlightCenterOffset;
            Vector3 center;
            Vector3 half;
            if (atRest)
            {
                center = desiredCenter;
                half = fullHalf;
            }
            else
            {
                ResolveFlightStart(state, ray.origin, desiredCenter, fullHalf, out center, out half);
            }

            state.FlightFullHalf = fullHalf;
            state.FlightHalf = half;
            state.FlightPosition = center - state.FlightCenterOffset;
            state.FlightDrag = throwForward ? 0.03f : 0.15f;
            state.FlightVelocity = throwForward
                ? PlayerVelocity(state.Player) * 0.35f + ray.direction * ThrowSpeed
                : Vector3.zero;
            state.Landed = false;

            float moved = (center - desiredCenter).magnitude;
            if (moved > 0.001f)
            {
                body.position = state.FlightPosition;
                state.Root.position = state.FlightPosition;
            }

            float scale = Mathf.Min(half.x / fullHalf.x, half.z / fullHalf.z);
            if (moved > 0.05f || scale < 0.999f)
            {
                Debug.Log("[Rebuild the School] Released next to a wall: the prop was pulled "
                    + moved.ToString("0.00") + " back toward you and its collision box is at "
                    + Mathf.RoundToInt(scale * 100f) + "% until it has room, so the flight starts clear of every wall.");
            }
        }

        /// <summary>
        /// The camera is known to be in free space, so the point sweep camera -> prop centre decides the
        /// side of every wall. Then the box is scanned back along that segment to the first position where
        /// it overlaps no wall; if there is none, the position that fits the largest collision box wins.
        /// </summary>
        private void ResolveFlightStart(
            CarryState state, Vector3 origin, Vector3 desiredCenter, Vector3 fullHalf,
            out Vector3 center, out Vector3 half)
        {
            EnvironmentController environment = state.Environment;
            Vector3 clamped = desiredCenter;
            Vector3 toCenter = desiredCenter - origin;
            FlightHit wallHit;
            if (FlightGridSweep(environment, origin, Vector3.zero, toCenter, false, out wallHit)
                && wallHit.Kind == FlightHitKind.Wall)
            {
                clamped = origin + toCenter * wallHit.T;
            }

            Vector3 segment = clamped - origin;
            int steps = Mathf.Max(1, Mathf.CeilToInt(segment.magnitude / FlightScanStep));
            Vector3 bestPoint = clamped;
            float bestScale = -1f;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 point = origin + segment * (1f - (float)i / steps);
                float scale = FlightFitScale(environment, point, fullHalf);
                if (scale >= 1f)
                {
                    bestPoint = point;
                    bestScale = scale;
                    break;
                }

                if (scale > bestScale)
                {
                    bestPoint = point;
                    bestScale = scale;
                }
            }

            bestScale = Mathf.Max(0f, bestScale);
            half = new Vector3(
                Mathf.Max(FlightMinHalfExtent, fullHalf.x * bestScale),
                fullHalf.y,
                Mathf.Max(FlightMinHalfExtent, fullHalf.z * bestScale));
            center = bestPoint;
            float low = state.FlightFloorY + half.y;
            if (center.y < low)
            {
                center.y = low;
            }

            if (2f * half.y + 0.2f < FlightCeilingHeight)
            {
                float high = state.FlightFloorY + FlightCeilingHeight - half.y;
                if (center.y > high)
                {
                    center.y = high;
                }
            }
        }

        private static float FlightFitScale(EnvironmentController environment, Vector3 point, Vector3 fullHalf)
        {
            for (int i = 0; i < FlightFitScales.Length; i++)
            {
                float scale = FlightFitScales[i];
                Vector3 trial = new Vector3(
                    Mathf.Max(FlightMinHalfExtent, fullHalf.x * scale),
                    fullHalf.y,
                    Mathf.Max(FlightMinHalfExtent, fullHalf.z * scale));
                if (!FlightOverlaps(environment, point, trial))
                {
                    return scale;
                }
            }

            return 0f;
        }

        private void StepAirborneProps()
        {
            if (_airborne.Count == 0)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state == null || state.Root == null || state.Body == null
                    || state.Environment == null || state.Landed)
                {
                    continue;
                }

                try
                {
                    RefreshFlightDoors(state.Environment);
                    StepFlight(state, dt);
                }
                catch (Exception exception)
                {
                    if (Time.unscaledTime >= _nextSweepErrorLogTime)
                    {
                        _nextSweepErrorLogTime = Time.unscaledTime + 5f;
                        Debug.LogError("[Rebuild the School] Thrown-prop flight step failed: " + exception);
                    }
                }
            }
        }

        /// <summary>
        /// One physics step of a released prop: gravity and drag, then up to FlightMaxBounces contacts. The
        /// box sweeps against the cell grid (wall bits, void cells, closed doors and windows), the floor and
        /// the ceiling are analytic and continuous, so no speed can skip a wall.
        /// </summary>
        private void StepFlight(CarryState state, float dt)
        {
            // Riding a student or sinking into a whirlpool replace the flight (g.Events.cs).
            if (FxStepSpecial(state, dt))
            {
                return;
            }

            Vector3 c = state.FlightPosition + state.FlightCenterOffset;
            Vector3 stepStart = c;
            Vector3 v = state.FlightVelocity;
            Vector3 half = GrowFlightBox(state, c);
            float gravity = Physics.gravity.y;
            if (gravity > -1f)
            {
                gravity = FlightGravityFallback;
            }

            // Gravity Chaos: +1 pulls down, -1 up (the factor turns over smoothly).
            float gravityScale = FxGravityScale(state, c, half, dt);
            v.y += gravity * gravityScale * dt;
            v *= Mathf.Max(0f, 1f - state.FlightDrag * dt);
            float speed = v.magnitude;
            if (speed > FlightMaxSpeed)
            {
                v *= FlightMaxSpeed / speed;
            }

            // Flood: a whirlpool catches the prop's own speed (v) and pulls it in; the pull is a drift on top of
            // the velocity, taken off again after the step.
            Vector3 drift = FxDrift(state, c, half, dt, ref v);
            v += drift;
            Vector3 impactVelocity = v;
            float remaining = 1f;
            bool settled = false;
            bool landed = false;
            bool pinned = false;
            for (int iteration = 0; iteration <= FlightMaxBounces; iteration++)
            {
                if (remaining <= 0.001f)
                {
                    settled = true;
                    break;
                }

                Vector3 d = v * (dt * remaining);
                FlightHit hit;
                if (!FindFlightHit(state, c, half, d, out hit))
                {
                    c += d;
                    settled = true;
                    break;
                }

                c += d * hit.T;
                if (hit.Kind == FlightHitKind.Floor && gravityScale >= GravityLandFactor)
                {
                    c.y = state.FlightFloorY + half.y;
                    if (FxHoldsOnFloor(state))
                    {
                        // In a whirlpool the prop slides along the floor to the middle instead of settling: only
                        // the vertical speed ends here, the rest of the step goes on (a prop that already lies on
                        // the floor touches it at once, so ending the step here would hold it in place).
                        FxFloorContact(state, c);
                        v.y = 0f;
                        remaining *= 1f - hit.T;
                        continue;
                    }

                    landed = true;
                    v = Vector3.zero;
                    settled = true;
                    break;
                }

                if (hit.Kind == FlightHitKind.Ceiling && gravityScale <= -GravityLandFactor)
                {
                    // Gravity Chaos: gravity points up, the prop sticks to the ceiling until it changes.
                    c.y = state.FlightFloorY + FlightCeilingHeight - half.y;
                    pinned = true;
                    v = Vector3.zero;
                    settled = true;
                    break;
                }

                if (hit.Kind == FlightHitKind.Support && gravityScale >= GravityLandFactor)
                {
                    c += hit.Normal * FlightSkin;
                    landed = true;
                    v = Vector3.zero;
                    settled = true;
                    break;
                }

                if (!hit.Inside)
                {
                    c += hit.Normal * FlightSkin;
                }

                RegisterFlightImpact(state, hit, v, c);
                v = FlightReflect(v, hit.Normal);
                remaining *= 1f - hit.T;
            }

            if (!settled)
            {
                // Wedged between surfaces: stop here; gravity takes over on the next step.
                v = Vector3.zero;
            }
            else if (!landed && !pinned)
            {
                v -= drift;
            }

            if (state.Thrown)
            {
                HitNpcs(state, stepStart, c, half, impactVelocity, ref v);
            }

            if (landed)
            {
                // The prop touched the floor (or a support): the whoosh ends, a quiet LockDoorStop plays.
                StopFlightSound(state);
                PlaySoundAt(c, FloorSoundName, FloorSoundVolume);
            }
            else if (state.FlightSoundObject != null)
            {
                state.FlightSoundObject.transform.position = c;
            }

            FxNotePinned(state, pinned, c);
            state.FlightVelocity = landed ? Vector3.zero : v;
            state.FlightPosition = c - state.FlightCenterOffset;
            FxApplyPose(state, c);
            if (landed)
            {
                state.Landed = true;
            }
        }

        /// <summary>
        /// The collision box may be smaller than the prop while the prop is squeezed against a wall; it
        /// grows back as soon as there is room (never into an overlap).
        /// </summary>
        private static Vector3 GrowFlightBox(CarryState state, Vector3 center)
        {
            Vector3 half = state.FlightHalf;
            Vector3 full = state.FlightFullHalf;
            if (half.x >= full.x && half.z >= full.z)
            {
                return half;
            }

            Vector3 trial = new Vector3(
                Mathf.Min(full.x, Mathf.Max(half.x * FlightGrowFactor, half.x + 0.05f)),
                half.y,
                Mathf.Min(full.z, Mathf.Max(half.z * FlightGrowFactor, half.z + 0.05f)));
            if (!FlightOverlaps(state.Environment, center, trial))
            {
                state.FlightHalf = trial;
                return trial;
            }

            return half;
        }

        private bool FindFlightHit(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            for (int attempt = 0; attempt < FlightMaxDoorRetries; attempt++)
            {
                FlightHit candidate;
                FlightHit other;
                bool found = FlightGridSweep(state.Environment, c, half, d, true, out candidate);
                if (FlightPlaneSweep(state, c, half, d, out other) && (!found || other.T < candidate.T))
                {
                    candidate = other;
                    found = true;
                }

                if (FlightColliderSweep(state, c, half, d, out other) && (!found || other.T < candidate.T))
                {
                    candidate = other;
                    found = true;
                }

                if (!found)
                {
                    return false;
                }

                best = candidate;
                if (candidate.Kind == FlightHitKind.Door && FlightDoorLetsPass(state, candidate.Door, c))
                {
                    // The door (or window) gave way; look again for whatever is behind it.
                    continue;
                }

                return true;
            }

            // Never treat a chain of doors as free space: the last hit stays a contact.
            return true;
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

        private static Vector3 FlightReflect(Vector3 velocity, Vector3 normal)
        {
            float into = -Vector3.Dot(velocity, normal);
            if (into <= 0f)
            {
                return velocity;
            }

            Vector3 tangent = velocity + normal * into;
            float away = into * FlightBounceRestitution;
            if (away < FlightBounceMinNormalSpeed)
            {
                away = 0f;
            }

            return tangent * FlightBounceFriction + normal * away;
        }

        private static void RegisterFlightImpact(CarryState state, FlightHit hit, Vector3 velocity, Vector3 point)
        {
            state.WallHits++;
            PlayImpactSound(state, hit, velocity, point);
            if (state.WallHits != 1)
            {
                return;
            }

            string what;
            switch (hit.Kind)
            {
                case FlightHitKind.Wall:
                    what = "a wall (cell " + hit.EdgeX + "," + hit.EdgeZ
                        + (hit.EdgeAxis == 0 ? ", east edge)" : ", north edge)");
                    break;
                case FlightHitKind.Door:
                    what = hit.Door != null && hit.Door.Door != null
                        ? "the closed " + DescribePath(hit.Door.Door.transform)
                        : "a closed door";
                    break;
                case FlightHitKind.Ceiling:
                    what = "the ceiling";
                    break;
                case FlightHitKind.Collider:
                    what = hit.Collider != null ? DescribePath(hit.Collider.transform) : "a collider";
                    break;
                default:
                    what = hit.Kind.ToString();
                    break;
            }

            float into = Mathf.Max(0f, -Vector3.Dot(velocity, hit.Normal));
            Debug.Log("[Rebuild the School] " + (state.Thrown ? "Thrown" : "Dropped") + " prop hit " + what
                + " at " + into.ToString("0.0") + " u/s; bounced.");
        }

        /// <summary>
        /// A prop that hits a wall, a closed door, the ceiling or a loose collider plays Bang. Grazing contacts
        /// and a quick series of bounces stay silent.
        /// </summary>
        private static void PlayImpactSound(CarryState state, FlightHit hit, Vector3 velocity, Vector3 point)
        {
            float into = Mathf.Max(0f, -Vector3.Dot(velocity, hit.Normal));
            if (into < ImpactSoundMinSpeed || Time.time < state.NextImpactSoundTime)
            {
                return;
            }

            state.NextImpactSoundTime = Time.time + ImpactSoundInterval;
            PlaySoundAt(point, BangSoundName, 1f);
        }

        /// <summary>The flight integrator reached the floor or a support: put the prop there for settling.</summary>
        private static bool FinishFlight(CarryState state, out Cell supportCell)
        {
            Vector3 position = state.FlightPosition;
            state.Body.position = position;
            state.Root.position = position;
            state.FlightVelocity = Vector3.zero;
            // The cell under the prop's centre owns it; the pivot can sit outside the prop's own bounds.
            return TryGetCell(state.Environment, position + state.FlightCenterOffset, out supportCell)
                || TryGetCell(state.Environment, position, out supportCell);
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

        // axis 0 = the east edge of cell (ax, az) (plane x = 10 (ax + 1)); axis 1 = its north edge (plane z = 10 (az + 1)).
        private static long FlightEdgeKey(int ax, int az, int axis)
        {
            return (((long)(ax + 1000)) * 100000L + (long)(az + 1000)) * 2L + axis;
        }

        private static bool TryFlightEdgeKey(IntVector2 position, Direction direction, out long key)
        {
            key = 0L;
            switch (direction)
            {
                case Direction.North:
                    key = FlightEdgeKey(position.x, position.z, 1);
                    return true;
                case Direction.East:
                    key = FlightEdgeKey(position.x, position.z, 0);
                    return true;
                case Direction.South:
                    key = FlightEdgeKey(position.x, position.z - 1, 1);
                    return true;
                case Direction.West:
                    key = FlightEdgeKey(position.x - 1, position.z, 0);
                    return true;
                default:
                    return false;
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

        // A cell that is outside the map, Null, an empty filler (ConstBin 16) or in an event spawner room (props never go
        // there, not even through its open door) is void: all its edges are solid.
        private static Cell FlightCell(EnvironmentController environment, int x, int z)
        {
            IntVector2 position = new IntVector2(x, z);
            if (!environment.ContainsCoordinates(position))
            {
                return null;
            }

            Cell cell = environment.CellFromPosition(position);
            return cell == null || cell.Null || cell.room == null || cell.ConstBin == 16
                || cell.room.category == RoomCategory.EventSpawner ? null : cell;
        }

        private static bool FlightEdgeSolid(EnvironmentController environment, int ax, int az, int axis)
        {
            Cell a = FlightCell(environment, ax, az);
            Cell b = axis == 0 ? FlightCell(environment, ax + 1, az) : FlightCell(environment, ax, az + 1);
            if (a == null || b == null)
            {
                return true;
            }

            Direction direction = axis == 0 ? Direction.East : Direction.North;
            return a.HasWallInDirection(direction) || b.HasWallInDirection(direction.GetOpposite());
        }

        private FlightEdgeKind FlightEdgeCandidate(
            EnvironmentController environment, int ax, int az, int axis, bool includeDoors, out FlightDoor door)
        {
            door = null;
            if (FlightEdgeSolid(environment, ax, az, axis))
            {
                return FlightEdgeKind.Wall;
            }

            List<FlightDoor> list;
            if (includeDoors && _flightDoors.TryGetValue(FlightEdgeKey(ax, az, axis), out list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (FlightDoorIsSolid(list[i]))
                    {
                        door = list[i];
                        return FlightEdgeKind.Door;
                    }
                }
            }

            return FlightEdgeKind.None;
        }

        /// <summary>Strict test: does the box overlap any wall or void edge (doors are not counted)?</summary>
        private static bool FlightOverlaps(EnvironmentController environment, Vector3 c, Vector3 half)
        {
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
                        && FlightEdgeSolid(environment, k - 1, j, 0))
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
                        && FlightEdgeSolid(environment, i, m - 1, 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Where a landed prop may rest: its full-size box overlaps no wall, void edge, door or window edge.
        /// A flight can end with a smaller box than the prop's (it started squeezed against a wall), the
        /// placement can turn the prop, and a doorway that is open now closes again with the prop in it, so
        /// the check uses the largest footprint the prop can have at that spot. Doors count open or shut.
        /// </summary>
        private bool FlightBoxClear(CarryState state, Vector3 rootPosition)
        {
            Vector3 half = state.FlightFullHalf;
            if (state.Environment == null || half.x <= 0f || half.z <= 0f)
            {
                return true;
            }

            Vector3 offset = state.FlightCenterOffset;
            if (state.Root != null && Quaternion.Angle(state.Root.rotation, state.TargetRotation) > 1f)
            {
                // Turned on landing: a square that holds the footprint at any angle, centred on the pivot.
                float reach = Mathf.Sqrt(half.x * half.x + half.z * half.z)
                    + new Vector2(offset.x, offset.z).magnitude;
                half.x = reach;
                half.z = reach;
                offset = Vector3.zero;
            }

            Vector3 centre = rootPosition + offset;
            return !FlightOverlaps(state.Environment, centre, half) && !FlightDoorSlabOverlaps(centre, half);
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
                        && _flightDoors.ContainsKey(FlightEdgeKey(k - 1, j, 0)))
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
                        && _flightDoors.ContainsKey(FlightEdgeKey(i, m - 1, 1)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Earliest contact of the box (centre c, half extents, displacement d) with a wall, a void edge or
        /// a closed door/window. Every solid edge is a thin rectangle, Minkowski-expanded by the half
        /// extents, so the box is a point swept against it: an exact slab test, no tunnelling at any speed.
        /// </summary>
        private bool FlightGridSweep(
            EnvironmentController environment, Vector3 c, Vector3 half, Vector3 d, bool includeDoors, out FlightHit best)
        {
            best = new FlightHit();
            bool found = false;
            float margin = FlightSkin + FlightWallHalfThickness;
            float minX = Mathf.Min(c.x, c.x + d.x) - half.x - margin;
            float maxX = Mathf.Max(c.x, c.x + d.x) + half.x + margin;
            float minZ = Mathf.Min(c.z, c.z + d.z) - half.z - margin;
            float maxZ = Mathf.Max(c.z, c.z + d.z) + half.z + margin;

            // x-normal planes at x = 10 k: the east edge of cells (k - 1, j)
            int k0 = Mathf.CeilToInt(minX / WorldTileWidth);
            int k1 = Mathf.FloorToInt(maxX / WorldTileWidth);
            int j0 = Mathf.FloorToInt(minZ / WorldTileWidth);
            int j1 = Mathf.FloorToInt(maxZ / WorldTileWidth);
            for (int k = k0; k <= k1; k++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    FlightDoor door;
                    FlightEdgeKind kind = FlightEdgeCandidate(environment, k - 1, j, 0, includeDoors, out door);
                    if (kind == FlightEdgeKind.None)
                    {
                        continue;
                    }

                    float t;
                    float na;
                    float nl;
                    bool inside;
                    if (!FlightSlabHit(
                            c.x, c.z, d.x, d.z, k * WorldTileWidth, j * WorldTileWidth, (j + 1) * WorldTileWidth,
                            half.x, half.z, out t, out na, out nl, out inside))
                    {
                        continue;
                    }

                    if (!found || t < best.T)
                    {
                        found = true;
                        best.T = t;
                        best.Normal = new Vector3(na, 0f, nl);
                        best.Kind = kind == FlightEdgeKind.Door ? FlightHitKind.Door : FlightHitKind.Wall;
                        best.Inside = inside;
                        best.Door = door;
                        best.Collider = null;
                        best.EdgeX = k - 1;
                        best.EdgeZ = j;
                        best.EdgeAxis = 0;
                    }
                }
            }

            // z-normal planes at z = 10 m: the north edge of cells (i, m - 1)
            int m0 = Mathf.CeilToInt(minZ / WorldTileWidth);
            int m1 = Mathf.FloorToInt(maxZ / WorldTileWidth);
            int i0 = Mathf.FloorToInt(minX / WorldTileWidth);
            int i1 = Mathf.FloorToInt(maxX / WorldTileWidth);
            for (int m = m0; m <= m1; m++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    FlightDoor door;
                    FlightEdgeKind kind = FlightEdgeCandidate(environment, i, m - 1, 1, includeDoors, out door);
                    if (kind == FlightEdgeKind.None)
                    {
                        continue;
                    }

                    float t;
                    float na;
                    float nl;
                    bool inside;
                    if (!FlightSlabHit(
                            c.z, c.x, d.z, d.x, m * WorldTileWidth, i * WorldTileWidth, (i + 1) * WorldTileWidth,
                            half.z, half.x, out t, out na, out nl, out inside))
                    {
                        continue;
                    }

                    if (!found || t < best.T)
                    {
                        found = true;
                        best.T = t;
                        best.Normal = new Vector3(nl, 0f, na);
                        best.Kind = kind == FlightEdgeKind.Door ? FlightHitKind.Door : FlightHitKind.Wall;
                        best.Inside = inside;
                        best.Door = door;
                        best.Collider = null;
                        best.EdgeX = i;
                        best.EdgeZ = m - 1;
                        best.EdgeAxis = 1;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Sweeps the box centre (ca along the plane normal, cl along the wall) by (da, dl) against one wall
        /// rectangle [plane +- thickness] x [latLo, latHi], expanded by the half extents (ha, hl). When the
        /// centre is already inside (the box overlaps the wall) the position is never moved: the wall only
        /// blocks motion that goes deeper through the nearest exit face, so the centre cannot cross it.
        /// </summary>
        private static bool FlightSlabHit(
            float ca, float cl, float da, float dl, float plane, float latLo, float latHi, float ha, float hl,
            out float t, out float na, out float nl, out bool inside)
        {
            t = 0f;
            na = 0f;
            nl = 0f;
            inside = false;
            float aLo = plane - FlightWallHalfThickness - ha;
            float aHi = plane + FlightWallHalfThickness + ha;
            float lLo = latLo - hl;
            float lHi = latHi + hl;
            if (aLo < ca && ca < aHi && lLo < cl && cl < lHi)
            {
                float paLo = ca - aLo;
                float paHi = aHi - ca;
                float plLo = cl - lLo;
                float plHi = lHi - cl;
                float least = Mathf.Min(Mathf.Min(paLo, paHi), Mathf.Min(plLo, plHi));
                if (least == paLo)
                {
                    na = -1f;
                }
                else if (least == paHi)
                {
                    na = 1f;
                }
                else if (least == plLo)
                {
                    nl = -1f;
                }
                else
                {
                    nl = 1f;
                }

                if (da * na + dl * nl >= 0f)
                {
                    na = 0f;
                    nl = 0f;
                    return false;
                }

                inside = true;
                return true;
            }

            float tMin = -1e30f;
            float tMax = 1e30f;
            if (Mathf.Abs(da) < FlightParallelEpsilon)
            {
                if (ca <= aLo || ca >= aHi)
                {
                    return false;
                }
            }
            else
            {
                float t1 = (aLo - ca) / da;
                float t2 = (aHi - ca) / da;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    na = da > 0f ? -1f : 1f;
                    nl = 0f;
                }

                tMax = Mathf.Min(tMax, t2);
            }

            if (Mathf.Abs(dl) < FlightParallelEpsilon)
            {
                if (cl <= lLo || cl >= lHi)
                {
                    return false;
                }
            }
            else
            {
                float t1 = (lLo - cl) / dl;
                float t2 = (lHi - cl) / dl;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    na = 0f;
                    nl = dl > 0f ? -1f : 1f;
                }

                tMax = Mathf.Min(tMax, t2);
            }

            if (tMin > tMax || tMax <= 0f || tMin > 1f)
            {
                return false;
            }

            if (tMin < 0f)
            {
                tMin = 0f;
                if (na == 0f && nl == 0f)
                {
                    return false;
                }
            }

            t = tMin;
            return true;
        }

        private static bool FlightPlaneSweep(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            bool found = false;
            if (d.y < 0f)
            {
                float gap = (c.y - half.y) - state.FlightFloorY;
                float t = gap < 0f ? 0f : gap / -d.y;
                if (t <= 1f)
                {
                    found = true;
                    best.T = t;
                    best.Normal = Vector3.up;
                    best.Kind = FlightHitKind.Floor;
                }
            }

            // A prop taller than the room has no ceiling contact; it would never fit under it anyway.
            if (d.y > 0f && 2f * half.y + 0.2f < FlightCeilingHeight)
            {
                float gap = state.FlightFloorY + FlightCeilingHeight - (c.y + half.y);
                float t = gap < 0f ? 0f : gap / d.y;
                if (t <= 1f && (!found || t < best.T))
                {
                    found = true;
                    best.T = t;
                    best.Normal = Vector3.down;
                    best.Kind = FlightHitKind.Ceiling;
                }
            }

            return found;
        }

        /// <summary>
        /// Loose props and furniture: a plain box cast. Walls (Tile colliders) and doors/windows come from the
        /// analytic sweep above, entities and the player are ignored, and hits that start inside a collider
        /// are skipped so a prop can never get stuck in one.
        /// </summary>
        private bool FlightColliderSweep(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            float distance = d.magnitude;
            if (distance < 0.0005f)
            {
                return false;
            }

            Vector3 direction = d / distance;
            int count = Physics.BoxCastNonAlloc(
                c,
                half,
                direction,
                SweepHits,
                Quaternion.identity,
                distance + FlightSkin,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return false;
            }

            try
            {
                if (count > 1)
                {
                    Array.Sort(SweepHits, 0, count, RayHitOrder);
                }

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = SweepHits[i];
                    Collider collider = hit.collider;
                    if (collider == null || !collider.enabled || hit.distance <= 0.0001f
                        || Vector3.Dot(direction, hit.normal) >= 0f
                        || IsPartOf(collider.transform, state.Root)
                        || (state.Player != null && IsPartOf(collider.transform, state.Player.transform))
                        || collider.GetComponentInParent<Entity>() != null
                        || collider.GetComponentInParent<Door>() != null
                        || collider.GetComponentInParent<Tile>() != null)
                    {
                        continue;
                    }

                    best.T = Mathf.Clamp01(hit.distance / distance);
                    best.Normal = hit.normal.normalized;
                    best.Collider = collider;
                    best.Kind = d.y <= 0f && best.Normal.y >= GroundSupportNormal
                        ? FlightHitKind.Support
                        : FlightHitKind.Collider;
                    return true;
                }

                return false;
            }
            finally
            {
                Array.Clear(SweepHits, 0, count);
            }
        }

        private static bool IsPartOf(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        // --- thrown props hit characters ---

        /// <summary>
        /// Thrown props only. The segment the prop's centre travelled this step is tested against the bounds
        /// of every character (the prop's half extents added), so no speed can skip one. A character that is
        /// hit is pushed once per flight, the Bang sound plays and the prop bounces back. Walls, doors and the
        /// floor were decided before this runs and are never changed by it.
        /// </summary>
        private void HitNpcs(
            CarryState state, Vector3 start, Vector3 end, Vector3 half, Vector3 impactVelocity, ref Vector3 velocity)
        {
            try
            {
                Vector3 flat = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
                List<NPC> npcs = state.Environment != null ? state.Environment.Npcs : null;
                if (npcs == null || flat.magnitude < NpcMinHitSpeed)
                {
                    return;
                }

                bool hit = false;
                for (int i = 0; i < npcs.Count; i++)
                {
                    NPC npc = npcs[i];
                    Entity entity;
                    Bounds bounds;
                    if (!TryGetPushableNpc(npc, out entity, out bounds)
                        || (state.PushedNpcs != null && state.PushedNpcs.Contains(npc)))
                    {
                        continue;
                    }

                    bounds.Expand(half * 2f);
                    float entry;
                    if (!SegmentHitsBounds(start, end, bounds, out entry))
                    {
                        continue;
                    }

                    // Only a prop that is moving toward the character hits it.
                    Vector3 away = entity.transform.position - Vector3.Lerp(start, end, entry);
                    away.y = 0f;
                    if (Vector3.Dot(flat, away) <= 0f)
                    {
                        continue;
                    }

                    if (state.PushedNpcs == null)
                    {
                        state.PushedNpcs = new List<NPC>();
                    }

                    state.PushedNpcs.Add(npc);
                    float push = PushNpc(npc, entity, away, flat);
                    BreakBullyingRule(state);
                    hit = true;
                    Debug.Log("[Rebuild the School] Thrown prop hit " + npc.name + " at "
                        + flat.magnitude.ToString("0.0") + " u/s; pushed it away at "
                        + push.ToString("0.0") + " u/s (Bang); the player broke the Bullying rule.");
                }

                if (hit)
                {
                    velocity.x = -velocity.x * NpcHitBounce;
                    velocity.z = -velocity.z * NpcHitBounce;
                }
            }
            catch (Exception exception)
            {
                if (Time.unscaledTime >= _nextSweepErrorLogTime)
                {
                    _nextSweepErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Rebuild the School] Pushing a character with a thrown prop failed: " + exception);
                }
            }
        }

        /// <summary>
        /// The push of Bully.Push: Entity.AddForce(new Force(direction, speed, acceleration)). The direction runs
        /// from the point of impact through the character (the Bully uses its own position instead).
        /// </summary>
        private static float PushNpc(NPC npc, Entity entity, Vector3 away, Vector3 flatVelocity)
        {
            if (away.sqrMagnitude < 0.0001f)
            {
                away = flatVelocity;
            }

            float push = Mathf.Clamp(flatVelocity.magnitude * NpcPushPerPropSpeed, NpcPushMinSpeed, NpcPushMaxSpeed)
                * NpcPushStrength;
            entity.AddForce(new Force(away.normalized, push, -push));
            PlayBang(npc);
            return push;
        }

        /// <summary>
        /// A character can be pushed while it is active, visible, not frozen or squished (a frozen entity keeps
        /// its forces unused) and has an enabled collider; its bounds are those of its colliders.
        /// </summary>
        private static bool TryGetPushableNpc(NPC npc, out Entity entity, out Bounds bounds)
        {
            entity = null;
            bounds = new Bounds();
            if (npc == null || !npc.isActiveAndEnabled || npc.Navigator == null)
            {
                return false;
            }

            entity = npc.Navigator.Entity;
            if (entity == null || !entity.isActiveAndEnabled || entity.Frozen || entity.Squished || !entity.Visible)
            {
                return false;
            }

            // The solid colliders decide; a character that only has trigger colliders uses those.
            bool found = false;
            entity.GetComponents(NpcColliders);
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                for (int i = 0; i < NpcColliders.Count; i++)
                {
                    Collider collider = NpcColliders[i];
                    if (collider == null || !collider.enabled || collider.isTrigger != (pass == 1))
                    {
                        continue;
                    }

                    if (found)
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                    else
                    {
                        bounds = collider.bounds;
                        found = true;
                    }
                }
            }

            NpcColliders.Clear();
            return found;
        }

        /// <summary>Does the segment a -> b touch the box? entry is the segment fraction where it first does.</summary>
        private static bool SegmentHitsBounds(Vector3 a, Vector3 b, Bounds box, out float entry)
        {
            entry = 0f;
            float tMin = 0f;
            float tMax = 1f;
            Vector3 delta = b - a;
            Vector3 low = box.min;
            Vector3 high = box.max;
            if (!SlabRange(a.x, delta.x, low.x, high.x, ref tMin, ref tMax)
                || !SlabRange(a.y, delta.y, low.y, high.y, ref tMin, ref tMax)
                || !SlabRange(a.z, delta.z, low.z, high.z, ref tMin, ref tMax))
            {
                return false;
            }

            entry = tMin;
            return true;
        }

        private static bool SlabRange(
            float origin, float delta, float low, float high, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(delta) < 1e-6f)
            {
                return origin >= low && origin <= high;
            }

            float t1 = (low - origin) / delta;
            float t2 = (high - origin) / delta;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }

            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }

        private static void PlayBang(NPC npc)
        {
            SoundObject bang = GetSound(BangSoundName);
            if (bang == null)
            {
                return;
            }

            // Played by the character's own audio manager, like 1st Prize plays its audBang.
            AudioManager manager = npc.GetComponentInChildren<AudioManager>();
            if (manager != null)
            {
                manager.PlaySingle(bang);
            }
        }

        /// <summary>The player breaks the "Bullying" rule; the Principal reacts when he sees it.</summary>
        private static void BreakBullyingRule(CarryState state)
        {
            if (state.Player != null)
            {
                state.Player.RuleBreak(BullyingRule, BullyingLinger, BullyingSensitivity);
            }
        }

        // --- sounds of a flying prop ---

        /// <summary>
        /// A sound by its SoundObject name. All SoundObjects are indexed on first use (and again after
        /// SoundRescanInterval seconds while a requested sound is missing), so a lookup is free afterwards.
        /// </summary>
        private static SoundObject GetSound(string name)
        {
            SoundObject sound;
            if (SoundsByName.TryGetValue(name, out sound) && sound != null)
            {
                return sound;
            }

            if (Time.unscaledTime < _nextSoundScanTime)
            {
                return null;
            }

            _nextSoundScanTime = Time.unscaledTime + SoundRescanInterval;
            ScanSounds();
            return SoundsByName.TryGetValue(name, out sound) && sound != null ? sound : null;
        }

        private static void ScanSounds()
        {
            try
            {
                SoundObject known;
                SoundObject[] sounds = Resources.FindObjectsOfTypeAll<SoundObject>();
                for (int i = 0; i < sounds.Length; i++)
                {
                    SoundObject candidate = sounds[i];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.name)
                        && (!SoundsByName.TryGetValue(candidate.name, out known) || known == null))
                    {
                        SoundsByName[candidate.name] = candidate;
                    }
                }

                // 1st Prize plays its audBang when it slams into a wall.
                if (!SoundsByName.TryGetValue(BangSoundName, out known) || known == null)
                {
                    FirstPrize[] prizes = Resources.FindObjectsOfTypeAll<FirstPrize>();
                    for (int i = 0; i < prizes.Length; i++)
                    {
                        if (prizes[i] != null && prizes[i].audBang != null)
                        {
                            SoundsByName[BangSoundName] = prizes[i].audBang;
                            break;
                        }
                    }
                }

                // The Gum (the projectile of Beans) loops its audWhoosh while it flies.
                if (!SoundsByName.TryGetValue(WhooshSoundName, out known) || known == null)
                {
                    FieldInfo whooshField = typeof(Gum).GetField(
                        "audWhoosh", BindingFlags.Instance | BindingFlags.NonPublic);
                    Gum[] gums = whooshField != null ? Resources.FindObjectsOfTypeAll<Gum>() : new Gum[0];
                    for (int i = 0; i < gums.Length; i++)
                    {
                        SoundObject whoosh = gums[i] != null ? whooshField.GetValue(gums[i]) as SoundObject : null;
                        if (whoosh != null)
                        {
                            SoundsByName[WhooshSoundName] = whoosh;
                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                LogSoundError("Indexing the sounds", exception);
            }

            ReportSound(BangSoundName);
            ReportSound(WhooshSoundName);
            ReportSound(FloorSoundName);
        }

        /// <summary>Logs, once per outcome, whether a sound the mod plays exists.</summary>
        private static void ReportSound(string name)
        {
            SoundObject sound;
            bool found = SoundsByName.TryGetValue(name, out sound) && sound != null;
            if (SoundsReported.Add(name + (found ? ":found" : ":missing")))
            {
                Debug.Log("[Rebuild the School] Sound '" + name + "': "
                    + (found ? "found (" + sound.name + ")." : "not found, so it stays silent."));
            }
        }

        /// <summary>
        /// A 3D sound source on its own object, built like AudioManager.VirtualAwake builds its AudioSourceManager
        /// (inactive while it is set up), so the game's volume settings and mixer groups apply.
        /// </summary>
        private static AudioSourceManager CreateSoundSource(string hostName, Vector3 position, out GameObject host)
        {
            host = new GameObject(hostName);
            try
            {
                host.SetActive(false);
                host.transform.position = position;
                AudioSourceManager manager = host.AddComponent<AudioSourceManager>();
                manager.Initialize(true, false, false);
                manager.spatialBlend = 1f;
                manager.rolloffMode = AudioRolloffMode.Linear;
                manager.minDistance = SoundMinDistance;
                manager.maxDistance = SoundMaxDistance;
                host.SetActive(true);
                return manager;
            }
            catch
            {
                Destroy(host);
                throw;
            }
        }

        /// <summary>A short sound at a position, fire and forget: its object removes itself.</summary>
        private static void PlaySoundAt(Vector3 position, string soundName, float volume)
        {
            try
            {
                SoundObject sound = GetSound(soundName);
                if (sound == null || sound.soundClip == null)
                {
                    return;
                }

                GameObject host;
                AudioSourceManager manager = CreateSoundSource("Rebuild the School sound", position, out host);
                manager.PlayOneShot(SoundType.Effect, sound.soundClip, sound.volumeMultiplier * volume);
                Destroy(host, sound.soundClip.length + 0.5f);
            }
            catch (Exception exception)
            {
                LogSoundError("Playing the sound '" + soundName + "'", exception);
            }
        }

        /// <summary>The looping whoosh of a thrown prop; StepFlight moves it with the prop, landing ends it.</summary>
        private static void StartFlightSound(CarryState state)
        {
            try
            {
                SoundObject whoosh = GetSound(WhooshSoundName);
                if (whoosh == null || whoosh.soundClip == null)
                {
                    return;
                }

                GameObject host;
                AudioSourceManager manager = CreateSoundSource(
                    "Rebuild the School flight sound", state.FlightPosition + state.FlightCenterOffset, out host);
                manager.SetClip(SoundType.Effect, whoosh.soundClip);
                manager.loop = true;
                manager.volume = whoosh.volumeMultiplier;
                manager.Play();
                // A flight ends by itself after ReleaseTimeout; this only guards against a missed stop.
                Destroy(host, ReleaseTimeout + 2f);
                state.FlightSoundObject = host;
            }
            catch (Exception exception)
            {
                LogSoundError("Starting the flight sound", exception);
            }
        }

        private static void StopFlightSound(CarryState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.FlightSoundObject != null)
            {
                Destroy(state.FlightSoundObject);
            }

            state.FlightSoundObject = null;
        }

        private static void LogSoundError(string what, Exception exception)
        {
            if (Time.unscaledTime >= _nextSoundErrorLogTime)
            {
                _nextSoundErrorLogTime = Time.unscaledTime + 5f;
                Debug.LogError("[Rebuild the School] " + what + " failed: " + exception);
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

            Debug.LogWarning("[Rebuild the School] No free spot for the prop near " + landingPosition
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

            Debug.Log("[Rebuild the School] Prop '" + (state.Root != null ? state.Root.name : "?") + "' "
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
                    if (!state.Body.isKinematic)
                    {
                        state.Body.velocity = restoreVelocity ? state.OriginalVelocity : Vector3.zero;
                        state.Body.angularVelocity = restoreVelocity ? state.OriginalAngularVelocity : Vector3.zero;
                    }
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

        // A name is looked at over and over (every renderer of every copy of a prop has one), so the answer is kept.
        private static readonly Dictionary<string, bool> SupportedNames = new Dictionary<string, bool>();

        private static bool IsSupportedPropName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            bool supported;
            if (SupportedNames.TryGetValue(name, out supported))
            {
                return supported;
            }

            supported = ComputeSupportedPropName(name);
            if (SupportedNames.Count >= 4096)
            {
                SupportedNames.Clear();
            }

            SupportedNames[name] = supported;
            return supported;
        }

        private static bool ComputeSupportedPropName(string name)
        {
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

        private static readonly string[] ForbiddenNameParts =
        {
            "elevator", "lift", "lamp", "light", "chandelier", "sconce",
            "bsoda", "no squee", "nosquee", "covercloud", "chalkcloud"
        };

        private static bool IsForbiddenPickupRoot(Transform root)
        {
            return ForbiddenPickupReason(root) != null;
        }

        /// <summary>Why a root must never be carried (elevators, items, clouds, real lights...), or null.</summary>
        private static string ForbiddenPickupReason(Transform root)
        {
            if (root == null)
            {
                return "missing object";
            }

            if (root.GetComponentInParent<Elevator>() != null
                || root.GetComponentInChildren<Elevator>(true) != null)
            {
                return "elevator";
            }

            if (root.GetComponentInParent<Item>() != null
                || root.GetComponentInChildren<Item>(true) != null)
            {
                return "item";
            }

            if (root.GetComponentInParent<CoverCloud>() != null
                || root.GetComponentInChildren<CoverCloud>(true) != null)
            {
                return "cover cloud";
            }

            if (HasForeignLight(root))
            {
                return "contains a light (lamp or fixture)";
            }

            string name = root.name.ToLowerInvariant();
            for (int i = 0; i < ForbiddenNameParts.Length; i++)
            {
                if (name.Contains(ForbiddenNameParts[i]))
                {
                    return "name contains '" + ForbiddenNameParts[i] + "'";
                }
            }

            return null;
        }

        /// <summary>
        /// A real light under the root marks a lamp or fixture. The glow this runtime attaches to the very
        /// prop it highlights or carries is also a Light; it must never make that prop "forbidden",
        /// otherwise a highlighted prop could not be picked up.
        /// </summary>
        private static bool HasForeignLight(Transform root)
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.gameObject.name != GlowObjectName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLocker(Transform root)
        {
            return root != null && root.name.ToLowerInvariant().Contains("locker");
        }

        // The name of the copy that gives a locker its back (it must stay free of the words IsSupportedPropName and
        // ForbiddenNameParts look for).
        private const string LockerBackName = "BackFace";

        // The copy is a flat slab at the open back: this thick (a share of the locker's depth), this far inside
        // the back edge (units), and a hair smaller in height and width than the locker, so the edges of the
        // real sides and the top cover it.
        private const float LockerBackThickness = 0.02f;
        private const float LockerBackGap = 0.003f;
        private const float LockerBackScale = 0.998f;

        /// <summary>
        /// The red and blue lockers (LockerBuilder: lockerPre and hideLockerPre) have no back; the game never shows
        /// it because they stand against a wall. A carried locker shows it, so one of its sides is copied onto the
        /// back: a copy of every mesh of the locker, turned a quarter circle around the vertical axis through its
        /// middle, squeezed along the locker's width until it is a flat slab and stretched along its depth until
        /// that is as wide as the locker, then moved to the open back. The side of the copy that was a side of the
        /// locker now covers the back (the sides keep their texture and the way up); the rest of the copy is flat
        /// and lies inside the locker. The locker's width runs along its local X (the lockers of a row stand 2
        /// units apart along X) and the open back is toward +Z, the wall (LockerBuilder turns the locker with its
        /// +Z toward the wall). The copy is made once, stays part of the locker and is listed in the cell like
        /// the locker's own renderers, so the chunk culling shows and hides it with them. Returns every renderer
        /// of the locker, the copy included.
        /// </summary>
        private static Renderer[] EnsureLockerBack(Cell cell, Transform root, Renderer[] renderers)
        {
            try
            {
                if (root.Find(LockerBackName) != null)
                {
                    return root.GetComponentsInChildren<Renderer>(true);
                }

                // The middle of the locker in its own coordinates, from the meshes (a culled renderer has no bounds).
                Bounds local = new Bounds();
                bool hasBounds = false;
                List<MeshRenderer> sources = new List<MeshRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer source = renderers[i] as MeshRenderer;
                    Bounds mesh;
                    if (source == null || !TryGetLocalMeshBounds(source, out mesh))
                    {
                        continue;
                    }

                    sources.Add(source);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = mesh.center + new Vector3(
                            (corner & 1) == 0 ? -mesh.extents.x : mesh.extents.x,
                            (corner & 2) == 0 ? -mesh.extents.y : mesh.extents.y,
                            (corner & 4) == 0 ? -mesh.extents.z : mesh.extents.z);
                        point = root.InverseTransformPoint(source.transform.TransformPoint(point));
                        if (hasBounds)
                        {
                            local.Encapsulate(point);
                        }
                        else
                        {
                            local = new Bounds(point, Vector3.zero);
                            hasBounds = true;
                        }
                    }
                }

                float halfWidth = local.extents.x;
                float halfDepth = local.extents.z;
                if (!hasBounds || halfWidth < 0.05f || halfDepth < 0.05f)
                {
                    return renderers;
                }

                // p -> slabCentre + Q * S * (p - c), written as the pose of the holder (the copies keep their own
                // pose relative to the locker, which the holder then turns and squeezes). Q is a quarter turn
                // around the vertical: the copy's left side (-X) lands on the back plane (+Z), turned to face
                // away from the locker, its depth axis along X.
                Vector3 c = local.center;
                Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
                Vector3 squeeze = new Vector3(
                    LockerBackThickness * halfDepth / halfWidth,
                    LockerBackScale,
                    LockerBackScale * halfWidth / halfDepth);
                Vector3 slabCentre = new Vector3(
                    c.x, c.y, local.max.z - LockerBackThickness * halfDepth - LockerBackGap);
                Transform holder = new GameObject(LockerBackName).transform;
                holder.SetParent(root, false);
                holder.localRotation = turn;
                holder.localScale = squeeze;
                holder.localPosition = slabCentre - turn * Vector3.Scale(squeeze, c);

                Vector3 rootScale = root.lossyScale;
                List<Renderer> copies = new List<Renderer>();
                for (int i = 0; i < sources.Count; i++)
                {
                    MeshRenderer source = sources[i];
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    Vector3 sourceScale = source.transform.lossyScale;
                    GameObject part = new GameObject(LockerBackName + " Mesh");
                    part.layer = source.gameObject.layer;
                    Transform partTransform = part.transform;
                    partTransform.SetParent(holder, false);
                    partTransform.localPosition = root.InverseTransformPoint(source.transform.position);
                    partTransform.localRotation = Quaternion.Inverse(root.rotation) * source.transform.rotation;
                    partTransform.localScale = new Vector3(
                        rootScale.x != 0f ? sourceScale.x / rootScale.x : sourceScale.x,
                        rootScale.y != 0f ? sourceScale.y / rootScale.y : sourceScale.y,
                        rootScale.z != 0f ? sourceScale.z / rootScale.z : sourceScale.z);
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    MeshRenderer copy = part.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = source.sharedMaterials;
                    copy.shadowCastingMode = source.shadowCastingMode;
                    copy.receiveShadows = source.receiveShadows;
                    copies.Add(copy);
                }

                AddRendererReferences(cell, copies);
                Debug.Log("[Rebuild the School] " + root.name + " got a back: a side of the locker copied onto it ("
                    + copies.Count + " mesh copies, locker box " + local.size.x.ToString("0.00") + " wide, "
                    + local.size.y.ToString("0.00") + " high, " + local.size.z.ToString("0.00") + " deep).");
                return root.GetComponentsInChildren<Renderer>(true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Rebuild the School] The locker back could not be made: " + exception.Message);
                return renderers;
            }
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

        /// <summary>
        /// The renderers of the prop that some cell lists: the game's culling shows and hides the listed
        /// renderers with their chunk. The level builder and CullingManager.PrepareOcclusionCalculations both
        /// list an object's renderers, so one renderer can be listed twice, or in the list of another cell.
        /// </summary>
        private static List<Renderer> RegisteredRenderers(EnvironmentController environment, Renderer[] renderers)
        {
            List<Renderer> registered = new List<Renderer>();
            if (renderers == null)
            {
                return registered;
            }

            HashSet<Renderer> wanted = new HashSet<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    wanted.Add(renderers[i]);
                }
            }

            HashSet<Renderer> found = new HashSet<Renderer>();
            ScanRendererLists(environment, wanted, found, false);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && found.Contains(renderers[i]) && !registered.Contains(renderers[i]))
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

        /// <summary>Takes every listing of these renderers out of one cell's list (not just the first).</summary>
        private static void RemoveRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return;
            }

            ScanRendererList(cell, new HashSet<Renderer>(renderers), null, true);
        }

        /// <summary>
        /// Takes every listing of these renderers out of every cell. A listing left behind in the cell the prop
        /// came from would let that chunk's culling hide the model wherever the prop stands now.
        /// </summary>
        private static void RemoveRendererReferencesEverywhere(
            EnvironmentController environment, List<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return;
            }

            ScanRendererLists(environment, new HashSet<Renderer>(renderers), null, true);
        }

        /// <summary>
        /// Lists the renderers in the cell again. The culling only touches a listed renderer when its chunk
        /// changes between shown and hidden, so a prop put into a chunk takes that chunk's state at once.
        /// </summary>
        private static void AddRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (cell == null || cell.renderers == null || renderers == null)
            {
                return;
            }

            bool chunkShown = !cell.HasChunk || cell.Chunk == null || cell.Chunk.Rendering;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!cell.renderers.Contains(renderer))
                {
                    cell.renderers.Add(renderer);
                }

                renderer.enabled = chunkShown;
            }
        }

        /// <summary>
        /// Walks the renderer list of every cell: the renderers in `wanted` are reported in `found` and, with
        /// `remove`, taken out of the list (every occurrence; destroyed renderers go with them).
        /// </summary>
        private static void ScanRendererLists(
            EnvironmentController environment, HashSet<Renderer> wanted, HashSet<Renderer> found, bool remove)
        {
            if (environment == null || environment.cells == null || wanted.Count == 0)
            {
                return;
            }

            Cell[,] cells = environment.cells;
            int width = cells.GetLength(0);
            int depth = cells.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    ScanRendererList(cells[x, z], wanted, found, remove);
                }
            }
        }

        private static void ScanRendererList(
            Cell cell, HashSet<Renderer> wanted, HashSet<Renderer> found, bool remove)
        {
            if (cell == null || cell.renderers == null)
            {
                return;
            }

            List<Renderer> listed = cell.renderers;
            for (int i = listed.Count - 1; i >= 0; i--)
            {
                Renderer renderer = listed[i];
                if (renderer == null)
                {
                    if (remove)
                    {
                        // Chunk.Render would throw on it and skip the rest of the list.
                        listed.RemoveAt(i);
                    }

                    continue;
                }

                if (!wanted.Contains(renderer))
                {
                    continue;
                }

                if (found != null)
                {
                    found.Add(renderer);
                }

                if (remove)
                {
                    listed.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The coverage that the other props standing in this cell still provide: the EnvironmentObjects of the
        /// cell's room and the lockers on the cell's base. Only those two places are looked at (they are where such
        /// props live); asking Unity for every EnvironmentObject and every MeshRenderer of the scene on each pickup
        /// froze the game for a while on floors with many objects.
        /// </summary>
        private static CellCoverage OtherCoverageProviders(
            EnvironmentController environment,
            Cell cell,
            Transform excludedRoot,
            CellCoverage coverage)
        {
            CellCoverage provided = CellCoverage.None;
            if (cell == null || cell.room == null)
            {
                return provided;
            }

            if (cell.room.objectObject != null)
            {
                EnvironmentObject[] objects = cell.room.objectObject.GetComponentsInChildren<EnvironmentObject>(true);
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
            }

            if (cell.HasObjectBase)
            {
                MeshRenderer[] renderers = cell.ObjectBase.GetComponentsInChildren<MeshRenderer>(true);
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

        /// <summary>
        /// The pickup orientation. The side of the prop the crosshair is on is the horizontal local side (+Z, +X,
        /// -Z or -X) that best matches the surface under the crosshair (its normal; a surface that faces up or
        /// down says nothing, then the viewing direction decides). That side is turned toward the camera, so a
        /// locker or a vending machine aimed at from the front is carried with its front toward the player.
        /// sideYaw is the turn of that side against "local +Z faces the camera"; the carry keeps it
        /// (UpdateHeldFacing).
        /// </summary>
        private static Quaternion AimedSideRotation(
            Quaternion rotation, Vector3 towardCamera, Vector3 aimNormal, Quaternion fallback, out float sideYaw)
        {
            sideYaw = 0f;
            Vector3 flatView = Vector3.ProjectOnPlane(towardCamera, Vector3.up);
            if (flatView.sqrMagnitude < 0.001f)
            {
                return fallback;
            }

            flatView.Normalize();
            Vector3 flatNormal = Vector3.ProjectOnPlane(aimNormal, Vector3.up);
            Vector3 judged = flatNormal.sqrMagnitude >= 0.25f ? flatNormal.normalized : flatView;
            float bestFacing = -2f;
            int bestSide = 0;
            for (int side = 0; side < 4; side++)
            {
                Vector3 axis = rotation * (Quaternion.AngleAxis(side * 90f, Vector3.up) * Vector3.forward);
                axis.y = 0f;
                if (axis.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                float facing = Vector3.Dot(axis.normalized, judged);
                if (facing > bestFacing)
                {
                    bestFacing = facing;
                    bestSide = side;
                }
            }

            sideYaw = -90f * bestSide;
            return CameraFacingRotation(towardCamera, fallback) * Quaternion.AngleAxis(sideYaw, Vector3.up);
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
            public float AimDistance;
            public string AimSource;
            // The surface normal at the crosshair (zero when the aim came from a mesh box, not a collider).
            public Vector3 AimNormal;
        }

        /// <summary>A movable prop in the aim-assist registry, matched by the boxes of its meshes.</summary>
        private sealed class ProxyProp
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public bool HasSolidCollider;
            public Renderer[] Renderers;
            public Bounds[] LocalBounds;
            public bool[] HasLocalBounds;
            // Where the root was last seen: the queries that look at every prop test this, not the transform.
            public Vector3 Position;
            // The world box of the meshes, worked out once and kept until the prop moves (ProxyBox).
            public bool HasBox;
            public Bounds Box;
        }

        private struct ProxyContainer
        {
            public Transform Root;
            public int MaxDepth;
        }

        private struct ProxyNode
        {
            public Transform Node;
            public int Depth;
            public int MaxDepth;
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
            private const float ReapplyInterval = 0.15f;
            private bool _restored;
            private Color _appliedColor;
            private bool _hasAppliedColor;
            private float _appliedTime;

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
                    _lightObject = new GameObject(GlowObjectName);
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

                // The highlight is now refreshed every frame; only touch the property blocks when the
                // color changes or periodically (other systems may rewrite the blocks), not 60x a second.
                float now = Time.unscaledTime;
                if (_hasAppliedColor && color == _appliedColor && now - _appliedTime < ReapplyInterval)
                {
                    return;
                }

                _hasAppliedColor = true;
                _appliedColor = color;
                _appliedTime = now;
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
            // The turn that puts the side the player aimed at toward the camera (see AimedSideRotation).
            public float SideYaw;
            // Gravity Chaos: the turn-over of the carried prop (0 upright, 180 feet up) about CarryRollAxis, the
            // horizontal line toward the player. CarryRotation itself stays the upright, yaw-only pose.
            public float CarryRoll;
            public Vector3 CarryRollAxis;
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
            public bool Thrown;
            public int WallHits;
            // Characters this thrown prop has already pushed (each is pushed once per flight).
            public List<NPC> PushedNpcs;
            // The looping whoosh of a thrown prop: it follows the prop and is destroyed when the prop lands.
            public GameObject FlightSoundObject;
            public float NextImpactSoundTime;
            // Flight of a released prop (see StepFlight): the body is kinematic and moved from here.
            public Vector3 FlightPosition;
            public Vector3 FlightVelocity;
            public Vector3 FlightCenterOffset;
            public Vector3 FlightFullHalf;
            public Vector3 FlightHalf;
            public float FlightDrag;
            public float FlightFloorY;
            public bool Landed;
            // True while a landed prop is being settled: the placement then also tests the full-size box
            // against the walls and doors (see FlightBoxClear).
            public bool CheckFlightBox;
            // What the vanilla events did to this prop (g.Events.cs).
            public EventFx Fx;
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
                g runtime = Instance;
                if (runtime == null)
                {
                    return true;
                }

                try
                {
                    if (runtime.HandlePlayerClick(__instance))
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Rebuild the School] PlayerClick input hook failed: " + exception);
                    if (runtime._held != null)
                    {
                        return false;
                    }
                }

                // A prop was just thrown or placed: the click that did it must not also click on something the
                // game can see.
                if (Time.unscaledTime < _gameMouseBlockedUntil)
                {
                    __instance.clickedThisFrame = null;
                    return false;
                }

                return true;
            }
        }
    }
}
