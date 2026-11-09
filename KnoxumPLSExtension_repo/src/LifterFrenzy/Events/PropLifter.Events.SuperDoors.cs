using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The super doors (LockdownDoor): an event door squishes a prop, a lever door waits on it.
    /// </summary>
    public sealed partial class PropLifter
    {

        private const float DoorSquishSeconds = 10f;
        private const float SlabDescendEpsilon = 0.002f;
        // A super door looks at the props within its own size plus this much (a prop's pivot may be off its middle).
        private const float SuperDoorNearMargin = 8f;
        // A prop that does not reach higher than this above the closed slab's underside stops nothing.
        private const float SuperDoorPropMinHeight = 0.05f;
        // A lever door's slab is brought back up to a prop only when it sits lower than the prop's top by more than this.
        private const float SuperDoorHoldSlack = 0.01f;
        private static readonly FieldInfo SuperDoorSlabField = typeof(LockdownDoor).GetField(
            "door", BindingFlags.Instance | BindingFlags.NonPublic);
        // The levers and buttons wired to a super door (private list, filled by GameButtonBase.SetUp) and the height
        // its slab is moving to (private; DoorMover moves the slab there from either side and stops).
        private static readonly FieldInfo SuperDoorLeversField = typeof(LockdownDoor).GetField(
            "connectedLevers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SuperDoorTargetField = typeof(LockdownDoor).GetField(
            "targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        // The super doors the Lockdown event built (its private list of doors).
        private static readonly FieldInfo LockdownDoorsField = typeof(LockdownEvent).GetField(
            "doors", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>What a super door remembers between frames (TickSuperDoors).</summary>
        private sealed class SuperDoorState
        {
            // The slab's world box when it was first looked at and the height of its transform then: the slab only
            // moves up and down, so its box now is that box moved by the change of height (no renderer bounds,
            // which a culled slab reports wrongly).
            public bool HasBox;
            public Bounds Box;
            public float BoxY;
            public bool HasBottom;
            public float Bottom;
            // A lever door that is stopped on a prop and waits for it to be taken away.
            public bool Waiting;
        }
        private bool _lockdownRunning;
        private readonly List<LockdownDoor> _superDoorList = new List<LockdownDoor>();
        private LockdownDoor[] _superDoors = new LockdownDoor[0];
        private readonly Dictionary<LockdownDoor, MeshRenderer> _superDoorSlabs =
            new Dictionary<LockdownDoor, MeshRenderer>();
        private readonly Dictionary<LockdownDoor, SuperDoorState> _superDoorStates =
            new Dictionary<LockdownDoor, SuperDoorState>();
        // The lever doors that wait on a prop now: their collider does not hide that prop from the aim.
        private readonly List<LockdownDoor> _waitingDoors = new List<LockdownDoor>();
        private EnvironmentController _superDoorsEnvironment;

        /// <summary>
        /// The super doors (LockdownDoor): the ones the Lockdown event built (its own private list, always
        /// current) and the ones in the floor's door list (taken once per floor, so it may be older than a door).
        /// </summary>
        private void RefreshSuperDoors(EnvironmentController environment)
        {
            if (_levelDoorsEnvironment != environment)
            {
                EnsureLevelDoors(environment);
            }

            if (_superDoorsEnvironment != environment)
            {
                // A new floor: nothing is remembered about the doors of the old one.
                _superDoorsEnvironment = environment;
                _superDoorStates.Clear();
                _superDoorSlabs.Clear();
                _waitingDoors.Clear();
            }

            _superDoorList.Clear();
            for (int i = 0; i < _floorEvents.Count; i++)
            {
                LockdownEvent lockdown = _floorEvents[i] as LockdownEvent;
                List<Door> own = null;
                if (lockdown != null && LockdownDoorsField != null)
                {
                    own = LockdownDoorsField.GetValue(lockdown) as List<Door>;
                }

                for (int j = 0; own != null && j < own.Count; j++)
                {
                    LockdownDoor door = own[j] as LockdownDoor;
                    if (door != null && !_superDoorList.Contains(door))
                    {
                        _superDoorList.Add(door);
                    }
                }
            }

            for (int i = 0; i < _levelSuperDoors.Length; i++)
            {
                LockdownDoor door = _levelSuperDoors[i];
                if (door != null && !_superDoorList.Contains(door))
                {
                    _superDoorList.Add(door);
                }
            }

            _superDoors = _superDoorList.ToArray();
        }

        /// <summary>
        /// The super doors (LockdownDoor), looked at every frame (a slab comes down fast) whenever one is closing
        /// or is waiting on a prop; a door at rest costs one position read. A prop may stand under a super door:
        ///   - a door that an event closes (Lockdown) and that has no lever: the prop is squished, the sound of
        ///     putting a prop down plays and the door goes straight back up (CrushUnderSuperDoor);
        ///   - a door that a lever (or a button) works: it stops on top of the prop and stays stuck there until the
        ///     prop is taken away, then closes the rest of the way (TickLeverDoor).
        /// This runs whenever such a door closes, not only during the Lockdown event.
        /// </summary>
        private void TickSuperDoors()
        {
            for (int i = 0; i < _superDoors.Length; i++)
            {
                LockdownDoor door = _superDoors[i];
                MeshRenderer slab = SuperDoorSlab(door);
                if (slab == null)
                {
                    continue;
                }

                SuperDoorState state;
                if (!_superDoorStates.TryGetValue(door, out state))
                {
                    state = new SuperDoorState();
                    _superDoorStates[door] = state;
                    // Once per door and floor: which way it is handled, and how tall its slab is.
                    Debug.Log("[Lifter Frenzy] Super door " + door.name + ": "
                        + (IsLeverDoor(door) ? "a lever/button door (it stops on a prop)" : "an event door (it squishes)")
                        + "; its opened height is " + door.originalHeight.ToString("0.00") + ".");
                }

                Bounds slabBox;
                float slabY;
                SuperDoorSlabBox(slab, state, out slabBox, out slabY);
                // The slab came down since the last time its height was written down. The height is written down
                // again only when it changed by a visible bit, so a slab that creeps (a slowed environment) is
                // caught after a few frames instead of never.
                float bottom = slabBox.min.y;
                bool descending = false;
                if (!state.HasBottom)
                {
                    state.HasBottom = true;
                    state.Bottom = bottom;
                }
                else if (Mathf.Abs(state.Bottom - bottom) >= SlabDescendEpsilon)
                {
                    descending = state.Bottom > bottom;
                    state.Bottom = bottom;
                }

                if (!descending && !state.Waiting)
                {
                    continue;
                }

                if (IsLeverDoor(door))
                {
                    TickLeverDoor(door, state, slabBox, slabY);
                }
                else if (descending)
                {
                    CrushUnderSuperDoor(door, slabBox);
                }
            }
        }

        /// <summary>
        /// The slab's world box now: the box taken when the door was first looked at, moved by the change of the
        /// slab's height (it only moves up and down). Without a mesh to measure, the renderer's own bounds.
        /// </summary>
        private static void SuperDoorSlabBox(
            MeshRenderer slab, SuperDoorState state, out Bounds box, out float slabY)
        {
            slabY = slab.transform.position.y;
            if (!state.HasBox)
            {
                Bounds first;
                if (!TryGetMeshWorldBounds(slab, out first))
                {
                    box = slab.bounds;
                    return;
                }

                state.Box = first;
                state.BoxY = slabY;
                state.HasBox = true;
            }

            box = state.Box;
            Vector3 centre = box.center;
            centre.y += slabY - state.BoxY;
            box.center = centre;
        }

        /// <summary>
        /// A door with a lever or a button wired to it (LockdownDoor.connectedLevers). If the game does not have
        /// that list or the slab's target height any more, no door counts as one: they all squish (the event door rule).
        /// </summary>
        private static bool IsLeverDoor(LockdownDoor door)
        {
            if (SuperDoorLeversField == null || SuperDoorTargetField == null || door == null)
            {
                return false;
            }

            try
            {
                System.Collections.ICollection levers = SuperDoorLeversField.GetValue(door) as System.Collections.ICollection;
                return levers != null && levers.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// A door that closes on its own (an event) treats what is under its slab like the hammer and the boulder
        /// do, in two steps. (1) The slab touches the top of a prop: the prop is squished at once (Entity.Squish for
        /// DoorSquishSeconds, see PropFx) and the door goes on closing. (2) The slab comes down onto the top of the
        /// squished prop: the sound of putting a prop down plays and the door goes straight back up
        /// (LockdownDoor.Open while the slab is moving only changes its target height). The box of the prop is
        /// measured from its meshes every time (SquishTargetBox), so after the squish it is the squished one. A prop
        /// that was squished already (hammer, boulder) goes straight to step 2.
        /// </summary>
        private void CrushUnderSuperDoor(LockdownDoor door, Bounds slabBox)
        {
            float near = slabBox.extents.magnitude + SuperDoorNearMargin;
            CollectSquishTargets(slabBox.center, near);
            bool stopped = false;
            for (int j = 0; j < _squishTargets.Count; j++)
            {
                SquishTarget target = _squishTargets[j];
                Bounds prop = new Bounds();
                if (target.Root == null
                    || (target.Root.position - slabBox.center).sqrMagnitude > near * near
                    || !SquishTargetBox(target, out prop) || !prop.Intersects(slabBox))
                {
                    continue;
                }

                if (SquishProp(target.Root, DoorSquishSeconds))
                {
                    // The first touch: squished now; the door goes on down to what is left of the prop.
                    Debug.Log("[Lifter Frenzy] A closing super door touched " + target.Root.name
                        + " and squished it; it goes on closing down to the top of the squished prop.");
                    continue;
                }

                stopped = true;
                PlaySoundAt(prop.center, FloorSoundName, FloorSoundVolume);
                Debug.Log("[Lifter Frenzy] A closing super door came down onto the squished "
                    + target.Root.name + " and went back up.");
            }

            if (stopped)
            {
                door.Open(true, false);
            }
        }

        /// <summary>
        /// A door that a lever works never squishes anything. Closing on a prop it comes to rest on the prop's top
        /// (the slab's target height is held there, so the slab stops, or comes back up if the prop arrived under
        /// it) and the door stays stuck for as long as the prop is under it. Once the prop is gone it closes the
        /// rest of the way (Shut again); a lever that opens the door meanwhile is not held back.
        /// </summary>
        private void TickLeverDoor(LockdownDoor door, SuperDoorState state, Bounds slabBox, float slabY)
        {
            // Where the slab's underside is, measured from the slab's own height: constant, whatever the height.
            float offset = slabBox.min.y - slabY;
            float top = 0f;
            Transform holder = null;
            bool under = !door.open && TopOfPropUnderSuperDoor(slabBox, offset, out top, out holder);
            if (under)
            {
                float hold = Mathf.Clamp(top - offset, 0f, Mathf.Max(0f, door.originalHeight));
                if (slabY < hold - SuperDoorHoldSlack)
                {
                    // The slab is lower than the prop's top (the prop came under it): it comes up to rest on it.
                    // Shut also starts the slab if it stands still.
                    door.Shut();
                }

                SetSuperDoorTarget(door, hold);
                if (!state.Waiting)
                {
                    state.Waiting = true;
                    if (!_waitingDoors.Contains(door))
                    {
                        _waitingDoors.Add(door);
                    }

                    Debug.Log("[Lifter Frenzy] A lever door stopped on " + (holder != null ? holder.name : "a prop")
                        + " and stays stuck until it is taken away (the prop's top is " + top.ToString("0.00")
                        + ", the slab's underside is " + slabBox.min.y.ToString("0.00") + ", its height is "
                        + slabY.ToString("0.00") + " and it moves to " + hold.ToString("0.00") + ").");
                }

                return;
            }

            if (!state.Waiting)
            {
                return;
            }

            state.Waiting = false;
            _waitingDoors.Remove(door);
            if (!door.open)
            {
                // The prop is gone: the door closes the rest of the way.
                door.Shut();
            }

            Debug.Log("[Lifter Frenzy] The prop is gone from under a lever door; "
                + (door.open ? "the door is open." : "it closes."));
        }

        /// <summary>
        /// The top of the highest prop under the slab: one whose footprint overlaps the slab's, that reaches below
        /// the slab's underside (it is really under the slab, not above it) and that reaches higher than the
        /// closed slab's underside. Props in flight count; a prop that is carried does not. A prop standing higher
        /// than the slab's underside does not hold the door up: the slab is already below it and cannot rest on it.
        /// </summary>
        private bool TopOfPropUnderSuperDoor(
            Bounds slabBox, float offset, out float top, out Transform holder)
        {
            top = 0f;
            holder = null;
            float near = slabBox.extents.magnitude + SuperDoorNearMargin;
            CollectSquishTargets(slabBox.center, near);
            for (int j = 0; j < _squishTargets.Count; j++)
            {
                SquishTarget target = _squishTargets[j];
                Bounds prop = new Bounds();
                if (target.Root == null
                    || (target.Root.position - slabBox.center).sqrMagnitude > near * near
                    || !SquishTargetBox(target, out prop)
                    || prop.max.x <= slabBox.min.x || prop.min.x >= slabBox.max.x
                    || prop.max.z <= slabBox.min.z || prop.min.z >= slabBox.max.z
                    || prop.min.y >= slabBox.min.y + SuperDoorPropMinHeight
                    || prop.max.y <= slabBox.min.y + SuperDoorPropMinHeight)
                {
                    continue;
                }

                if (holder == null || prop.max.y > top)
                {
                    top = prop.max.y;
                    holder = target.Root;
                }
            }

            return holder != null;
        }

        /// <summary>The slab of a lever door moves to this height (LockdownDoor.targetHeight) and stops there.</summary>
        private static void SetSuperDoorTarget(LockdownDoor door, float height)
        {
            try
            {
                SuperDoorTargetField.SetValue(door, height);
            }
            catch (Exception exception)
            {
                LogEventError("lever door", exception);
            }
        }

        /// <summary>
        /// True for a collider of a lever door that is stuck on a prop (see IsIgnoredForAim): while such a door
        /// is stuck its collider is switched on and would hide the prop under it from the aim.
        /// </summary>
        private bool FxIsWaitingDoorCollider(Collider collider)
        {
            if (_waitingDoors.Count == 0 || collider == null)
            {
                return false;
            }

            LockdownDoor door = collider.GetComponentInParent<LockdownDoor>();
            return door != null && _waitingDoors.Contains(door);
        }

        private MeshRenderer SuperDoorSlab(LockdownDoor door)
        {
            if (door == null)
            {
                return null;
            }

            MeshRenderer slab;
            if (_superDoorSlabs.TryGetValue(door, out slab) && slab != null)
            {
                return slab;
            }

            slab = null;
            try
            {
                if (SuperDoorSlabField != null)
                {
                    slab = SuperDoorSlabField.GetValue(door) as MeshRenderer;
                }
            }
            catch (Exception)
            {
                slab = null;
            }

            if (slab == null)
            {
                slab = door.GetComponentInChildren<MeshRenderer>();
            }

            _superDoorSlabs[door] = slab;
            return slab;
        }
    }
}
