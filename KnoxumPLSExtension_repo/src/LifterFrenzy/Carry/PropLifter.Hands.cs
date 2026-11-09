using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The hands: pickup, the held pose, the rotation and the release.
    /// </summary>
    public sealed partial class PropLifter
    {


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

                // Same fresh aim query as PropLifter.Update. With no prop under the crosshair this returns
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
                    Debug.LogWarning("[Lifter Frenzy] Pickup blocked: player, environment or target is not ready.");
                    return;
                }

                _player = player;
                Ray ray;
                if (!TryGetCameraRay(player, out ray))
                {
                    Debug.LogWarning("[Lifter Frenzy] Pickup blocked: camera ray is unavailable.");
                    return;
                }

                string targetName = DescribePath(candidate.Root);
                if (!CanBeginCarry(candidate, player))
                {
                    Debug.LogWarning("[Lifter Frenzy] Selected prop is not currently movable: " + targetName);
                    return;
                }

                // Restore hover blocks before constructing the carry glow; otherwise the carry
                // state would capture the temporary hover tint as its supposed original state.
                ClearPickupHighlight();
                CarryState state;
                if (!BeginCarry(candidate, player, ray, out state))
                {
                    Debug.LogWarning("[Lifter Frenzy] Pickup setup rejected: " + targetName + ".");
                    return;
                }

                _held = state;
                InvalidateAim();
                Debug.Log("[Lifter Frenzy] G picked up " + targetName + " ("
                    + (candidate.AimSource ?? "aim") + ", " + candidate.AimDistance.ToString("0.0")
                    + " units); visual center locked to the camera orbit.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Lifter Frenzy] Pickup failed: " + exception);
            }
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
            // carrying it again goes on with that state (the Events files).
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
                    ? "[Lifter Frenzy] Prop kicked (wheel click); it flies like a thrown one."
                    : throwForward
                        ? "[Lifter Frenzy] Prop thrown; it flies on a swept path that cannot pass walls, doors or the floor."
                        : "[Lifter Frenzy] Prop dropped; it falls on a swept path and settles on contact.");
            }
            catch (Exception exception)
            {
                ReturnToOrigin(state);
                _held = null;
                Debug.LogError("[Lifter Frenzy] Release failed; object returned to origin: " + exception);
            }
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
    }
}
