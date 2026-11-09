using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Conveyor belts: the push a prop gets from a belt and the chain a belt leaves behind.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Conveyor belts (BeltManager): a prop whose underside is this near the floor stands on it; a belt that is
        // slower than this carries nothing.
        private const float BeltFloorTolerance = 0.25f;
        private const float BeltMinSpeed = 0.5f;

        // A conveyor belt's MovementModifier: its movementAddend (direction * speed) is the push the game gives a
        // character that stands on the belt, zero while the belt is stopped.
        private static readonly FieldInfo BeltMoveModField = typeof(BeltManager).GetField(
            "moveMod", BindingFlags.Instance | BindingFlags.NonPublic);
        // The conveyor belts and the cranes of the floor (EnsureLevelFixtures), looked for once per floor.
        private BeltManager[] _belts = new BeltManager[0];

        /// <summary>
        /// The push of the running belts under the prop: BeltManager's movementAddend (direction * speed, the push
        /// the game adds to a character's movement on a belt; zero while a belt is stopped). A belt acts on the
        /// prop's footprint at any height, as its trigger acts on a character. `overBelt` tells whether the prop
        /// stands over any belt at all (running or not), which is what a chain of belts needs.
        /// </summary>
        private bool FxBeltRunningPush(Vector3 c, Vector3 half, out Vector3 push, out bool overBelt)
        {
            push = Vector3.zero;
            overBelt = false;
            if (_belts.Length == 0 || BeltMoveModField == null)
            {
                return false;
            }

            Bounds box = new Bounds(c, half * 2f);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _belts.Length; i++)
            {
                BeltManager belt = _belts[i];
                BoxCollider zone = belt != null && belt.isActiveAndEnabled ? belt.BoxCollider : null;
                if (zone == null || !zone.enabled)
                {
                    continue;
                }

                Bounds area = zone.bounds;
                if (box.max.x <= area.min.x || box.min.x >= area.max.x
                    || box.max.z <= area.min.z || box.min.z >= area.max.z)
                {
                    continue;
                }

                overBelt = true;
                MovementModifier beltPush = BeltMoveModField.GetValue(belt) as MovementModifier;
                if (beltPush != null)
                {
                    sum.x += beltPush.movementAddend.x;
                    sum.z += beltPush.movementAddend.z;
                }
            }

            if (sum.magnitude < BeltMinSpeed)
            {
                return false;
            }

            push = sum;
            return true;
        }

        /// <summary>
        /// Nothing (a wall, a closed door that does not give way, a prop) stands across the way the belt pushes the
        /// prop: the prop rides on. A door that gives way (a swinging one) opens for the prop here, as in flight.
        /// </summary>
        private bool FxBeltPathClear(CarryState state, Vector3 c, Vector3 half, Vector3 push, float dt)
        {
            FlightHit blocker;
            return !FindFlightHit(state, c, half, push * (dt * 1.5f), out blocker);
        }

        /// <summary>
        /// The way the belts carry the prop now, or zero. The belts under the prop push it with their own
        /// direction and speed (several belts add up). A chain of belts is usually one belt at a time: the belt
        /// room starts the belt the player stands at and stops the others, so the belt after a seam is standing
        /// still; a prop that comes from a running belt keeps the way it was going over such a belt (fx.BeltPush),
        /// so a chain carries it on instead of stopping it at the seam. Off every belt, it is forgotten. Sets
        /// fx.OnBelt for the step.
        /// </summary>
        private Vector3 FxBeltCurrent(CarryState state, Vector3 c, Vector3 half, float dt)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return Vector3.zero;
            }

            Vector3 push;
            bool overBelt;
            bool running = FxBeltRunningPush(c, half, out push, out overBelt);
            if (!running)
            {
                if (!overBelt)
                {
                    // Off the belts: the way the last belt went is forgotten.
                    fx.BeltPush = Vector3.zero;
                    return Vector3.zero;
                }

                if (fx.BeltPush.sqrMagnitude < BeltMinSpeed * BeltMinSpeed)
                {
                    return Vector3.zero;
                }

                // A still belt of the chain ahead: the prop goes on the way the belt it came from sent it.
                push = fx.BeltPush;
            }

            if (!FxBeltPathClear(state, c, half, push, dt))
            {
                // Something blocks the way: the prop does not ride on (it stops against it and settles there).
                fx.BeltPush = Vector3.zero;
                return Vector3.zero;
            }

            fx.BeltPush = push;
            fx.OnBelt = true;
            fx.KeepLanding = true;
            LogOnBelt(state, fx);
            return push;
        }

        /// <summary>
        /// Conveyor belts, at the start of a flight step: a prop that stands on the floor over a running belt is
        /// carried by it. The push is a drift on top of the velocity (the caller adds it and takes it off again after
        /// the step, like the whirlpool's pull) and the prop's own speed is dropped by the caller: on a belt it moves
        /// at the belt's speed and in its direction, nothing else. While it lasts the prop does not settle
        /// (FxHoldsOnFloor): it rides until it is completely off the belts and lands where it is.
        /// </summary>
        private Vector3 FxBeltDrift(CarryState state, Vector3 c, Vector3 half, float dt)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return Vector3.zero;
            }

            fx.OnBelt = false;
            if (_belts.Length == 0 || c.y - half.y > state.FlightFloorY + BeltFloorTolerance)
            {
                return Vector3.zero;
            }

            try
            {
                return FxBeltCurrent(state, c, half, dt);
            }
            catch (Exception exception)
            {
                LogEventError("conveyor belt", exception);
                return Vector3.zero;
            }
        }

        /// <summary>
        /// A prop that comes down on a belt does not settle there: it stops against the belt (a landing stops a
        /// prop too) and the belt takes it from the next step on (FxBeltDrift). `v` gets the belt's way for the rest
        /// of this step and `drift` grows by it, so the caller takes it off again at the end of the step; true when
        /// the prop came down on a belt.
        /// </summary>
        private bool FxBeltLanding(
            CarryState state, Vector3 c, Vector3 half, float dt, ref Vector3 v, ref Vector3 drift)
        {
            if (state.Fx == null || _belts.Length == 0)
            {
                return false;
            }

            try
            {
                Vector3 push = FxBeltCurrent(state, c, half, dt);
                if (push.sqrMagnitude < BeltMinSpeed * BeltMinSpeed)
                {
                    return false;
                }

                // On the belt the prop moves exactly at the belt's speed: its own speed is gone.
                v.x = drift.x + push.x;
                v.z = drift.z + push.z;
                drift += push;
                return true;
            }
            catch (Exception exception)
            {
                LogEventError("conveyor belt", exception);
                return false;
            }
        }

        private static void LogOnBelt(CarryState state, EventFx fx)
        {
            if (fx.BeltLogged)
            {
                return;
            }

            fx.BeltLogged = true;
            Debug.Log("[Lifter Frenzy] " + state.Root.name
                + " is on a running conveyor belt and rides it until it is completely off the belts.");
        }
    }
}
