using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The throw: a prop never becomes an ordinary flying rigidbody. Its speed is a velocity the mod steps by hand,
    /// so the flight can be swept.
    /// </summary>
    public sealed partial class PropLifter
    {


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
                            Debug.LogWarning("[Lifter Frenzy] A prop came to rest outside every room; it went back to where it came from.");
                            ReturnToOrigin(state);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[Lifter Frenzy] Settling a landed prop failed; it went back to where it came from: " + exception);
                        ReturnToOrigin(state);
                    }
                }
                else if (state.Age >= ReleaseTimeout)
                {
                    Debug.LogWarning("[Lifter Frenzy] A prop was still airborne after "
                        + ReleaseTimeout + " s; it went back to where it came from.");
                    ReturnToOrigin(state);
                    _airborne.RemoveAt(i);
                }
            }
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
                Debug.Log("[Lifter Frenzy] Released next to a wall: the prop was pulled "
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
                    EnsureLevelFixtures(state.Environment);
                    StepFlight(state, dt);
                }
                catch (Exception exception)
                {
                    if (Time.unscaledTime >= _nextSweepErrorLogTime)
                    {
                        _nextSweepErrorLogTime = Time.unscaledTime + 5f;
                        Debug.LogError("[Lifter Frenzy] Thrown-prop flight step failed: " + exception);
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
            // Riding a student or sinking into a whirlpool replace the flight (the Events files).
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
            // Conveyor belts: a prop that stands on a belt is carried along with the belt's speed and direction
            // only (its own speed is dropped), and the push is not the speed it hits a character with.
            Vector3 belt = FxBeltDrift(state, c, half, dt);
            if (state.Fx != null && state.Fx.OnBelt)
            {
                v.x = 0f;
                v.z = 0f;
            }

            drift += belt;
            v += drift;
            Vector3 impactVelocity = v - belt;
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
                    if (FxHoldsOnFloor(state) || FxBeltLanding(state, c, half, dt, ref v, ref drift))
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
                if (!landed && !pinned)
                {
                    HitCranes(state, stepStart, c, half);
                }
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

        private sealed class RayHitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
