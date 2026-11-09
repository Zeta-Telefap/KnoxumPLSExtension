using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// What happens to a prop right after the hands let go: the event hooks of release, settle and resume.
    /// </summary>
    public sealed partial class PropLifter
    {


        // ------------------------------------------------------------------------------------------
        // Hooks called by the core file (flight, release, landing)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// The state a released prop starts with. Gravity Chaos: a prop that was carried takes the gravity of the
        /// player who carried it. A player on the ceiling carries it turned over (feet up, CarryRoll), so it is
        /// let go that way and falls up to the ceiling; a player on the floor who took it off the ceiling lets it
        /// fall to the floor. A kick is the exception: the prop stays as it was (kept pose, own gravity) and flies
        /// along the way the player looks. A flipper turns any prop over later (FxTouchFlippers). Call it after
        /// BeginFlight, before the kick velocity.
        /// </summary>
        private void FxOnRelease(CarryState state, bool kick)
        {
            EventFx previous = state.Fx;
            EventFx fx = new EventFx();
            Quaternion pose = state.Root.rotation;
            fx.BaseRotation = pose;
            bool up;
            float roll;
            Vector3 axis;
            if (kick)
            {
                up = previous != null && (previous.Pinned || previous.GravityTarget < 0f);
                roll = previous != null ? previous.RollAngle : 0f;
                axis = previous != null ? previous.RollAxis : Vector3.zero;
            }
            else
            {
                up = PlayerIsFlipped(state.Player);
                roll = state.CarryRoll;
                axis = Mathf.Abs(roll) >= 0.05f ? state.CarryRollAxis : Vector3.zero;
            }

            // Without a roll yet, a flipper (or the end of the event) turns the prop about its own forward line.
            fx.RollAxis = axis.sqrMagnitude > 0.5f ? axis : pose * Vector3.forward;
            fx.RollAngle = roll;
            fx.RollOrigin = roll;
            state.Fx = fx;
            try
            {
                RefreshEventRegistries(false);
                if (up && _gravityEvent != null && _gravityEvent.Active)
                {
                    fx.GravityFactor = -1f;
                    fx.GravityTarget = -1f;
                    fx.KeepLanding = true;
                }
            }
            catch (Exception exception)
            {
                LogEventError("release", exception);
            }
        }

        /// <summary>A prop in a special state is not sent home by the release timeout.</summary>
        private static bool FxKeepsAirborne(CarryState state)
        {
            EventFx fx = state.Fx;
            if (fx == null || !(fx.Pinned || fx.Riding || fx.Sinking || fx.Pulled || fx.Craned || fx.OnBelt))
            {
                return false;
            }

            fx.SpecialSeconds += Time.deltaTime;
            return fx.SpecialSeconds < EventSpecialMaxSeconds;
        }

        private static void FxBeforeSettle(CarryState state)
        {
            EventFx fx = state.Fx;
            if (fx != null && fx.KeepLanding)
            {
                // A prop that went through an event settles where it ended up, like a thrown one.
                state.Thrown = true;
            }
        }

        /// <summary>
        /// Inside a whirlpool's pull (or going down it), or on a running conveyor belt, a prop on the floor slides
        /// instead of settling.
        /// </summary>
        private static bool FxHoldsOnFloor(CarryState state)
        {
            EventFx fx = state.Fx;
            return fx != null && (fx.Sinking || fx.Pulled || fx.OnBelt);
        }

        /// <summary>
        /// Riding a student, sinking into a whirlpool and hanging in a crane's claw replace the flight; true when
        /// this step was handled here (StepFlight returns), false for an ordinary step.
        /// </summary>
        private bool FxStepSpecial(CarryState state, float dt)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return false;
            }

            try
            {
                if (fx.Sinking)
                {
                    return FxStepSink(state, fx, dt);
                }

                if (fx.Craned)
                {
                    return FxStepCrane(state, fx, dt);
                }

                if (fx.Riding || _followers.Length > 0)
                {
                    return FxStepRide(state, fx, dt);
                }
            }
            catch (Exception exception)
            {
                LogEventError("special flight step", exception);
                fx.Riding = false;
                fx.Follower = null;
                FxAbortSink(state);
                FxAbortCrane(state);
            }

            return false;
        }

        /// <summary>
        /// A prop that leaves the whirlpool ride half way (error, level change, sent home) lets go of it and closes
        /// the exit pool that was opened for it.
        /// </summary>
        private static void FxAbortSink(CarryState state)
        {
            EventFx fx = state != null ? state.Fx : null;
            if (fx == null)
            {
                return;
            }

            fx.Sinking = false;
            fx.SinkRising = false;
            fx.SinkPool = null;
            // The other whirlpool of the flood is left as it is (only a pool made for this prop is closed).
            fx.ExitOther = null;
            Whirlpool exit = fx.ExitPool;
            fx.ExitPool = null;
            try
            {
                if (exit != null)
                {
                    exit.StartClose();
                }
            }
            catch (Exception)
            {
                // The pool is gone already.
            }
        }

        /// <summary>The prop turns over while its gravity changes; FlightPosition stays the unturned pose.</summary>
        private void FxApplyPose(CarryState state, Vector3 c)
        {
            EventFx fx = state.Fx;
            float turn = fx != null ? fx.RollAngle - fx.RollOrigin : 0f;
            if (fx == null || (Mathf.Abs(turn) < 0.05f && !fx.RollApplied))
            {
                state.Body.MovePosition(state.FlightPosition);
                return;
            }

            Quaternion roll = Quaternion.AngleAxis(turn, fx.RollAxis);
            state.Body.MovePosition(c - roll * state.FlightCenterOffset);
            state.Body.MoveRotation(roll * fx.BaseRotation);
            fx.RollApplied = Mathf.Abs(turn) >= 0.05f;
        }

        private void FxNotePinned(CarryState state, bool pinned, Vector3 c)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return;
            }

            if (pinned && !fx.Pinned)
            {
                fx.Pinned = true;
                fx.KeepLanding = true;
                StopFlightSound(state);
                PlaySoundAt(c, FloorSoundName, FloorSoundVolume);
                // A prop on the ceiling is as solid as one on the floor (the player cannot walk through it) ...
                FxSetSolid(state, true);
            }
            else if (!pinned && fx.Pinned)
            {
                // ... and ignores the player again when it falls.
                fx.Pinned = false;
                FxSetSolid(state, false);
            }
        }

        /// <summary>
        /// A prop that stands still in the air (pinned to the ceiling, riding a student) is a target again, not a
        /// flying prop: the aim sees it and G or the wheel click takes it (TryResumeCarry).
        /// </summary>
        private static bool FxResumable(CarryState state)
        {
            return state != null && state.Fx != null && (state.Fx.Pinned || state.Fx.Riding);
        }

        /// <summary>True for a prop in the list of released props, whatever it is doing (flying, pinned, riding).</summary>
        private bool IsReleased(Transform root)
        {
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state != null && state.Root == root)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Solid: the player collides with the prop and its mesh colliders work, as they do for a prop that lies
        /// in the world. Not solid (flight, carry): the player's colliders are ignored and the non-convex mesh
        /// colliders of the body stay off (BeginCarry).
        /// </summary>
        private static void FxSetSolid(CarryState state, bool solid)
        {
            try
            {
                // Both ways start from a clean state: no ignored pairs are left over to be listed twice.
                RestorePlayerCollisions(state);
                if (solid)
                {
                    RestoreNonConvex(state.NonConvexColliders);
                    return;
                }

                IgnorePlayerCollisions(state);
                List<MeshCollider> nonConvex = state.NonConvexColliders;
                for (int i = 0; nonConvex != null && i < nonConvex.Count; i++)
                {
                    if (nonConvex[i] != null)
                    {
                        nonConvex[i].enabled = false;
                    }
                }
            }
            catch (Exception exception)
            {
                LogEventError("solidity of a pinned prop", exception);
            }
        }

        /// <summary>
        /// A pinned (or riding) prop is still in the list of airborne props as far as the flight is concerned.
        /// BeginCarry asks here first: picking it up (or kicking it) takes that state out of the air and carries it
        /// on, so the prop keeps its origin, coverage and renderer bookkeeping; releasing it creates a fresh event
        /// state (FxOnRelease). `resumable` tells whether the prop was such a one; the result is its carry state
        /// (null: it failed and went back to where it came from). Any other prop: the ordinary pickup follows.
        /// </summary>
        private CarryState TryResumeCarry(
            Candidate candidate, PlayerManager player, Ray ray, bool keepRotation, out bool resumable)
        {
            resumable = false;
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState found = _airborne[i];
                if (found == null || found.Root != candidate.Root || !FxResumable(found))
                {
                    continue;
                }

                resumable = true;
                _airborne.RemoveAt(i);
                try
                {
                    if (found.Body == null)
                    {
                        throw new InvalidOperationException("The pinned prop has lost its body.");
                    }

                    ResumeFromAir(found, candidate, ray, keepRotation);
                }
                catch (Exception exception)
                {
                    LogEventError("picking up a pinned prop", exception);
                    ReturnToOrigin(found);
                    return null;
                }

                return found;
            }

            return null;
        }

        private static void ResumeFromAir(CarryState state, Candidate candidate, Ray ray, bool keepRotation)
        {
            EventFx fx = state.Fx;
            StopFlightSound(state);

            // The pose the prop shows now. On the ceiling it is turned over: fx.RollAngle about the roll axis.
            Quaternion shown = state.Root.rotation;
            float roll = fx != null ? fx.RollAngle : 0f;
            Vector3 ownAxis = fx != null && fx.RollAxis.sqrMagnitude > 0.5f ? fx.RollAxis : shown * Vector3.forward;
            float sideYaw = 0f;
            Quaternion carryRotation;
            if (keepRotation)
            {
                // A kick leaves the prop exactly as it stands (no turn toward the camera, no snap upright); the
                // upright pose under its roll is the one it lands in when its gravity returns.
                carryRotation = Mathf.Abs(roll) < 0.05f ? shown : Quaternion.AngleAxis(-roll, ownAxis) * shown;
                state.CarryRotation = carryRotation;
                state.CarryRoll = 0f;
                state.CarryRollAxis = ownAxis;
            }
            else
            {
                // The carry goes on from the pose the prop shows: the upright (yaw only) pose, turned by the roll
                // about the line toward the player. The carry then turns it over or upright by itself, with the
                // player's own gravity (UpdateHeldFacing): a ceiling prop taken by a player on the floor comes down,
                // one taken by a player on the ceiling stays up.
                Vector3 toward = Vector3.ProjectOnPlane(-ray.direction, Vector3.up);
                Vector3 axis = toward.sqrMagnitude > 0.001f ? toward.normalized : ownAxis;
                Quaternion unrolled = Mathf.Abs(roll) < 0.05f ? shown : Quaternion.AngleAxis(-roll, axis) * shown;
                carryRotation = AimedSideRotation(shown, -ray.direction, candidate.AimNormal, unrolled, out sideYaw);
                state.CarryRotation = unrolled;
                state.CarryRoll = roll;
                state.CarryRollAxis = axis;
            }

            state.Landed = false;
            state.Thrown = false;
            state.FlightVelocity = Vector3.zero;
            state.PositionSmoothVelocity = Vector3.zero;
            state.ManualRotationSteps = 0;
            state.SideYaw = sideYaw;
            state.CameraRotation = carryRotation;
            state.TargetRotation = carryRotation;
            if (state.EnvironmentObject != null)
            {
                state.Coverage = state.EnvironmentObject.coverage.Rotated(DirectionFromRotation(carryRotation));
            }
            else if (state.IsLocker)
            {
                state.Coverage = LockerCoverage(carryRotation);
            }

            Bounds bounds;
            if (CombinedBounds(state.Renderers, out bounds))
            {
                state.LocalGrabPoint = VisualCenterAnchor(state.Root, bounds);
            }

            FxSetSolid(state, false);
            RestoreVisualGlow(state);
            state.Glow = new GlowState(state.Root, state.Renderers);
            state.Glow.SetColor(CarryHighlightColor);
        }

        // --- Student Shuffle ---

        /// <summary>
        /// A flying prop that comes within reach of a student of the crowd, below the student's head, is lifted
        /// onto the head and rides with it: the position follows the student, so the prop goes wherever the
        /// crowd goes. When the student is gone (the event ends) the prop is released with the student's speed.
        /// </summary>
        private bool FxStepRide(CarryState state, EventFx fx, float dt)
        {
            Vector3 c = state.FlightPosition + state.FlightCenterOffset;
            Vector3 half = state.FlightFullHalf;
            if (fx.Riding && fx.Follower == null)
            {
                fx.Riding = false;
                Vector3 release = fx.RideVelocity;
                release.y = 0f;
                state.FlightVelocity = release;
                Debug.Log("[Lifter Frenzy] The student is gone; " + state.Root.name + " falls.");
                return false;
            }

            if (!fx.Riding)
            {
                // A prop whose gravity points up (Gravity Chaos) does not come down on a head.
                if (fx.Pinned || fx.GravityFactor < 0.5f || fx.RideBlocked)
                {
                    return false;
                }

                StudentCrowd_Follower follower = FindFollowerUnder(state.Environment, c, half);
                if (follower == null)
                {
                    return false;
                }

                Vector3 start = follower.transform.position;
                fx.Riding = true;
                fx.Follower = follower;
                fx.KeepLanding = true;
                fx.RideOffset = new Vector3(c.x - start.x, 0f, c.z - start.z);
                fx.RideLast = start;
                fx.RideVelocity = Vector3.zero;
                StopFlightSound(state);
                Debug.Log("[Lifter Frenzy] " + state.Root.name + " landed on a student and rides on its head.");
            }

            Vector3 head = fx.Follower.transform.position;
            Vector3 speed = (head - fx.RideLast) / Mathf.Max(dt, 0.0001f);
            fx.RideLast = head;
            if (speed.magnitude < FlightMaxSpeed)
            {
                fx.RideVelocity = Vector3.Lerp(fx.RideVelocity, speed, 0.5f);
            }

            fx.RideOffset = Vector3.MoveTowards(fx.RideOffset, Vector3.zero, CrowdCenterSpeed * dt);
            float top = state.FlightFloorY + FlightCeilingHeight - half.y - 0.05f;
            float wanted = Mathf.Min(head.y + _crowdHeadOffset + half.y, top);
            float y = Mathf.MoveTowards(c.y, wanted, CrowdLiftSpeed * dt);
            Vector3 next = new Vector3(head.x + fx.RideOffset.x, y, head.z + fx.RideOffset.z);
            if (BoxTouchesEventSpawner(state.Environment, next, half))
            {
                // The crowd goes into its spawner room (or comes out of it): no prop goes there. This one stays where
                // it is, falls, and does not ride again.
                fx.Riding = false;
                fx.Follower = null;
                fx.RideBlocked = true;
                state.FlightVelocity = Vector3.zero;
                Debug.Log("[Lifter Frenzy] The student walks into an event spawner room; " + state.Root.name
                    + " does not follow and falls.");
                return false;
            }

            state.FlightPosition = next - state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;
            state.Body.MovePosition(state.FlightPosition);
            return true;
        }
    }
}
