using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The wheel-click kick.
    /// </summary>
    public sealed partial class PropLifter
    {
        // ------------------------------------------------------------------------------------------
        // Tuning
        // ------------------------------------------------------------------------------------------

        // Wheel click: the prop under the crosshair is kicked like an RMB throw (same speed, push and sounds).
        private const float KickRange = 14f;
        private const float KickLift = 5f;
        private const float KickUpward = 0.12f;

        // ------------------------------------------------------------------------------------------
        // Wheel click: kick
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Not carrying: the prop under the crosshair is kicked where it stands. It is not turned and keeps its
        /// full-size collision box (BeginCarry keepRotation, BeginFlight atRest), then flies like an RMB throw
        /// (speed, push of a hit character, Bullying rule, Whoosh and Bang) and settles through the same landing
        /// as every throw. Carrying: the wheel click throws the carried prop (see HandlePlayerClick).
        /// </summary>
        private void TryKick()
        {
            Candidate target = ResolveAimThisFrame(_player);
            if (target == null || target.Root == null || target.AimDistance > KickRange
                || !CanBeginCarry(target, _player))
            {
                return;
            }

            Ray ray;
            if (!TryGetCameraRay(_player, out ray))
            {
                return;
            }

            ClearPickupHighlight();
            CarryState state;
            // keepRotation: the prop is not turned toward the camera first (see BeginCarry).
            if (!BeginCarry(target, _player, ray, out state, true))
            {
                return;
            }

            _held = state;
            ReleaseHeld(true, true);
        }

        /// <summary>
        /// A prop lying on the floor would land again in the very first step, so the kick goes a little away from
        /// the surface it lies on and never into it. That is up for a prop on the floor and down for one that is
        /// pinned to the ceiling (Gravity Chaos), whichever way the player's own gravity points: a player on the
        /// ceiling looks "down" at the floor props, and the kick still goes along the way he looks, level.
        /// </summary>
        private static Vector3 KickVelocity(CarryState state, Ray ray, bool fromCeiling)
        {
            Vector3 away = fromCeiling ? Vector3.down : Vector3.up;
            Vector3 direction = ray.direction;
            if (Vector3.Dot(direction, away) < 0f)
            {
                // Aimed toward the surface the prop rests on: kick along the heading, level.
                direction = Vector3.ProjectOnPlane(direction, away);
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector3.ProjectOnPlane(state.Player.transform.forward, away);
                }

                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector3.forward;
                }
            }

            direction = (direction.normalized + away * KickUpward).normalized;
            return PlayerVelocity(state.Player) * 0.35f + direction * ThrowSpeed + away * KickLift;
        }
    }
}
