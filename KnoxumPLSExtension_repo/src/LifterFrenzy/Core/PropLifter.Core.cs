using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The runtime object of Lifter Frenzy: lifecycle, and the shared cell / ray / bounds / mass helpers that every
    /// other part uses. The parts of the class are split over the Core, Carry, Placement, World and Events folders;
    /// together they are the one MonoBehaviour the plugin adds.
    /// </summary>
    public sealed partial class PropLifter : MonoBehaviour
    {


        public static PropLifter Instance { get; private set; }

        /// <summary>
        /// True while a prop is carried and for GameMouseBlockAfterRelease seconds after it was thrown or placed.
        /// The game then ignores LMB (PlayerClick), RMB (ItemManager.UseItem) and the wheel (item selection).
        /// This mod reads the mouse itself, through UnityEngine.Input, and is not affected.
        /// </summary>
        internal static bool GameMouseBlocked
        {
            get
            {
                PropLifter props = Instance;
                return Time.unscaledTime < _gameMouseBlockedUntil || (props != null && props._held != null);
            }
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
            Debug.Log("[Lifter Frenzy] Carry runtime started. Controls: G, LMB, RMB, wheel click (kick), R + mouse wheel.");
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

            // Wheel-click kick, event squishers, characters that walk around props (the Events files).
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
    }
}
