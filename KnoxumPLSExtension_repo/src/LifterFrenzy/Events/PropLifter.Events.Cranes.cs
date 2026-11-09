using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The cranes that pick a thrown prop out of the air.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Cranes (CraneController): the heights and waits of its own CarryEntityToTarget.
        private const float CraneLowHeight = 5f;
        private const float CraneHighHeight = 12.5f;
        private const float CraneClawSeconds = 1.5f;
        private const float CranePauseSeconds = 0.5f;
        private const float CraneLiftSpeedFallback = 3f;
        private const float CraneDropSpeedFallback = 10f;
        // What CraneController.EntityTriggerEnter and CarryEntityToTarget use (all private).
        private static readonly FieldInfo CraneBalloonField = typeof(CraneController).GetField(
            "balloon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneNavigatorField = typeof(CraneController).GetField(
            "navigator", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneRoomFunctionField = typeof(CraneController).GetField(
            "craneRoom", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneAudioField = typeof(CraneController).GetField(
            "audioManager", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneRunningLoopField = typeof(CraneController).GetField(
            "audRunningLoop", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneLiftingLoopField = typeof(CraneController).GetField(
            "audLiftingLoop", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneThunkField = typeof(CraneController).GetField(
            "audThunk", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneLiftSpeedField = typeof(CraneController).GetField(
            "liftSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneDropSpeedField = typeof(CraneController).GetField(
            "dropSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CraneCarryingField = typeof(CraneController).GetField(
            "carryingEntity", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CraneCloseClawMethod = typeof(CraneController).GetMethod(
            "CloseClaw", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CraneOpenClawMethod = typeof(CraneController).GetMethod(
            "OpenClaw", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CraneTargetCellMethod = typeof(CraneController).GetMethod(
            "GetTargetCellFromOutputCell", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A prop in a crane's claw. CraneCarryProp (the crane's sequence) keeps the height up to date.</summary>
        private sealed class CraneCarry
        {
            public CraneController Crane;
            // The height of the claw above the floor, as CraneController.CarryEntityToTarget counts it (Entity
            // height: 5 hanging, 12.5 lifted).
            public float Height;
            // The claw opened: from here on the prop falls, at this speed to begin with (the crane's dropSpeed).
            public bool Released;
            public float DropSpeed;
        }

        /// <summary>The private parts of one crane that its sequence needs, taken once.</summary>
        private sealed class CraneHandle
        {
            public CraneController Crane;
            public Balloon Balloon;
            public Navigator Navigator;
            public CraneRoomFunction Room;
            public AudioManager Audio;
            public SoundObject RunningLoop;
            public SoundObject LiftingLoop;
            public SoundObject Thunk;
            public float LiftSpeed;
            public float DropSpeed;
        }
        private CraneController[] _cranes = new CraneController[0];
        private readonly Dictionary<CraneController, CraneHandle> _craneHandles =
            new Dictionary<CraneController, CraneHandle>();

        // --- cranes ---

        private CraneHandle GetCraneHandle(CraneController crane)
        {
            CraneHandle handle;
            if (_craneHandles.TryGetValue(crane, out handle))
            {
                return handle;
            }

            handle = BuildCraneHandle(crane);
            _craneHandles[crane] = handle;
            return handle;
        }

        /// <summary>The private parts of a crane that its sequence needs; null when this game's crane does not have them.</summary>
        private static CraneHandle BuildCraneHandle(CraneController crane)
        {
            try
            {
                if (CraneBalloonField == null || CraneNavigatorField == null || CraneRoomFunctionField == null
                    || CraneAudioField == null || CraneRunningLoopField == null || CraneLiftingLoopField == null
                    || CraneThunkField == null || CraneCarryingField == null || CraneCloseClawMethod == null
                    || CraneOpenClawMethod == null || CraneTargetCellMethod == null)
                {
                    return null;
                }

                CraneHandle handle = new CraneHandle();
                handle.Crane = crane;
                handle.Balloon = CraneBalloonField.GetValue(crane) as Balloon;
                handle.Navigator = CraneNavigatorField.GetValue(crane) as Navigator;
                handle.Room = CraneRoomFunctionField.GetValue(crane) as CraneRoomFunction;
                handle.Audio = CraneAudioField.GetValue(crane) as AudioManager;
                handle.RunningLoop = CraneRunningLoopField.GetValue(crane) as SoundObject;
                handle.LiftingLoop = CraneLiftingLoopField.GetValue(crane) as SoundObject;
                handle.Thunk = CraneThunkField.GetValue(crane) as SoundObject;
                handle.LiftSpeed = ReadFloatField(CraneLiftSpeedField, crane, CraneLiftSpeedFallback);
                handle.DropSpeed = ReadFloatField(CraneDropSpeedField, crane, CraneDropSpeedFallback);
                if (handle.Balloon == null || handle.Navigator == null || handle.Room == null
                    || handle.Audio == null || handle.Balloon.Entity == null)
                {
                    return null;
                }

                return handle;
            }
            catch (Exception exception)
            {
                LogEventError("crane", exception);
                return null;
            }
        }

        private static float ReadFloatField(FieldInfo field, object owner, float fallback)
        {
            try
            {
                if (field != null)
                {
                    return (float)field.GetValue(owner);
                }
            }
            catch (Exception)
            {
                // The fallback is the value the game ships with.
            }

            return fallback;
        }

        private static bool IsCraneBusy(CraneHandle handle)
        {
            try
            {
                return (bool)CraneCarryingField.GetValue(handle.Crane);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// A thrown prop that flies into a crane's claw is grabbed (CraneController.EntityTriggerEnter does the same
        /// for a character): the crane lifts it, carries it to the output of the conveyor belt and lets go. Nothing
        /// pushes the crane. The crane takes what is in its room, when it is free, by the same trigger the game uses;
        /// the swept path of the step is tested, so no speed gets past.
        /// </summary>
        private void HitCranes(CarryState state, Vector3 start, Vector3 end, Vector3 half)
        {
            EventFx fx = state.Fx;
            if (_cranes.Length == 0 || fx == null || fx.Craned || fx.Pinned)
            {
                return;
            }

            try
            {
                Cell cell;
                if (!TryGetCell(state.Environment, end, out cell))
                {
                    return;
                }

                for (int i = 0; i < _cranes.Length; i++)
                {
                    CraneController crane = _cranes[i];
                    if (crane == null || !crane.isActiveAndEnabled)
                    {
                        continue;
                    }

                    CraneHandle handle = GetCraneHandle(crane);
                    if (handle == null || IsCraneBusy(handle) || cell.room != handle.Room.Room)
                    {
                        continue;
                    }

                    Collider trigger = handle.Balloon.Entity.Trigger;
                    if (trigger == null || !trigger.enabled)
                    {
                        continue;
                    }

                    Bounds volume = trigger.bounds;
                    volume.Expand(half * 2f);
                    float entry;
                    if (SegmentHitsBounds(start, end, volume, out entry) && BeginCraneGrab(state, handle))
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                LogEventError("crane grab", exception);
            }
        }

        /// <summary>
        /// What CraneController.EntityTriggerEnter does when it takes a character, for a prop: an output cell of the
        /// room and the cell beside it, the crane stops wandering, finds its way and closes the claw. The rest is
        /// CraneCarryProp, a copy of CarryEntityToTarget that carries the prop instead of an entity.
        /// </summary>
        private bool BeginCraneGrab(CarryState state, CraneHandle handle)
        {
            Cell output = handle.Room.GetRandomOutputCell();
            Cell target = output != null
                ? CraneTargetCellMethod.Invoke(handle.Crane, new object[] { output }) as Cell
                : null;
            if (output == null || target == null)
            {
                return false;
            }

            EventFx fx = state.Fx;
            CraneCarry carry = new CraneCarry();
            carry.Crane = handle.Crane;
            carry.Height = CraneLowHeight;
            carry.DropSpeed = handle.DropSpeed;
            try
            {
                CraneCarryingField.SetValue(handle.Crane, true);
                handle.Balloon.enabled = false;
                handle.Balloon.Entity.UpdateInternalMovement(Vector3.zero);
                handle.Navigator.FindPath(target.CenterWorldPosition);
                handle.Navigator.ManuallyAppendDestinationPoints(output.CenterWorldPosition);
                handle.Audio.FlushQueue(true);
                handle.Audio.SetLoop(true);
                handle.Audio.PlaySingle(handle.Thunk);
                CraneCloseClawMethod.Invoke(handle.Crane, null);
                handle.Crane.StartCoroutine(CraneCarryProp(handle, carry, state.Environment, target));
            }
            catch (Exception)
            {
                // Leave the crane as it was found; the prop flies on.
                carry.Released = true;
                CraneCarryingField.SetValue(handle.Crane, false);
                handle.Balloon.enabled = true;
                handle.Navigator.ClearDestination();
                throw;
            }

            fx.Craned = true;
            fx.Carry = carry;
            fx.KeepLanding = true;
            StopFlightSound(state);
            state.FlightVelocity = Vector3.zero;
            Debug.Log("[Lifter Frenzy] A crane grabbed " + state.Root.name
                + " and carries it to the conveyor belt.");
            return true;
        }

        /// <summary>
        /// CraneController.CarryEntityToTarget with a prop in the claw: the claw closes, the crane lifts (height 5 to
        /// 12.5), flies along its path to the output cell, opens the claw (the prop falls, FxStepCrane lets go of it)
        /// and goes back to where it took the prop and down to its own height. The crane is free again at the end
        /// (CraneController.ReInit), whatever happens on the way.
        /// </summary>
        private System.Collections.IEnumerator CraneCarryProp(
            CraneHandle h, CraneCarry carry, EnvironmentController environment, Cell targetCell)
        {
            CraneController crane = h.Crane;
            try
            {
                float delay = CraneClawSeconds;
                while (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    yield return null;
                }

                crane.Entity.SetInteractionState(false);
                float height = CraneLowHeight;
                h.Audio.QueueAudio(h.LiftingLoop, true);
                while (height < CraneHighHeight)
                {
                    carry.Height = height;
                    crane.Entity.SetHeight(height);
                    yield return null;
                    height += Time.deltaTime * h.LiftSpeed;
                }

                height = CraneHighHeight;
                carry.Height = height;
                crane.Entity.SetHeight(height);
                h.Audio.FlushQueue(true);
                delay = CranePauseSeconds;
                while (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    yield return null;
                }

                h.Audio.QueueAudio(h.RunningLoop, true);
                while (h.Navigator.HasDestination)
                {
                    h.Navigator.MoveTowardsDestination(
                        h.Balloon.speed * crane.Entity.ExternalActivity.Multiplier * Time.deltaTime
                        * environment.EnvironmentTimeScale);
                    yield return null;
                }

                h.Audio.FlushQueue(true);
                h.Audio.PlaySingle(h.Thunk);
                delay = CranePauseSeconds;
                while (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    yield return null;
                }

                CraneOpenClawMethod.Invoke(crane, null);
                carry.Released = true;
                // The crane waits as long as a character takes to come down to the floor.
                delay = (CraneHighHeight - CraneLowHeight) / Mathf.Max(0.1f, h.DropSpeed);
                while (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    yield return null;
                }

                h.Navigator.ManuallyAppendDestinationPoints(targetCell.CenterWorldPosition);
                delay = CraneClawSeconds;
                while (delay > 0f)
                {
                    delay -= Time.deltaTime;
                    yield return null;
                }

                height = CraneHighHeight;
                crane.Entity.SetHeight(height);
                h.Audio.QueueAudio(h.RunningLoop, true);
                h.Audio.SetLoop(true);
                while (h.Navigator.HasDestination)
                {
                    h.Navigator.MoveTowardsDestination(
                        h.Balloon.speed * crane.Entity.ExternalActivity.Multiplier * Time.deltaTime
                        * environment.EnvironmentTimeScale);
                    yield return null;
                }

                h.Audio.QueueAudio(h.LiftingLoop, true);
                h.Audio.PlaySingle(h.Thunk);
                while (height > CraneLowHeight)
                {
                    crane.Entity.SetHeight(height);
                    yield return null;
                    height -= Time.deltaTime * h.LiftSpeed;
                }

                height = CraneLowHeight;
                crane.Entity.SetHeight(height);
            }
            finally
            {
                // The prop never stays in the claw and the crane never stays busy.
                carry.Released = true;
                if (crane != null)
                {
                    crane.ReInit();
                }
            }
        }

        /// <summary>
        /// A prop in a crane's claw hangs at the claw's place (the crane's x and z, the claw's height) while the
        /// crane's own sequence runs. When the claw opens (or the crane is gone) the prop is let go and falls on at
        /// the speed a character is dropped at; the ordinary flight takes over, and with it the belt below.
        /// </summary>
        private bool FxStepCrane(CarryState state, EventFx fx, float dt)
        {
            CraneCarry carry = fx.Carry;
            if (carry == null || carry.Crane == null || carry.Released)
            {
                float drop = carry != null ? carry.DropSpeed : CraneDropSpeedFallback;
                FxAbortCrane(state);
                state.FlightVelocity = new Vector3(0f, -drop, 0f);
                return false;
            }

            Vector3 crane = carry.Crane.transform.position;
            Vector3 centre = new Vector3(crane.x, state.FlightFloorY + carry.Height, crane.z);
            state.FlightVelocity = Vector3.zero;
            state.FlightPosition = centre - state.FlightCenterOffset;
            state.Body.MovePosition(state.FlightPosition);
            return true;
        }

        /// <summary>The prop lets go of the crane (the claw opened, an error, the crane is gone).</summary>
        private static void FxAbortCrane(CarryState state)
        {
            EventFx fx = state != null ? state.Fx : null;
            if (fx == null)
            {
                return;
            }

            if (fx.Carry != null)
            {
                fx.Carry.Released = true;
            }

            fx.Craned = false;
            fx.Carry = null;
        }
    }
}
