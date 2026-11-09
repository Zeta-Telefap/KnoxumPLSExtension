using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The state of one prop inside an event: the whirlpool it is caught by, the belt under it, the crane that holds
    /// it, the gravity it took.
    /// </summary>
    public sealed partial class PropLifter
    {


        // ------------------------------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------------------------------

        /// <summary>What the events did to one released prop; created in FxOnRelease, null-safe everywhere.</summary>
        private sealed class EventFx
        {
            public Quaternion BaseRotation;

            // Gravity Chaos: +1 = down, -1 = up; the roll turns the prop over while gravity changes.
            public float GravityFactor = 1f;
            public float GravityTarget = 1f;
            public float RollAngle;
            // The roll that BaseRotation already contains (a prop that was carried turned over is let go that way)
            // and the horizontal axis the roll turns about: the pose is BaseRotation turned by RollAngle - RollOrigin.
            public float RollOrigin;
            public Vector3 RollAxis;
            public bool RollApplied;
            public bool Pinned;

            // Flood.
            public bool Pulled;
            public bool PullLogged;
            public bool PullGaveUp;
            public float PulledSeconds;
            public bool FloorTouched;
            public bool Sinking;
            public Whirlpool SinkPool;
            public Vector3 SinkCentre;
            // 1 -> 0.2 going down into the pool that caught the prop, 0.2 -> 1 coming up out of the exit pool.
            public float SinkPercent = 1f;
            public bool SinkRising;
            public bool HasExit;
            public Vector3 ExitFloor;
            // Only the new whirlpool that was formed for this prop (no other whirlpool was left): it is closed again
            // when the prop is out.
            public Whirlpool ExitPool;
            // The other whirlpool of the flood the prop comes out of: it closes as well once the prop is out.
            public Whirlpool ExitOther;

            // Student Shuffle.
            public bool Riding;
            public StudentCrowd_Follower Follower;
            public Vector3 RideOffset;
            public Vector3 RideLast;
            public Vector3 RideVelocity;

            // A prop that was let go at the edge of an event spawner room does not ride again.
            public bool RideBlocked;

            // Conveyor belts: the prop stands on a running belt and is carried by it until it is off the belt, and
            // the push of the last belt it rode, which carries it over a still belt of the chain.
            public bool OnBelt;
            public bool BeltLogged;
            public Vector3 BeltPush;

            // Cranes: a crane holds the prop in its claw (the crane's own sequence runs, FxStepCrane follows it).
            public bool Craned;
            public CraneCarry Carry;

            // A prop that went through an event settles where it ends up, like a thrown one.
            public bool KeepLanding;
            public float SpecialSeconds;
        }
    }
}
