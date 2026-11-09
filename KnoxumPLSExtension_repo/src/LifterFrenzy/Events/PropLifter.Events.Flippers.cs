using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Gravity Chaos: the flippers and the gravity a prop takes.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Gravity Chaos.
        private const float GravityFlipRate = 1.6f;
        private const float GravityRollRate = 360f;
        private const float GravityLandFactor = 0.9f;
        // The flippers the gravity event keeps in a private list (they are children of floor tiles, not of the event).
        private static readonly FieldInfo GravityFlippersField = typeof(GravityEvent).GetField(
            "flippers", BindingFlags.Instance | BindingFlags.NonPublic);
        private GravityEvent _gravityEvent;
        private bool _gravityRunning;
        private GravityFlipper[] _flippers = new GravityFlipper[0];
        private Bounds[] _flipperBounds = new Bounds[0];

        private void RefreshFlippers()
        {
            List<GravityFlipper> list = null;
            if (_gravityEvent != null)
            {
                if (GravityFlippersField != null)
                {
                    try
                    {
                        list = GravityFlippersField.GetValue(_gravityEvent) as List<GravityFlipper>;
                    }
                    catch (Exception)
                    {
                        list = null;
                    }
                }

                if (list == null)
                {
                    // The field is gone in this version of the game: search, but only while the event runs.
                    list = new List<GravityFlipper>(FindObjectsOfType<GravityFlipper>());
                }
            }

            int count = list != null ? list.Count : 0;
            LogEventObjects("Gravity Chaos: gravity flippers", count, ref _loggedFlippers);
            _flippers = new GravityFlipper[count];
            _flipperBounds = new Bounds[count];
            for (int i = 0; i < count; i++)
            {
                GravityFlipper flipper = list[i];
                _flippers[i] = flipper;
                if (flipper == null)
                {
                    continue;
                }

                Bounds bounds;
                if (!TryGetOwnerBounds(flipper, true, out bounds) && !TryGetOwnerBounds(flipper, false, out bounds))
                {
                    bounds = new Bounds(flipper.transform.position, Vector3.one * 5f);
                }

                bounds.Expand(0.5f);
                _flipperBounds[i] = bounds;
            }
        }

        private static bool PlayerIsFlipped(PlayerManager player)
        {
            try
            {
                return player != null && player.plm != null && player.plm.Entity != null
                    && player.plm.Entity.Flipped;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // --- Gravity Chaos ---

        /// <summary>
        /// +1 = gravity pulls down, -1 = up. A flipper the prop touches is used up (GravityEvent.DestroyFlipper,
        /// like the player and the characters do) and turns the prop over at once (FxTouchFlippers); when the
        /// event ends gravity returns to normal, smoothly.
        /// </summary>
        private float FxGravityScale(CarryState state, Vector3 c, Vector3 half, float dt)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return 1f;
            }

            try
            {
                bool active = _gravityEvent != null && _gravityEvent.Active;
                // A prop as tall as the room has no ceiling to fall to.
                bool fits = 2f * half.y + 0.2f < FlightCeilingHeight;
                if (active && fits)
                {
                    FxTouchFlippers(fx, c, half);
                }
                else if (!active)
                {
                    fx.GravityTarget = 1f;
                }

                float target = active && fits ? fx.GravityTarget : 1f;
                fx.GravityFactor = Mathf.MoveTowards(fx.GravityFactor, target, GravityFlipRate * dt);
                fx.RollAngle = Mathf.MoveTowards(
                    fx.RollAngle, fx.GravityFactor < 0f ? 180f : 0f, GravityRollRate * dt);
                return fx.GravityFactor;
            }
            catch (Exception exception)
            {
                LogEventError("gravity", exception);
                fx.GravityFactor = 1f;
                fx.GravityTarget = 1f;
                return 1f;
            }
        }

        private void FxTouchFlippers(EventFx fx, Vector3 c, Vector3 half)
        {
            if (_flippers.Length == 0)
            {
                return;
            }

            Bounds box = new Bounds(c, half * 2f);
            for (int i = 0; i < _flippers.Length; i++)
            {
                GravityFlipper flipper = _flippers[i];
                if (flipper == null || !box.Intersects(_flipperBounds[i]))
                {
                    continue;
                }

                fx.GravityTarget = fx.GravityTarget > 0f ? -1f : 1f;
                // At once: gravity and the pose go to the new side in this very step (like Entity.Flip of a
                // character), no ramp. The ramps in FxGravityScale only carry the prop back when the event ends.
                fx.GravityFactor = fx.GravityTarget;
                fx.RollAngle = fx.GravityTarget < 0f ? 180f : 0f;
                fx.KeepLanding = true;
                _flippers[i] = null;
                _flipperBounds[i] = new Bounds();
                _gravityEvent.DestroyFlipper(flipper);
                Debug.Log("[Lifter Frenzy] A prop touched a gravity flipper; its gravity now turns "
                    + (fx.GravityTarget < 0f ? "up" : "down") + ".");
                return;
            }
        }
    }
}
