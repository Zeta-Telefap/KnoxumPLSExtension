using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RebuildTheSchool
{
    /// <summary>
    /// What the vanilla BB+ events and characters do to props (the part of g that reacts to the game world):
    /// Flood (Whirlpool), Gravity Chaos (GravityFlipper), Balder Dash (Balder), Dr Reflex's hammer,
    /// the "super doors" (LockdownDoor), Student Shuffle (StudentCrowd), the wheel-click kick and characters
    /// that walk around props. g.cs only calls the Fx* hooks; every hook is guarded, so a failing
    /// event is logged (throttled) and never breaks the base flight.
    /// </summary>
    public sealed partial class g
    {
        // ------------------------------------------------------------------------------------------
        // Tuning
        // ------------------------------------------------------------------------------------------

        // Wheel click: the prop under the crosshair is kicked like an RMB throw (same speed, push and sounds).
        private const float KickRange = 14f;
        private const float KickLift = 5f;
        private const float KickUpward = 0.12f;

        private const float EventScanInterval = 0.5f;
        // The events of the floor are children of the environment (a few dozen children, no tiles): their list is
        // taken again when that number changes and at the latest this often.
        private const float FloorEventsRescanSeconds = 5f;
        private const float EventErrorLogInterval = 5f;
        // A special state (ceiling, student, whirlpool) that somehow lasts this long is dropped.
        private const float EventSpecialMaxSeconds = 120f;

        // Squish = Entity.Squish: vertical scale 0.5, wobbling during the last 3 s (see PropFx).
        private const float SquishTickInterval = 0.12f;
        private const float SquishNearMargin = 14f;
        private const float BalderSquishSeconds = 10f;
        private const float HammerSquishFallbackSeconds = 30f;
        private const float HammerReach = 5f;
        private const float HammerRadius = 4f;
        private const float DoorSquishSeconds = 10f;
        private const float SlabDescendEpsilon = 0.002f;
        // A super door looks at the props within its own size plus this much (a prop's pivot may be off its middle).
        private const float SuperDoorNearMargin = 8f;
        // A prop that does not reach higher than this above the closed slab's underside stops nothing.
        private const float SuperDoorPropMinHeight = 0.05f;
        // A lever door's slab is brought back up to a prop only when it sits lower than the prop's top by more than this.
        private const float SuperDoorHoldSlack = 0.01f;

        // Conveyor belts (BeltManager): a prop whose underside is this near the floor stands on it; a belt that is
        // slower than this carries nothing.
        private const float BeltFloorTolerance = 0.25f;
        private const float BeltMinSpeed = 0.5f;

        // Cranes (CraneController): the heights and waits of its own CarryEntityToTarget.
        private const float CraneLowHeight = 5f;
        private const float CraneHighHeight = 12.5f;
        private const float CraneClawSeconds = 1.5f;
        private const float CranePauseSeconds = 0.5f;
        private const float CraneLiftSpeedFallback = 3f;
        private const float CraneDropSpeedFallback = 10f;

        // Gravity Chaos.
        private const float GravityFlipRate = 1.6f;
        private const float GravityRollRate = 360f;
        private const float GravityLandFactor = 0.9f;

        // Flood: the numbers of Whirlpool (maxForce 20, catches at 5 units, sinkSpeed 2, down to 20%, an exit
        // whirlpool that forms for 3 s).
        private const float WhirlpoolPull = 20f;
        private const float WhirlpoolSinkDistance = 5f;
        private const float WhirlpoolSinkSpeed = 2f;
        private const float WhirlpoolSinkFloor = 0.2f;
        private const float WhirlpoolExitFormSeconds = 3f;
        // The prop goes down by its own height plus this (the floor hides it) while the sink percent goes to 20%.
        private const float WhirlpoolDropMargin = 0.3f;
        private const float WhirlpoolGlideSpeed = 12f;
        private const float WhirlpoolFloorTolerance = 0.3f;
        private const float WhirlpoolMinHoldPull = 0.3f;
        private const int WhirlpoolClosingLayer = 19;
        // The magnet for props. The vanilla pull is zero at the edge of the sphere and a prop that is thrown past
        // a pool has its own speed, so a prop within this far of the sphere is caught: it is pulled at least this
        // fast (units per second), a bit stronger than an entity, and its own sideways speed dies out (rate per
        // second, stronger toward the middle).
        private const float WhirlpoolMagnetExtra = 4f;
        // A prop that is pulled this long without reaching the middle (something in the way) is let go.
        private const float WhirlpoolMaxPullSeconds = 10f;
        private const float WhirlpoolMinSlide = 5f;
        private const float WhirlpoolPropBoost = 1.5f;
        private const float WhirlpoolCatchDamp = 6f;

        // Student Shuffle.
        private const float CrowdCatchRadius = 3.5f;
        private const float CrowdCatchAbove = 0.5f;
        private const float CrowdHeadOffsetDefault = 3.5f;
        private const float CrowdLiftSpeed = 20f;
        private const float CrowdCenterSpeed = 4f;

        // Characters walk around props (see TickNpcAvoidance).
        private const float AvoidScanInterval = 0.4f;
        private const float AvoidScanRadius = 60f;
        private const float AvoidMaxSize = 25f;
        // A prop only matters to a character if it reaches into this band above the floor.
        private const float AvoidBandBottom = 0.6f;
        private const float AvoidBandTop = 7f;
        // Clearance = the character's radius (limited to this range) + the margin. A leg only counts as blocked
        // when it comes closer to a prop than clearance - slack, so legs that run along a detour (at clearance)
        // never are and a character brushing a prop does not flip between plans.
        private const float AvoidMinClearance = 0.8f;
        private const float AvoidMaxClearance = 1.5f;
        private const float AvoidMargin = 0.15f;
        private const float AvoidSlack = 0.3f;
        // A character already closer to a prop than that keeps the margin it has minus the shrink; one that
        // touches the prop (closer than the touch distance) ignores it.
        private const float AvoidShrink = 0.1f;
        private const float AvoidTouch = 0.1f;
        // Detour points lie this far outside the clearance rectangle, so legs along it never count as inside.
        private const float AvoidPad = 0.05f;
        private const float AvoidEpsilon = 0.01f;
        // Guards against detours that never end (see ChangeAllowed and ReachedAgain).
        private const float AvoidReach = 1f;
        private const float AvoidSameCorner = 0.75f;
        private const float AvoidWindow = 3f;
        private const int AvoidMaxChanges = 8;
        private const float AvoidLoopWindow = 25f;
        private const int AvoidLoopVisits = 2;
        private const float AvoidLoopPause = 10f;
        // A blocked leg with no way round is looked at again after this long, not every frame.
        private const float AvoidRetryDelay = 0.15f;
        private const int AvoidForgetFrames = 600;
        // A detour point must keep this half width clear of the walls.
        private const float AvoidWallClearance = 0.6f;
        private const int AvoidMaxPoints = 16;
        private const float AvoidLogInterval = 3f;

        private static readonly FieldInfo ReflexSquishTimeField = typeof(DrReflex).GetField(
            "squishTime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SuperDoorSlabField = typeof(LockdownDoor).GetField(
            "door", BindingFlags.Instance | BindingFlags.NonPublic);
        // The levers and buttons wired to a super door (private list, filled by GameButtonBase.SetUp) and the height
        // its slab is moving to (private; DoorMover moves the slab there from either side and stops).
        private static readonly FieldInfo SuperDoorLeversField = typeof(LockdownDoor).GetField(
            "connectedLevers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SuperDoorTargetField = typeof(LockdownDoor).GetField(
            "targetHeight", BindingFlags.Instance | BindingFlags.NonPublic);
        // The flippers the gravity event keeps in a private list (they are children of floor tiles, not of the event).
        private static readonly FieldInfo GravityFlippersField = typeof(GravityEvent).GetField(
            "flippers", BindingFlags.Instance | BindingFlags.NonPublic);
        // The super doors the Lockdown event built (its private list of doors).
        private static readonly FieldInfo LockdownDoorsField = typeof(LockdownEvent).GetField(
            "doors", BindingFlags.Instance | BindingFlags.NonPublic);
        // Every event the level builder gave the floor (EnvironmentController.AddEvent), running or not.
        private static readonly FieldInfo EnvironmentEventsField = typeof(EnvironmentController).GetField(
            "events", BindingFlags.Instance | BindingFlags.NonPublic);

        // A conveyor belt's MovementModifier: its movementAddend (direction * speed) is the push the game gives a
        // character that stands on the belt, zero while the belt is stopped.
        private static readonly FieldInfo BeltMoveModField = typeof(BeltManager).GetField(
            "moveMod", BindingFlags.Instance | BindingFlags.NonPublic);
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

        // Whirlpool.Close() (private) shrinks the pool and destroys it; Whirlpool.Update starts it when it catches
        // an entity, so a pool that has caught a prop does the same.
        private static readonly MethodInfo WhirlpoolCloseMethod = typeof(Whirlpool).GetMethod(
            "Close", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo WhirlpoolSphereField = typeof(Whirlpool).GetField(
            "sphereCollider", BindingFlags.Instance | BindingFlags.NonPublic);
        // Navigator.destinationPoints is protected: the list is read and edited through reflection.
        private static readonly FieldInfo NavigatorPointsField = typeof(Navigator).GetField(
            "destinationPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Vector3 AvoidWallHalf = new Vector3(AvoidWallClearance, 1f, AvoidWallClearance);
        private static readonly float[] AvoidCornerX = new float[4];
        private static readonly float[] AvoidCornerZ = new float[4];
        private static readonly List<Collider> EventColliders = new List<Collider>();
        // When each kind of event error may be logged again (a failing step is logged on its own, not hidden by another).
        private static readonly Dictionary<string, float> EventErrorLogTimes = new Dictionary<string, float>();

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

        private struct SquishTarget
        {
            public Transform Root;
            public Renderer[] Renderers;
        }

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

        /// <summary>The footprint of a prop on the floor plane (world x/z).</summary>
        private struct PropRect
        {
            public Transform Root;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        /// <summary>A prop near a character: its footprint, the zone a leg must stay out of, and whether it is part of the obstacle being walked around.</summary>
        private struct AvoidNearProp
        {
            public PropRect Raw;
            public PropRect Hard;
            public bool InCluster;
        }

        /// <summary>What the walk-around remembers about one character.</summary>
        private sealed class AvoidState
        {
            public bool HasCommit;
            public Vector3 Commit;
            public int Changes;
            public float WindowEnd;
            public float BlockedUntil;
            public float RetryAt;
            public readonly Vector3[] Reached = new Vector3[8];
            public readonly float[] ReachedAt = new float[8];
            public int ReachedCount;
            public int ReachedNext;
            public int LastFrame;
        }

        private float _nextEventScanTime;
        // The frame a carried prop was thrown or dropped: a wheel click in that frame was that throw, not a kick.
        private int _lastReleaseFrame = -1;
        private float _nextSquishTickTime;
        private float _nextAvoidScanTime;
        private float _nextAvoidLogTime;
        private float _crowdHeadOffset = CrowdHeadOffsetDefault;
        private GravityEvent _gravityEvent;
        // The events of this floor (RefreshFloorEvents) and what was last written to the log about them.
        private readonly List<RandomEvent> _floorEvents = new List<RandomEvent>();
        private EnvironmentController _floorEventsEnvironment;
        private int _floorEventsChildren = -1;
        private float _nextFloorEventsScan;
        private string _loggedFloorEvents;
        private bool _gravityRunning;
        private bool _floodRunning;
        private bool _crowdRunning;
        private bool _lockdownRunning;
        private readonly List<LockdownDoor> _superDoorList = new List<LockdownDoor>();
        // What was last written to the log about each event's objects (only the start and the end are logged).
        private int _loggedFlippers;
        private int _loggedWhirlpools;
        private int _loggedStudents;
        private Whirlpool[] _whirlpools = new Whirlpool[0];
        private SphereCollider[] _poolSpheres = new SphereCollider[0];
        private GravityFlipper[] _flippers = new GravityFlipper[0];
        private Bounds[] _flipperBounds = new Bounds[0];
        private Balder_Entity[] _balders = new Balder_Entity[0];
        private DrReflex[] _reflexes = new DrReflex[0];
        private LockdownDoor[] _superDoors = new LockdownDoor[0];
        private StudentCrowd_Follower[] _followers = new StudentCrowd_Follower[0];
        private StudentEvent _studentEvent;
        private readonly List<Balder_Entity> _balderList = new List<Balder_Entity>();
        private readonly List<DrReflex> _reflexList = new List<DrReflex>();
        private readonly List<SquishTarget> _squishTargets = new List<SquishTarget>();
        private readonly Dictionary<LockdownDoor, MeshRenderer> _superDoorSlabs =
            new Dictionary<LockdownDoor, MeshRenderer>();
        private readonly Dictionary<LockdownDoor, SuperDoorState> _superDoorStates =
            new Dictionary<LockdownDoor, SuperDoorState>();
        // The lever doors that wait on a prop now: their collider does not hide that prop from the aim.
        private readonly List<LockdownDoor> _waitingDoors = new List<LockdownDoor>();
        private EnvironmentController _superDoorsEnvironment;
        // The conveyor belts and the cranes of the floor (EnsureLevelFixtures), looked for once per floor.
        private BeltManager[] _belts = new BeltManager[0];
        private CraneController[] _cranes = new CraneController[0];
        private EnvironmentController _fixturesEnvironment;
        private readonly Dictionary<CraneController, CraneHandle> _craneHandles =
            new Dictionary<CraneController, CraneHandle>();
        // Footprints of the props near any character (refreshed a few times per second) and, per character
        // and frame, the ones its next leg can touch.
        private readonly List<Vector3> _avoidNpcPositions = new List<Vector3>();
        private readonly List<PropRect> _avoidProps = new List<PropRect>();
        private readonly List<AvoidNearProp> _avoidNear = new List<AvoidNearProp>();
        private readonly Dictionary<int, AvoidState> _avoidStates = new Dictionary<int, AvoidState>();
        private readonly List<int> _avoidForget = new List<int>();

        private static void LogEventError(string what, Exception exception)
        {
            float next;
            if (EventErrorLogTimes.TryGetValue(what, out next) && Time.unscaledTime < next)
            {
                return;
            }

            EventErrorLogTimes[what] = Time.unscaledTime + EventErrorLogInterval;
            Debug.LogError("[Rebuild the School] Event handling (" + what + ") failed: " + exception);
        }

        /// <summary>One line when an event starts to run and one when it is over.</summary>
        private static void LogEventRunning(string what, bool running, ref bool logged)
        {
            if (running == logged)
            {
                return;
            }

            logged = running;
            Debug.Log("[Rebuild the School] " + what + (running ? " is running." : " is over."));
        }

        /// <summary>One line when an event's objects appear (count > 0) and one when they are all gone.</summary>
        private static void LogEventObjects(string what, int count, ref int logged)
        {
            bool changed = (count > 0) != (logged > 0);
            logged = count;
            if (changed)
            {
                Debug.Log("[Rebuild the School] " + what + (count > 0 ? ": " + count + " found." : ": none left."));
            }
        }

        // ------------------------------------------------------------------------------------------
        // Per-frame entry point (called at the end of Update)
        // ------------------------------------------------------------------------------------------

        private void UpdateEvents()
        {
            if (_player == null || _player.ec == null || Time.timeScale <= 0f)
            {
                return;
            }

            // Every part is guarded on its own: one that fails is logged and the others go on.
            try
            {
                if (_held == null && _lastReleaseFrame != Time.frameCount && Input.GetMouseButtonDown(2))
                {
                    TryKick();
                }
            }
            catch (Exception exception)
            {
                LogEventError("kick", exception);
            }

            RefreshEventRegistries(false);

            try
            {
                TickSquishers();
            }
            catch (Exception exception)
            {
                LogEventError("squish", exception);
            }

            try
            {
                // Every frame (a door that closes comes down fast); a door at rest costs one position read.
                TickSuperDoors();
            }
            catch (Exception exception)
            {
                LogEventError("super door squish", exception);
            }

            try
            {
                TickNpcAvoidance();
            }
            catch (Exception exception)
            {
                LogEventError("characters walking around props", exception);
            }
        }

        /// <summary>
        /// What the events work with, a couple of times per second and never per fixed step. Nothing here searches
        /// the scene (a search walks every object of the floor). The events come from the children of the
        /// environment (RefreshFloorEvents), whoever started them: EnvironmentController.GetEvent knows only the
        /// events its own EventTimer started, so an event that something else started (Level Studio's StartEvent,
        /// a tool that calls Begin) would never be seen through it. From the events: the whirlpools (the flood's
        /// own list), the flippers (the gravity event's private list), the students (children of the student
        /// event) and the super doors (the Lockdown event's list, the door list of the floor); the characters
        /// come from EnvironmentController.Npcs. Every step is guarded on its own, so one that fails is logged
        /// and does not take the others (or the squishing and the walk-around) with it.
        /// </summary>
        private void RefreshEventRegistries(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now < _nextEventScanTime)
            {
                return;
            }

            _nextEventScanTime = now + EventScanInterval;
            EnvironmentController environment = _player != null ? _player.ec : null;
            if (environment == null)
            {
                return;
            }

            try
            {
                RefreshFloorEvents(environment);
            }
            catch (Exception exception)
            {
                LogEventError("list of events", exception);
            }

            try
            {
                _gravityEvent = RunningEvent<GravityEvent>(environment, RandomEventType.Gravity);
                LogEventRunning("Gravity Chaos", _gravityEvent != null, ref _gravityRunning);
                RefreshFlippers();
            }
            catch (Exception exception)
            {
                LogEventError("gravity flippers", exception);
            }

            try
            {
                FloodEvent flood = RunningEvent<FloodEvent>(environment, RandomEventType.Flood);
                LogEventRunning("Flood", flood != null, ref _floodRunning);
                RefreshWhirlpools(flood);
            }
            catch (Exception exception)
            {
                LogEventError("whirlpools", exception);
            }

            try
            {
                StudentEvent crowd = RunningEvent<StudentEvent>(environment, RandomEventType.StudentShuffle);
                LogEventRunning("Student Shuffle", crowd != null, ref _crowdRunning);
                RefreshFollowers(crowd);
            }
            catch (Exception exception)
            {
                LogEventError("students", exception);
            }

            try
            {
                RefreshCharacters(environment);
            }
            catch (Exception exception)
            {
                LogEventError("characters", exception);
            }

            try
            {
                LockdownEvent lockdown = RunningEvent<LockdownEvent>(environment, RandomEventType.Lockdown);
                LogEventRunning("Lockdown (test procedure)", lockdown != null, ref _lockdownRunning);
                RefreshSuperDoors(environment);
            }
            catch (Exception exception)
            {
                LogEventError("super doors", exception);
            }
        }

        /// <summary>
        /// The events of this floor: the level builder (and Level Studio's StartEvent) put them under the
        /// environment, which has only a few hundred children (rooms, builders, characters, the events; the tiles
        /// hang elsewhere), so one look at them costs next to nothing; the environment's own list of the events
        /// the level gave it (private, EnvironmentController.AddEvent) is read as well. The list is taken again
        /// when the number of children changes and at the latest every FloorEventsRescanSeconds.
        /// </summary>
        private void RefreshFloorEvents(EnvironmentController environment)
        {
            Transform root = environment.transform;
            int children = root.childCount;
            float now = Time.unscaledTime;
            if (environment == _floorEventsEnvironment && children == _floorEventsChildren
                && now < _nextFloorEventsScan)
            {
                return;
            }

            if (environment != _floorEventsEnvironment)
            {
                // A new floor: its list is written to the log again.
                _loggedFloorEvents = null;
            }

            _floorEventsEnvironment = environment;
            _floorEventsChildren = children;
            _nextFloorEventsScan = now + FloorEventsRescanSeconds;
            _floorEvents.Clear();
            for (int i = 0; i < children; i++)
            {
                RandomEvent found = root.GetChild(i).GetComponent<RandomEvent>();
                if (found != null)
                {
                    _floorEvents.Add(found);
                }
            }

            List<RandomEvent> registered = null;
            if (EnvironmentEventsField != null)
            {
                registered = EnvironmentEventsField.GetValue(environment) as List<RandomEvent>;
            }

            for (int i = 0; registered != null && i < registered.Count; i++)
            {
                RandomEvent one = registered[i];
                if (one != null && !_floorEvents.Contains(one))
                {
                    _floorEvents.Add(one);
                }
            }

            string names = string.Empty;
            for (int i = 0; i < _floorEvents.Count; i++)
            {
                names += (names.Length > 0 ? ", " : string.Empty) + _floorEvents[i].GetType().Name;
            }

            if (names != _loggedFloorEvents)
            {
                _loggedFloorEvents = names;
                Debug.Log("[Rebuild the School] Events of this floor: " + (names.Length > 0 ? names : "none") + ".");
            }
        }

        /// <summary>
        /// The running event of a kind: the one EnvironmentController.GetEvent returns, else one of the floor's
        /// events whose Active flag is on (Active is set by Begin, however the event was started).
        /// </summary>
        private T RunningEvent<T>(EnvironmentController environment, RandomEventType type) where T : RandomEvent
        {
            RandomEvent timed = environment.GetEvent(type);
            if (timed != null && timed is T)
            {
                return (T)timed;
            }

            for (int i = 0; i < _floorEvents.Count; i++)
            {
                RandomEvent one = _floorEvents[i];
                if (one != null && one.Active && one is T)
                {
                    return (T)one;
                }
            }

            return null;
        }

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

        private void RefreshWhirlpools(FloodEvent flood)
        {
            List<Whirlpool> list = flood != null ? flood.whirlpools : null;
            int count = list != null ? list.Count : 0;
            _whirlpools = new Whirlpool[count];
            _poolSpheres = new SphereCollider[count];
            int alive = 0;
            for (int i = 0; i < count; i++)
            {
                _whirlpools[i] = list[i];
                _poolSpheres[i] = PoolSphere(list[i]);
                if (list[i] != null)
                {
                    alive++;
                }
            }

            LogEventObjects("Flood: whirlpools", alive, ref _loggedWhirlpools);
        }

        /// <summary>
        /// The students of the crowd are children of the student event. The event is remembered until the last
        /// of them is gone: it is no longer "running" while the crowd walks out, and a ride goes on until then.
        /// </summary>
        private void RefreshFollowers(StudentEvent crowd)
        {
            if (crowd != null)
            {
                _studentEvent = crowd;
            }

            StudentCrowd_Follower[] followers = _studentEvent != null
                ? _studentEvent.GetComponentsInChildren<StudentCrowd_Follower>()
                : new StudentCrowd_Follower[0];
            if (followers.Length == 0 && crowd == null)
            {
                _studentEvent = null;
            }

            _followers = followers;
            LogEventObjects("Student Shuffle: students", followers.Length, ref _loggedStudents);
            for (int i = 0; i < _followers.Length; i++)
            {
                StudentCrowd_Follower follower = _followers[i];
                Renderer sprite = follower != null ? follower.GetComponentInChildren<Renderer>() : null;
                if (sprite != null && sprite.enabled)
                {
                    float top = sprite.bounds.max.y - follower.transform.position.y;
                    if (top > 1f && top < 6f)
                    {
                        _crowdHeadOffset = top;
                    }

                    break;
                }
            }
        }

        private void RefreshCharacters(EnvironmentController environment)
        {
            _balderList.Clear();
            _reflexList.Clear();
            List<NPC> npcs = environment.Npcs;
            for (int i = 0; npcs != null && i < npcs.Count; i++)
            {
                NPC npc = npcs[i];
                Balder_Entity balder = npc as Balder_Entity;
                if (balder != null)
                {
                    _balderList.Add(balder);
                    continue;
                }

                DrReflex reflex = npc as DrReflex;
                if (reflex != null)
                {
                    _reflexList.Add(reflex);
                }
            }

            _balders = _balderList.ToArray();
            _reflexes = _reflexList.ToArray();
        }

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

        /// <summary>The sphere trigger that Whirlpool itself uses for its radius (its private sphereCollider).</summary>
        private static SphereCollider PoolSphere(Whirlpool pool)
        {
            if (pool == null)
            {
                return null;
            }

            SphereCollider sphere = null;
            try
            {
                if (WhirlpoolSphereField != null)
                {
                    sphere = WhirlpoolSphereField.GetValue(pool) as SphereCollider;
                }
            }
            catch (Exception)
            {
                sphere = null;
            }

            return sphere != null ? sphere : pool.GetComponentInChildren<SphereCollider>();
        }

        /// <summary>
        /// The volume of a character: the trigger collider of its Entity (the one the game's own trigger events
        /// use, whichever object it hangs on), else the enabled colliders on the character's own object.
        /// </summary>
        private static bool TryGetCharacterVolume(NPC npc, out Bounds volume)
        {
            volume = new Bounds();
            Entity entity = npc != null ? npc.Entity : null;
            Collider trigger = entity != null ? entity.Trigger : null;
            if (trigger != null && trigger.enabled)
            {
                volume = trigger.bounds;
                return true;
            }

            return TryGetOwnerBounds(npc, true, out volume) || TryGetOwnerBounds(npc, false, out volume);
        }

        /// <summary>The union of an object's enabled colliders (triggers only, or solid ones only).</summary>
        private static bool TryGetOwnerBounds(Component owner, bool triggers, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            owner.GetComponents(EventColliders);
            for (int i = 0; i < EventColliders.Count; i++)
            {
                Collider collider = EventColliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger != triggers)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            EventColliders.Clear();
            return found;
        }

        // ------------------------------------------------------------------------------------------
        // Conveyor belts and cranes
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// The conveyor belts (BeltManager) and the cranes (CraneController) of the floor, looked up once per floor,
        /// the first time a prop is in flight on it. FindObjectsOfType walks the whole scene: never every frame, and
        /// never while the level is generated (no prop flies then).
        /// </summary>
        private void EnsureLevelFixtures(EnvironmentController environment)
        {
            if (environment == null || environment == _fixturesEnvironment)
            {
                return;
            }

            _fixturesEnvironment = environment;
            _craneHandles.Clear();
            _belts = FindObjectsOfType<BeltManager>();
            _cranes = FindObjectsOfType<CraneController>();
            if (_belts.Length > 0 || _cranes.Length > 0)
            {
                Debug.Log("[Rebuild the School] This floor has " + _belts.Length + " conveyor belt(s) and "
                    + _cranes.Length + " crane(s).");
            }
        }

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
            Debug.Log("[Rebuild the School] " + state.Root.name
                + " is on a running conveyor belt and rides it until it is completely off the belts.");
        }

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
            Debug.Log("[Rebuild the School] A crane grabbed " + state.Root.name
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

        // ------------------------------------------------------------------------------------------
        // Squish: Balder, Dr Reflex's hammer, closing super doors
        // ------------------------------------------------------------------------------------------

        private static bool SquishProp(Transform root, float seconds)
        {
            PropFx fx = root.GetComponent<PropFx>();
            if (fx != null && fx.enabled && fx.Squished)
            {
                return false;
            }

            PropFx.GetOrAdd(root).Squish(seconds);
            return true;
        }

        private void TickSquishers()
        {
            float now = Time.unscaledTime;
            if (now < _nextSquishTickTime)
            {
                return;
            }

            _nextSquishTickTime = now + SquishTickInterval;
            if (_balders.Length == 0 && _reflexes.Length == 0)
            {
                return;
            }

            // One that fails is logged and does not stop the other.
            try
            {
                TickBalders();
            }
            catch (Exception exception)
            {
                LogEventError("Balder squish", exception);
            }

            try
            {
                TickHammers();
            }
            catch (Exception exception)
            {
                LogEventError("Dr Reflex's hammer", exception);
            }
        }

        /// <summary>
        /// The props that can be squished within `radius` of `centre`: the ones in flight and the movable props of
        /// the registry (tested on the position the registry keeps, so most props cost one distance test and
        /// nothing is asked of Unity for them). Each danger zone collects its own.
        /// </summary>
        private void CollectSquishTargets(Vector3 centre, float radius)
        {
            _squishTargets.Clear();
            float radiusSqr = radius * radius;
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state == null || state.Root == null || state.Renderers == null
                    || (state.Root.position - centre).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                SquishTarget target;
                target.Root = state.Root;
                target.Renderers = state.Renderers;
                _squishTargets.Add(target);
            }

            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                if ((proxy.Position - centre).sqrMagnitude > radiusSqr || proxy.Root == null
                    || !proxy.Root.gameObject.activeInHierarchy
                    || (_held != null && proxy.Root == _held.Root) || IsReleased(proxy.Root))
                {
                    continue;
                }

                SquishTarget target;
                target.Root = proxy.Root;
                target.Renderers = proxy.Renderers;
                _squishTargets.Add(target);
            }
        }

        /// <summary>
        /// Balder_DefaultState squishes whatever enters the boulder's trigger for 10 s (Entity.Squish) and plays
        /// its squish sound; a prop inside the boulder's volume gets the same.
        /// </summary>
        private void TickBalders()
        {
            for (int i = 0; i < _balders.Length; i++)
            {
                Balder_Entity balder = _balders[i];
                Bounds volume = new Bounds();
                if (balder == null || !balder.isActiveAndEnabled || balder.Crumbled
                    || !TryGetCharacterVolume(balder, out volume))
                {
                    continue;
                }

                float near = volume.extents.magnitude + SquishNearMargin;
                CollectSquishTargets(volume.center, near);
                for (int j = 0; j < _squishTargets.Count; j++)
                {
                    SquishTarget target = _squishTargets[j];
                    Bounds prop = new Bounds();
                    if (target.Root == null
                        || (target.Root.position - volume.center).sqrMagnitude > near * near
                        || !CombinedBounds(target.Renderers, out prop) || !prop.Intersects(volume))
                    {
                        continue;
                    }

                    if (SquishProp(target.Root, BalderSquishSeconds))
                    {
                        balder.Squish();
                        Debug.Log("[Rebuild the School] A Balder squished " + target.Root.name + " for "
                            + BalderSquishSeconds.ToString("0") + " s.");
                    }
                }
            }
        }

        /// <summary>
        /// DrReflex.HammerCheck (only while he hunts, DrReflex_Hunting): a sphere of radius 4, five units in
        /// front of him, squishes everything inside it for his squishTime (30 s) and swings the hammer.
        /// </summary>
        private void TickHammers()
        {
            for (int i = 0; i < _reflexes.Length; i++)
            {
                DrReflex reflex = _reflexes[i];
                if (reflex == null || !reflex.isActiveAndEnabled || reflex.behaviorStateMachine == null
                    || !(reflex.behaviorStateMachine.CurrentState is DrReflex_Hunting))
                {
                    continue;
                }

                Vector3 centre = reflex.transform.position + reflex.transform.forward * HammerReach;
                float near = HammerRadius + SquishNearMargin;
                CollectSquishTargets(centre, near);
                for (int j = 0; j < _squishTargets.Count; j++)
                {
                    SquishTarget target = _squishTargets[j];
                    Bounds prop = new Bounds();
                    if (target.Root == null
                        || (target.Root.position - centre).sqrMagnitude > near * near
                        || !CombinedBounds(target.Renderers, out prop)
                        || prop.SqrDistance(centre) > HammerRadius * HammerRadius)
                    {
                        continue;
                    }

                    if (SquishProp(target.Root, HammerSquishSeconds(reflex)))
                    {
                        reflex.Hammer(null);
                        Debug.Log("[Rebuild the School] Dr Reflex's hammer squished " + target.Root.name + ".");
                    }
                }
            }
        }

        private static float HammerSquishSeconds(DrReflex reflex)
        {
            try
            {
                if (ReflexSquishTimeField != null)
                {
                    return (float)ReflexSquishTimeField.GetValue(reflex);
                }
            }
            catch (Exception)
            {
                // The fallback below is the value the game ships with.
            }

            return HammerSquishFallbackSeconds;
        }

        /// <summary>A boulder and Dr Reflex are not pushed by a thrown prop: they hammer it (HitHammerer).</summary>
        private static bool IsHammerer(NPC npc)
        {
            return npc is Balder_Entity || npc is DrReflex;
        }

        /// <summary>
        /// A thrown prop that touches a boulder (Balder_Entity) or Dr Reflex is hammered at once and the character
        /// is not pushed. The boulder squishes it for BalderSquishSeconds and plays its squish sound, as
        /// Balder_DefaultState does for any entity that touches it. Dr Reflex squishes it for his squishTime and
        /// swings the hammer while he has it (DrReflex_Hunting); without the hammer only its sound plays (a swing
        /// that nothing in his animation answers would stay queued). The prop bounces back like off any character.
        /// Tested on the swept path of the step, so no speed gets past; a prop hits the same character once per flight.
        /// </summary>
        private bool HitHammerer(CarryState state, NPC npc, Vector3 start, Vector3 end, Vector3 half, Vector3 flat)
        {
            Balder_Entity balder = npc as Balder_Entity;
            DrReflex reflex = npc as DrReflex;
            if (npc == null || !npc.isActiveAndEnabled || (balder == null && reflex == null)
                || (balder != null && balder.Crumbled)
                || (state.PushedNpcs != null && state.PushedNpcs.Contains(npc)))
            {
                return false;
            }

            Bounds volume;
            if (!TryGetCharacterVolume(npc, out volume))
            {
                return false;
            }

            volume.Expand(half * 2f);
            float entry;
            if (!SegmentHitsBounds(start, end, volume, out entry))
            {
                return false;
            }

            // Only a prop that is moving toward the character hits it.
            Vector3 away = npc.transform.position - Vector3.Lerp(start, end, entry);
            away.y = 0f;
            if (Vector3.Dot(flat, away) <= 0f)
            {
                return false;
            }

            if (state.PushedNpcs == null)
            {
                state.PushedNpcs = new List<NPC>();
            }

            state.PushedNpcs.Add(npc);
            if (balder != null)
            {
                SquishProp(state.Root, BalderSquishSeconds);
                balder.Squish();
                Debug.Log("[Rebuild the School] A thrown prop hit a Balder at " + flat.magnitude.ToString("0.0")
                    + " u/s: the boulder squished " + state.Root.name + " at once and was not pushed.");
                return true;
            }

            SquishProp(state.Root, HammerSquishSeconds(reflex));
            bool hasHammer = reflex.behaviorStateMachine != null
                && reflex.behaviorStateMachine.CurrentState is DrReflex_Hunting;
            if (hasHammer)
            {
                reflex.Hammer(null);
            }
            else
            {
                reflex.HammerSound();
            }

            Debug.Log("[Rebuild the School] A thrown prop hit Dr Reflex at " + flat.magnitude.ToString("0.0")
                + " u/s: he hammered " + state.Root.name + " at once and was not pushed"
                + (hasHammer ? "." : " (no hammer yet: only its sound)."));
            return true;
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
                    Debug.Log("[Rebuild the School] A closing super door touched " + target.Root.name
                        + " and squished it; it goes on closing down to the top of the squished prop.");
                    continue;
                }

                stopped = true;
                PlaySoundAt(prop.center, FloorSoundName, FloorSoundVolume);
                Debug.Log("[Rebuild the School] A closing super door came down onto the squished "
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

                    Debug.Log("[Rebuild the School] A lever door stopped on " + (holder != null ? holder.name : "a prop")
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

            Debug.Log("[Rebuild the School] The prop is gone from under a lever door; "
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

        /// <summary>
        /// The world box of a squish target, measured from its meshes now. Not the box the registry keeps until a
        /// prop moves (ProxyBox): that is the unsquished one after a squish, and the renderers' own bounds are wrong
        /// while the game's culling has them switched off. Right for a squished prop and for a culled one.
        /// </summary>
        private static bool SquishTargetBox(SquishTarget target, out Bounds box)
        {
            box = new Bounds();
            Renderer[] renderers = target.Renderers;
            if (renderers == null)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds one;
                if (!TryGetMeshWorldBounds(renderer, out one))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    one = renderer.bounds;
                }

                if (found)
                {
                    box.Encapsulate(one);
                }
                else
                {
                    box = one;
                    found = true;
                }
            }

            return found;
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

        // ------------------------------------------------------------------------------------------
        // Hooks called by g.cs (flight, release, landing)
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
                Debug.Log("[Rebuild the School] A prop touched a gravity flipper; its gravity now turns "
                    + (fx.GravityTarget < 0f ? "up" : "down") + ".");
                return;
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

        // --- Flood: whirlpools ---

        private bool WhirlpoolShape(int index, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0f;
            Whirlpool pool = _whirlpools[index];
            SphereCollider sphere = _poolSpheres[index];
            // The sphere is enabled once the pool has formed; a closing pool (layer 19) pulls nothing.
            if (pool == null || sphere == null || !pool.isActiveAndEnabled || !sphere.enabled
                || pool.gameObject.layer == WhirlpoolClosingLayer)
            {
                return false;
            }

            Vector3 scale = sphere.transform.lossyScale;
            radius = sphere.radius * Mathf.Max(scale.x, scale.z);
            centre = pool.transform.position;
            return radius > 0.5f;
        }

        /// <summary>
        /// The pull of Whirlpool.Update, made a magnet for props. A prop within WhirlpoolMagnetExtra of the
        /// pool's sphere is caught: the sideways speed it came with dies out (v is changed here) and it is pulled
        /// to the middle of the pool at the vanilla speed, (R - d) / R * 20 units per second, times
        /// WhirlpoolPropBoost, never slower than WhirlpoolMinSlide and never more than the distance left; a wall
        /// in between stops it. At 5 units from the middle, on the floor, the prop starts to sink (FxStepSink).
        /// The pull is a drift on top of the velocity: the caller adds it and takes it off again after the step.
        /// </summary>
        private Vector3 FxDrift(CarryState state, Vector3 c, Vector3 half, float dt, ref Vector3 v)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return Vector3.zero;
            }

            fx.Pulled = false;
            if (_whirlpools.Length == 0 || fx.PullGaveUp)
            {
                return Vector3.zero;
            }

            try
            {
                Bounds box = new Bounds(c, half * 2f);
                float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
                int bestIndex = -1;
                float bestSpeed = 0f;
                float bestFlat = 0f;
                float bestZone = 1f;
                Vector3 bestCentre = Vector3.zero;
                Vector3 bestDirection = Vector3.zero;
                for (int i = 0; i < _whirlpools.Length; i++)
                {
                    Vector3 centre;
                    float radius;
                    if (!WhirlpoolShape(i, out centre, out radius))
                    {
                        continue;
                    }

                    float zone = radius + WhirlpoolMagnetExtra;
                    if (box.SqrDistance(centre) > zone * zone)
                    {
                        continue;
                    }

                    Vector3 flat = new Vector3(centre.x - c.x, 0f, centre.z - c.z);
                    float distance = flat.magnitude;
                    float pull = (radius - Mathf.Min(distance, radius)) / radius * WhirlpoolPull * timeScale;
                    float speed = Mathf.Max(pull * WhirlpoolPropBoost, WhirlpoolMinSlide * timeScale);
                    FlightHit wall;
                    if (speed <= bestSpeed
                        || (FlightGridSweep(state.Environment, c, Vector3.zero, flat, false, out wall)
                            && wall.Kind == FlightHitKind.Wall))
                    {
                        continue;
                    }

                    bestIndex = i;
                    bestSpeed = speed;
                    bestFlat = distance;
                    bestZone = zone;
                    bestCentre = centre;
                    bestDirection = distance > 0.001f ? flat / distance : Vector3.zero;
                }

                if (bestIndex < 0)
                {
                    return Vector3.zero;
                }

                if (bestFlat <= WhirlpoolSinkDistance && c.y - half.y <= state.FlightFloorY + WhirlpoolFloorTolerance)
                {
                    FxBeginSink(state, fx, _whirlpools[bestIndex], bestCentre);
                    return Vector3.zero;
                }

                // The catch: the speed a thrown prop brings dies out, faster toward the middle. It is taken off
                // before the drift is added (a drift inside v would be damped too, and taking it off later would
                // turn it around).
                float closeness = 1f - Mathf.Clamp01(bestFlat / bestZone);
                float keep = Mathf.Exp(-WhirlpoolCatchDamp * (0.25f + 0.75f * closeness) * dt);
                v.x *= keep;
                v.z *= keep;

                float pullSpeed = bestSpeed;
                if (pullSpeed * dt > bestFlat)
                {
                    pullSpeed = bestFlat / Mathf.Max(dt, 0.0001f);
                }

                Vector3 drift = bestDirection * pullSpeed;
                // Inside the sink distance the prop never settles before it has sunk, however short the last pull.
                fx.Pulled = bestFlat <= WhirlpoolSinkDistance
                    || drift.sqrMagnitude >= WhirlpoolMinHoldPull * WhirlpoolMinHoldPull;
                if (fx.Pulled)
                {
                    if (!fx.PullLogged)
                    {
                        fx.PullLogged = true;
                        Debug.Log("[Rebuild the School] A whirlpool pulls " + state.Root.name + " in ("
                            + bestFlat.ToString("0.0") + " from its middle).");
                    }

                    fx.PulledSeconds += dt;
                    if (fx.PulledSeconds > WhirlpoolMaxPullSeconds)
                    {
                        // Something keeps the prop from reaching the middle: let it go and land where it is.
                        fx.PullGaveUp = true;
                        fx.Pulled = false;
                        return Vector3.zero;
                    }
                }

                return drift;
            }
            catch (Exception exception)
            {
                LogEventError("whirlpool pull", exception);
                return Vector3.zero;
            }
        }

        /// <summary>
        /// A prop that is pulled along the floor of a pool's area stays in the air as far as the sound goes: the
        /// whoosh ends and the quiet LockDoorStop plays, once (like the landing of any other throw).
        /// </summary>
        private static void FxFloorContact(CarryState state, Vector3 c)
        {
            EventFx fx = state.Fx;
            if (fx == null || fx.FloorTouched)
            {
                return;
            }

            fx.FloorTouched = true;
            StopFlightSound(state);
            PlaySoundAt(c, FloorSoundName, FloorSoundVolume);
        }

        /// <summary>
        /// What Whirlpool.Update does when it catches an entity, for a prop: the pool takes this one prop and
        /// closes (layer 19, Close(): it shrinks and is destroyed), so a pool takes one prop. The prop comes out of
        /// another whirlpool of the flood, picked at random; that pool closes too once the prop is out (as the new
        /// whirlpool of Whirlpool.Teleport does). Only when no other whirlpool is left does it happen as in
        /// Whirlpool.Teleport: a new whirlpool forms at a random cell and closes again when the prop is out.
        /// </summary>
        private void FxBeginSink(CarryState state, EventFx fx, Whirlpool pool, Vector3 centre)
        {
            fx.Sinking = true;
            fx.SinkRising = false;
            fx.SinkPool = pool;
            fx.SinkCentre = centre;
            fx.SinkPercent = 1f;
            fx.KeepLanding = true;
            fx.HasExit = false;
            fx.ExitPool = null;
            fx.ExitOther = null;
            StopFlightSound(state);
            CloseWhirlpool(pool);

            string exitKind = null;
            Vector3 otherFloor;
            Whirlpool otherPool;
            Cell exit;
            if (TryPickOtherWhirlpool(state.Environment, pool, out otherFloor, out otherPool))
            {
                fx.HasExit = true;
                fx.ExitFloor = otherFloor;
                fx.ExitOther = otherPool;
                exitKind = "another whirlpool";
            }
            else if (TryPickWhirlpoolExit(state.Environment, out exit))
            {
                fx.HasExit = true;
                fx.ExitFloor = exit.FloorWorldPosition;
                fx.ExitPool = CreateExitWhirlpool(pool, fx.ExitFloor);
                exitKind = "a new whirlpool (no other one is left)";
            }

            Debug.Log("[Rebuild the School] A whirlpool took " + state.Root.name + (fx.HasExit
                ? "; it comes out of " + exitKind + " at " + fx.ExitFloor + "." : "; there is no place to come out."));
        }

        /// <summary>
        /// One of the other whirlpools of the flood, at random: not the one that took the prop, none that is
        /// closing (layer 19: used up, or the exit of a character) and none in an event spawner room.
        /// </summary>
        private bool TryPickOtherWhirlpool(
            EnvironmentController environment, Whirlpool taken, out Vector3 floor, out Whirlpool pool)
        {
            floor = Vector3.zero;
            pool = null;
            List<Whirlpool> options = new List<Whirlpool>();
            for (int i = 0; i < _whirlpools.Length; i++)
            {
                Whirlpool other = _whirlpools[i];
                if (other == null || other == taken || !other.isActiveAndEnabled
                    || other.gameObject.layer == WhirlpoolClosingLayer)
                {
                    continue;
                }

                Cell cell;
                if (TryGetCell(environment, other.transform.position, out cell) && IsEventSpawnerCell(cell))
                {
                    continue;
                }

                options.Add(other);
            }

            if (options.Count == 0)
            {
                return false;
            }

            pool = options[UnityEngine.Random.Range(0, options.Count)];
            floor = pool.transform.position;
            return true;
        }

        /// <summary>A whirlpool that has caught something: no more catching (layer 19) and it shrinks away.</summary>
        private static void CloseWhirlpool(Whirlpool pool)
        {
            if (pool == null)
            {
                return;
            }

            pool.gameObject.layer = WhirlpoolClosingLayer;
            System.Collections.IEnumerator routine = null;
            try
            {
                if (WhirlpoolCloseMethod != null)
                {
                    routine = WhirlpoolCloseMethod.Invoke(pool, null) as System.Collections.IEnumerator;
                }
            }
            catch (Exception)
            {
                routine = null;
            }

            if (routine != null)
            {
                // Started next to whatever the pool already runs (StartClose would stop all of it).
                pool.StartCoroutine(routine);
            }
            else
            {
                pool.StartClose();
            }
        }

        /// <summary>
        /// The new whirlpool of Whirlpool.Teleport: it forms for 3 s on layer 19 (it catches nothing), is listed in
        /// the flood so the flood removes it at its end, and is closed (StartClose) when the prop is out.
        /// </summary>
        private static Whirlpool CreateExitWhirlpool(Whirlpool source, Vector3 floor)
        {
            try
            {
                if (source == null || source.whirlpoolPre == null)
                {
                    return null;
                }

                Whirlpool exit = UnityEngine.Object.Instantiate<Whirlpool>(source.whirlpoolPre);
                exit.ec = source.ec;
                exit.FormTime = WhirlpoolExitFormSeconds;
                exit.transform.position = floor;
                exit.whirlpoolPre = source.whirlpoolPre;
                exit.flood = source.flood;
                exit.gameObject.layer = WhirlpoolClosingLayer;
                if (source.flood != null && source.flood.whirlpools != null)
                {
                    source.flood.whirlpools.Add(exit);
                }

                return exit;
            }
            catch (Exception exception)
            {
                LogEventError("exit whirlpool", exception);
                return null;
            }
        }

        /// <summary>
        /// The prop slides to the middle of the pool and sinks (0.4 s, the sink speed of Whirlpool.Teleport; it
        /// goes down by its own height, the floor hides it), jumps to the other whirlpool and rises out of it (0.4 s).
        /// It lands there like any thrown prop. Vanilla lowers the entity's height the same way.
        /// </summary>
        private bool FxStepSink(CarryState state, EventFx fx, float dt)
        {
            float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
            Vector3 half = state.FlightFullHalf;
            float fullDrop = 2f * half.y + WhirlpoolDropMargin;
            float step = dt * timeScale * WhirlpoolSinkSpeed;
            Vector3 c = state.FlightPosition + state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;

            if (!fx.SinkRising)
            {
                Vector3 centre = fx.SinkPool != null ? fx.SinkPool.transform.position : fx.SinkCentre;
                fx.SinkCentre = centre;
                c.x = Mathf.MoveTowards(c.x, centre.x, WhirlpoolGlideSpeed * dt);
                c.z = Mathf.MoveTowards(c.z, centre.z, WhirlpoolGlideSpeed * dt);
                fx.SinkPercent = Mathf.Max(WhirlpoolSinkFloor, fx.SinkPercent - step);
                c.y = state.FlightFloorY + half.y - fullDrop * SinkDepth(fx.SinkPercent);
                FxMoveSinking(state, c);
                if (fx.SinkPercent > WhirlpoolSinkFloor + 0.0001f)
                {
                    return true;
                }

                // At the bottom: the jump to the new whirlpool (Whirlpool.Teleport: subject.Teleport(newPos)).
                fx.SinkRising = true;
                if (fx.HasExit)
                {
                    state.FlightFloorY = fx.ExitFloor.y;
                    c = new Vector3(fx.ExitFloor.x, fx.ExitFloor.y + half.y - fullDrop, fx.ExitFloor.z);
                    FxMoveSinking(state, c);
                }

                return true;
            }

            fx.SinkPercent = Mathf.Min(1f, fx.SinkPercent + step);
            c.y = state.FlightFloorY + half.y - fullDrop * SinkDepth(fx.SinkPercent);
            FxMoveSinking(state, c);
            if (fx.SinkPercent < 1f - 0.0001f)
            {
                return true;
            }

            // Out: a whirlpool that was formed for this prop closes (newPool.StartClose()), and so does the other
            // whirlpool of the flood it came up out of (unless it is closing already: two Close() would fight over
            // its scale); the prop lands where it stands.
            Whirlpool exit = fx.ExitPool;
            Whirlpool other = fx.ExitOther;
            fx.ExitPool = null;
            fx.ExitOther = null;
            fx.Sinking = false;
            fx.SinkRising = false;
            fx.SinkPool = null;
            if (exit != null)
            {
                exit.StartClose();
            }

            if (other != null && other.gameObject.layer != WhirlpoolClosingLayer)
            {
                CloseWhirlpool(other);
            }

            state.Landed = true;
            Debug.Log("[Rebuild the School] " + state.Root.name + " came up out of a whirlpool at " + c + ".");
            return true;
        }

        /// <summary>1 at the top of the ride (sink percent 1), 0 at the bottom (the floor percent).</summary>
        private static float SinkDepth(float percent)
        {
            return Mathf.Clamp01((1f - percent) / (1f - WhirlpoolSinkFloor));
        }

        private static void FxMoveSinking(CarryState state, Vector3 centre)
        {
            state.FlightPosition = centre - state.FlightCenterOffset;
            state.Body.MovePosition(state.FlightPosition);
        }

        /// <summary>
        /// The cell a prop comes out at: a random one, as Whirlpool.Teleport picks it (the cells that are not off
        /// limits; ConvertEntityUnsafeCells moves it to a safe cell of its room), without the cells that have hard
        /// coverage when there are others, and never in an event spawner room.
        /// </summary>
        private static bool TryPickWhirlpoolExit(EnvironmentController environment, out Cell exit)
        {
            exit = null;
            if (environment == null)
            {
                return false;
            }

            // AllTilesNoGarbage hands out a shared buffer (the flood itself uses it): it is copied before it is changed.
            List<Cell> cells = new List<Cell>(environment.AllTilesNoGarbage(false, false));
            if (cells.Count == 0)
            {
                cells = new List<Cell>(environment.AllTilesNoGarbage(false, true));
            }

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (IsEventSpawnerCell(cells[i]))
                {
                    cells.RemoveAt(i);
                }
            }

            if (cells.Count == 0)
            {
                return false;
            }

            List<Cell> pick = new List<Cell>(1);
            pick.Add(cells[UnityEngine.Random.Range(0, cells.Count)]);
            pick.ConvertEntityUnsafeCells();
            exit = pick.Count > 0 && !IsEventSpawnerCell(pick[0]) ? pick[0] : null;
            return exit != null;
        }

        /// <summary>A cell of an event spawner room (where the Student Shuffle crowd comes from): no prop goes there.</summary>
        private static bool IsEventSpawnerCell(Cell cell)
        {
            return cell != null && !cell.Null && cell.room != null && cell.room.category == RoomCategory.EventSpawner;
        }

        /// <summary>True when the floor plan of the box (its centre and half size) lies over a cell of an event spawner room.</summary>
        private static bool BoxTouchesEventSpawner(EnvironmentController environment, Vector3 centre, Vector3 half)
        {
            if (environment == null)
            {
                return false;
            }

            // The corners and the points between them, never more than a cell apart.
            int stepsX = Mathf.Max(1, Mathf.CeilToInt(2f * half.x / WorldTileWidth));
            int stepsZ = Mathf.Max(1, Mathf.CeilToInt(2f * half.z / WorldTileWidth));
            for (int i = 0; i <= stepsX; i++)
            {
                for (int j = 0; j <= stepsZ; j++)
                {
                    Vector3 point = new Vector3(
                        centre.x - half.x + 2f * half.x * i / stepsX,
                        centre.y,
                        centre.z - half.z + 2f * half.z * j / stepsZ);
                    Cell cell;
                    if (TryGetCell(environment, point, out cell) && IsEventSpawnerCell(cell))
                    {
                        return true;
                    }
                }
            }

            return false;
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
                Debug.Log("[Rebuild the School] The student is gone; " + state.Root.name + " falls.");
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
                Debug.Log("[Rebuild the School] " + state.Root.name + " landed on a student and rides on its head.");
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
                Debug.Log("[Rebuild the School] The student walks into an event spawner room; " + state.Root.name
                    + " does not follow and falls.");
                return false;
            }

            state.FlightPosition = next - state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;
            state.Body.MovePosition(state.FlightPosition);
            return true;
        }

        private StudentCrowd_Follower FindFollowerUnder(EnvironmentController environment, Vector3 c, Vector3 half)
        {
            // A prop as tall as the room cannot sit on a head under the ceiling; nor one that is at a spawner room.
            if (_followers.Length == 0 || 2f * half.y + 0.2f >= FlightCeilingHeight
                || BoxTouchesEventSpawner(environment, c, half))
            {
                return null;
            }

            float reach = CrowdCatchRadius + Mathf.Max(half.x, half.z) * 0.5f;
            float bestSqr = reach * reach;
            StudentCrowd_Follower best = null;
            for (int i = 0; i < _followers.Length; i++)
            {
                StudentCrowd_Follower follower = _followers[i];
                if (follower == null || !follower.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 position = follower.transform.position;
                float dx = position.x - c.x;
                float dz = position.z - c.z;
                float sqr = dx * dx + dz * dz;
                Cell followerCell;
                if (sqr >= bestSqr || c.y - half.y > position.y + _crowdHeadOffset + CrowdCatchAbove
                    || (TryGetCell(environment, position, out followerCell) && IsEventSpawnerCell(followerCell)))
                {
                    continue;
                }

                best = follower;
                bestSqr = sqr;
            }

            return best;
        }

        // ------------------------------------------------------------------------------------------
        // Characters walk around props
        // ------------------------------------------------------------------------------------------
        // The vanilla Navigator moves a character along its path points by plain transform.position changes,
        // so nothing but the walls ever stopped it and props were simply walked through. A character whose next
        // leg crosses a prop now gets a detour: one or two corner points of the obstacle's rectangle (the
        // prop, or props standing too close together to pass between, plus the character's radius) are put in
        // front of that leg in Navigator.destinationPoints (a protected list). Every leg of the detour is
        // checked against the walls and against the other props; when no such detour exists the character
        // walks on as before. A detour is left alone until its first point is reached, and a character that
        // keeps circling (or changes plan too often) walks on straight for a while. Nobody kicks, pushes or
        // launches a prop.

        private void TickNpcAvoidance()
        {
            EnvironmentController environment = _player.ec;
            if (NavigatorPointsField == null || environment == null || environment.Npcs == null
                || environment.Npcs.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextAvoidScanTime)
            {
                _nextAvoidScanTime = now + AvoidScanInterval;
                RefreshAvoidProps(environment);
                ForgetAvoidStates();
            }

            if (_avoidProps.Count == 0)
            {
                return;
            }

            List<NPC> npcs = environment.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                try
                {
                    DetourAroundProps(npcs[i], environment);
                }
                catch (Exception exception)
                {
                    LogEventError("character walking around a prop", exception);
                }
            }
        }

        /// <summary>
        /// The footprints (renderer bounds on the floor plane) of the props near any character, a couple of
        /// times per second. Props that are carried or in the air are not obstacles; they are when they land.
        /// </summary>
        private void RefreshAvoidProps(EnvironmentController environment)
        {
            _avoidProps.Clear();
            List<NPC> npcs = environment.Npcs;
            _avoidNpcPositions.Clear();
            for (int n = 0; n < npcs.Count; n++)
            {
                NPC npc = npcs[n];
                if (npc != null)
                {
                    _avoidNpcPositions.Add(npc.transform.position);
                }
            }

            if (_avoidNpcPositions.Count == 0)
            {
                return;
            }

            float reachSqr = AvoidScanRadius * AvoidScanRadius;
            for (int i = 0; i < _proxies.Count; i++)
            {
                // The registry's own position and box (the box is kept until the prop moves), so a prop far from every
                // character costs a few distance tests, and a near one costs nothing more once its box is known.
                ProxyProp proxy = _proxies[i];
                Vector3 position = proxy.Position;
                bool near = false;
                for (int n = 0; n < _avoidNpcPositions.Count && !near; n++)
                {
                    float dx = _avoidNpcPositions[n].x - position.x;
                    float dz = _avoidNpcPositions[n].z - position.z;
                    near = dx * dx + dz * dz <= reachSqr;
                }

                if (!near || proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy
                    || (_held != null && proxy.Root == _held.Root) || IsReleased(proxy.Root))
                {
                    continue;
                }

                Bounds bounds = new Bounds();
                if (!ProxyBox(proxy, out bounds)
                    || bounds.max.y < AvoidBandBottom || bounds.min.y > AvoidBandTop
                    || bounds.size.x > AvoidMaxSize || bounds.size.z > AvoidMaxSize)
                {
                    continue;
                }

                PropRect rect = new PropRect();
                rect.Root = proxy.Root;
                rect.MinX = bounds.min.x;
                rect.MaxX = bounds.max.x;
                rect.MinZ = bounds.min.z;
                rect.MaxZ = bounds.max.z;
                _avoidProps.Add(rect);
            }
        }

        /// <summary>
        /// One character, every frame: look at the leg it is walking (its position to its next path point)
        /// and, when a prop is in the way, put the corner points of a detour in front of it.
        /// </summary>
        private void DetourAroundProps(NPC npc, EnvironmentController environment)
        {
            Navigator navigator = npc != null && npc.isActiveAndEnabled ? npc.Navigator : null;
            if (navigator == null || !navigator.HasDestination)
            {
                return;
            }

            Entity entity = navigator.Entity;
            if (entity == null || !entity.isActiveAndEnabled || entity.Frozen || entity.InteractionDisabled)
            {
                return;
            }

            List<Vector3> points = NavigatorPointsField.GetValue(navigator) as List<Vector3>;
            if (points == null || points.Count == 0 || points.Count > AvoidMaxPoints)
            {
                return;
            }

            AvoidState state = StateOf(npc);
            float now = Time.time;
            if (now < state.BlockedUntil || now < state.RetryAt)
            {
                return;
            }

            Vector3 start = navigator.transform.position;
            if (state.HasCommit)
            {
                // The detour put in front of this character is still being walked: leave it alone.
                if (Distance2D(points[0], state.Commit) < 0.0001f)
                {
                    return;
                }

                // Its first point is gone: reached, or the Navigator built a new path.
                state.HasCommit = false;
                if (Distance2D(start, state.Commit) < AvoidReach && ReachedAgain(npc, state, state.Commit, now))
                {
                    return;
                }
            }

            Vector3 next = points[0];
            float clearance = Mathf.Clamp(navigator.Radius, AvoidMinClearance, AvoidMaxClearance) + AvoidMargin;
            float hard = clearance - AvoidSlack;

            // The props this leg can touch. A prop blocks the leg when the leg comes closer to it than `hard`;
            // props whose zones overlap form one obstacle (nobody fits between them) and a detour goes round
            // all of it. The detour leaves the leg's box, so the box grows until the obstacle fits in it.
            float segLowX = Mathf.Min(start.x, next.x) - clearance;
            float segHighX = Mathf.Max(start.x, next.x) + clearance;
            float segLowZ = Mathf.Min(start.z, next.z) - clearance;
            float segHighZ = Mathf.Max(start.z, next.z) + clearance;
            float lowX = segLowX;
            float highX = segHighX;
            float lowZ = segLowZ;
            float highZ = segHighZ;
            int blocker = -1;
            PropRect corners = new PropRect();
            for (int expand = 0; expand < 3; expand++)
            {
                CollectNearProps(start, hard, lowX, highX, lowZ, highZ);
                blocker = FirstBlocker(start, next);
                if (blocker < 0)
                {
                    return;
                }

                corners = ClusterBounds(blocker, clearance + AvoidPad);
                float boxLowX = Mathf.Min(segLowX, corners.MinX - clearance);
                float boxHighX = Mathf.Max(segHighX, corners.MaxX + clearance);
                float boxLowZ = Mathf.Min(segLowZ, corners.MinZ - clearance);
                float boxHighZ = Mathf.Max(segHighZ, corners.MaxZ + clearance);
                if (boxLowX >= lowX && boxHighX <= highX && boxLowZ >= lowZ && boxHighZ <= highZ)
                {
                    break;
                }

                lowX = Mathf.Min(lowX, boxLowX);
                highX = Mathf.Max(highX, boxHighX);
                lowZ = Mathf.Min(lowZ, boxLowZ);
                highZ = Mathf.Max(highZ, boxHighZ);
            }

            if (InsideCluster(next.x, next.z))
            {
                // The next path point lies inside the obstacle (a cell centre under a table). The destination
                // itself cannot be moved; any other point is skipped if the next one can be walked to
                // straight, else it moves to the nearest free spot beside the obstacle.
                if (points.Count < 2 || !ChangeAllowed(state, now))
                {
                    return;
                }

                if (LegClear(environment, start, points[1], false))
                {
                    points.RemoveAt(0);
                }
                else
                {
                    Vector3 moved;
                    if (TryPushOut(environment, start, next, corners, out moved))
                    {
                        points[0] = moved;
                        state.Commit = moved;
                        state.HasCommit = true;
                    }
                }

                return;
            }

            AvoidCornerX[0] = corners.MinX;
            AvoidCornerZ[0] = corners.MinZ;
            AvoidCornerX[1] = corners.MaxX;
            AvoidCornerZ[1] = corners.MinZ;
            AvoidCornerX[2] = corners.MaxX;
            AvoidCornerZ[2] = corners.MaxZ;
            AvoidCornerX[3] = corners.MinX;
            AvoidCornerZ[3] = corners.MaxZ;

            // One corner, or two neighbouring ones: the shortest way around that is clear all along. When the
            // far end of the leg is cluttered with other props, the second pass accepts a way that is clear up
            // to the last corner and past this obstacle; the next one gets its own detour when the character is there.
            float bestLength = float.MaxValue;
            int bestFirst = -1;
            int bestSecond = -1;
            for (int pass = 0; pass < 2 && bestFirst < 0; pass++)
            {
                for (int first = 0; first < 4; first++)
                {
                    for (int variant = 0; variant < 3; variant++)
                    {
                        int second = variant == 0 ? -1 : (variant == 1 ? (first + 1) & 3 : (first + 3) & 3);
                        float length;
                        if (DetourClear(environment, start, next, first, second, pass == 1, out length)
                            && length < bestLength)
                        {
                            bestLength = length;
                            bestFirst = first;
                            bestSecond = second;
                        }
                    }
                }
            }

            if (bestFirst < 0)
            {
                // Wall to wall, or a cluster of props: no way round, so the character walks on as it always did.
                state.RetryAt = now + AvoidRetryDelay;
                return;
            }

            if (!ChangeAllowed(state, now))
            {
                return;
            }

            if (bestSecond >= 0)
            {
                points.Insert(0, new Vector3(AvoidCornerX[bestSecond], next.y, AvoidCornerZ[bestSecond]));
            }

            Vector3 firstPoint = new Vector3(AvoidCornerX[bestFirst], next.y, AvoidCornerZ[bestFirst]);
            points.Insert(0, firstPoint);
            state.Commit = firstPoint;
            state.HasCommit = true;
            if (Time.unscaledTime >= _nextAvoidLogTime)
            {
                _nextAvoidLogTime = Time.unscaledTime + AvoidLogInterval;
                Transform blockRoot = _avoidNear[blocker].Raw.Root;
                Debug.Log("[Rebuild the School] " + npc.name + " walks around "
                    + (blockRoot != null ? blockRoot.name : "a prop") + " (" + (bestSecond >= 0 ? 2 : 1)
                    + " detour point(s), " + bestLength.ToString("0.0") + " units).");
            }
        }

        private AvoidState StateOf(NPC npc)
        {
            int id = npc.GetInstanceID();
            AvoidState state;
            if (!_avoidStates.TryGetValue(id, out state))
            {
                state = new AvoidState();
                _avoidStates[id] = state;
            }

            state.LastFrame = Time.frameCount;
            return state;
        }

        private void ForgetAvoidStates()
        {
            if (_avoidStates.Count == 0)
            {
                return;
            }

            _avoidForget.Clear();
            foreach (KeyValuePair<int, AvoidState> pair in _avoidStates)
            {
                if (Time.frameCount - pair.Value.LastFrame > AvoidForgetFrames)
                {
                    _avoidForget.Add(pair.Key);
                }
            }

            for (int i = 0; i < _avoidForget.Count; i++)
            {
                _avoidStates.Remove(_avoidForget[i]);
            }
        }

        /// <summary>
        /// Guard against detours that never end: a character has only so many plan changes per window, and a
        /// corner it keeps coming back to means it is circling. Either way it walks on straight for a while.
        /// </summary>
        private static bool ChangeAllowed(AvoidState state, float now)
        {
            if (now > state.WindowEnd)
            {
                state.WindowEnd = now + AvoidWindow;
                state.Changes = 0;
            }

            if (state.Changes >= AvoidMaxChanges)
            {
                state.BlockedUntil = state.WindowEnd;
                return false;
            }

            state.Changes++;
            return true;
        }

        private bool ReachedAgain(NPC npc, AvoidState state, Vector3 corner, float now)
        {
            int visits = 0;
            for (int i = 0; i < state.ReachedCount; i++)
            {
                if (now - state.ReachedAt[i] < AvoidLoopWindow && Distance2D(state.Reached[i], corner) < AvoidSameCorner)
                {
                    visits++;
                }
            }

            state.Reached[state.ReachedNext] = corner;
            state.ReachedAt[state.ReachedNext] = now;
            state.ReachedNext = (state.ReachedNext + 1) % state.Reached.Length;
            state.ReachedCount = Mathf.Min(state.ReachedCount + 1, state.Reached.Length);
            if (visits < AvoidLoopVisits)
            {
                return false;
            }

            state.BlockedUntil = now + AvoidLoopPause;
            if (Time.unscaledTime >= _nextAvoidLogTime)
            {
                _nextAvoidLogTime = Time.unscaledTime + AvoidLogInterval;
                Debug.Log("[Rebuild the School] " + npc.name + " keeps circling props; it walks on straight for "
                    + AvoidLoopPause.ToString("0") + " s.");
            }

            return true;
        }

        /// <summary>
        /// The props within the box, each with the zone a leg must stay out of. A character that already stands
        /// closer to a prop than that keeps the margin it has (a prop it touches is ignored), so it is never
        /// forced the long way round a prop it is next to.
        /// </summary>
        private void CollectNearProps(Vector3 start, float hard, float lowX, float highX, float lowZ, float highZ)
        {
            _avoidNear.Clear();
            for (int i = 0; i < _avoidProps.Count; i++)
            {
                PropRect prop = _avoidProps[i];
                if (prop.MaxX < lowX || prop.MinX > highX || prop.MaxZ < lowZ || prop.MinZ > highZ)
                {
                    continue;
                }

                float zone = hard;
                float away = DistanceToRect(prop, start.x, start.z);
                if (away < hard + AvoidEpsilon)
                {
                    if (away < AvoidTouch)
                    {
                        continue;
                    }

                    zone = away - AvoidShrink;
                }

                AvoidNearProp item = new AvoidNearProp();
                item.Raw = prop;
                item.Hard = GrowRect(prop, zone);
                _avoidNear.Add(item);
            }
        }

        /// <summary>The prop whose zone the leg enters first, or -1.</summary>
        private int FirstBlocker(Vector3 start, Vector3 next)
        {
            int blocker = -1;
            float blockerEntry = float.MaxValue;
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                float entry;
                if (SegmentEntersRect(start.x, start.z, next.x, next.z, _avoidNear[i].Hard, out entry)
                    && entry < blockerEntry)
                {
                    blocker = i;
                    blockerEntry = entry;
                }
            }

            return blocker;
        }

        /// <summary>
        /// Marks the obstacle the blocking prop belongs to (props whose zones overlap, transitively) and returns
        /// its outline: the footprints' bounds grown by offset. A detour uses the corners of that rectangle.
        /// </summary>
        private PropRect ClusterBounds(int blocker, float offset)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                SetCluster(i, i == blocker);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < _avoidNear.Count; i++)
                {
                    if (_avoidNear[i].InCluster)
                    {
                        continue;
                    }

                    for (int j = 0; j < _avoidNear.Count; j++)
                    {
                        if (_avoidNear[j].InCluster && RectsOverlap(_avoidNear[i].Hard, _avoidNear[j].Hard))
                        {
                            SetCluster(i, true);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            PropRect bounds = _avoidNear[blocker].Raw;
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (!_avoidNear[i].InCluster)
                {
                    continue;
                }

                PropRect raw = _avoidNear[i].Raw;
                bounds.MinX = Mathf.Min(bounds.MinX, raw.MinX);
                bounds.MaxX = Mathf.Max(bounds.MaxX, raw.MaxX);
                bounds.MinZ = Mathf.Min(bounds.MinZ, raw.MinZ);
                bounds.MaxZ = Mathf.Max(bounds.MaxZ, raw.MaxZ);
            }

            return GrowRect(bounds, offset);
        }

        private void SetCluster(int index, bool value)
        {
            AvoidNearProp item = _avoidNear[index];
            item.InCluster = value;
            _avoidNear[index] = item;
        }

        private bool InsideCluster(float x, float z)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (_avoidNear[i].InCluster && InsideRect(_avoidNear[i].Hard, x, z))
                {
                    return true;
                }
            }

            return false;
        }

        private bool LegEntersCluster(Vector3 origin, Vector3 target)
        {
            for (int i = 0; i < _avoidNear.Count; i++)
            {
                float entry;
                if (_avoidNear[i].InCluster
                    && SegmentEntersRect(origin.x, origin.z, target.x, target.z, _avoidNear[i].Hard, out entry))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// start -> corner (-> second corner) -> next: every point and every leg is clear. With relaxed, the last
        /// leg only has to clear the walls and the obstacle that is being walked around, not the other props.
        /// </summary>
        private bool DetourClear(
            EnvironmentController environment, Vector3 start, Vector3 next, int first, int second,
            bool relaxed, out float length)
        {
            length = 0f;
            Vector3 a = new Vector3(AvoidCornerX[first], start.y, AvoidCornerZ[first]);
            if (!PointClear(environment, a) || !LegClear(environment, start, a, true))
            {
                return false;
            }

            length = Distance2D(start, a);
            Vector3 last = a;
            if (second >= 0)
            {
                Vector3 b = new Vector3(AvoidCornerX[second], start.y, AvoidCornerZ[second]);
                if (!PointClear(environment, b) || !LegClear(environment, a, b, true))
                {
                    return false;
                }

                length += Distance2D(a, b);
                last = b;
            }

            bool finalLegClear = relaxed
                ? LegClear(environment, last, next, false) && !LegEntersCluster(last, next)
                : LegClear(environment, last, next, true);
            if (!finalLegClear)
            {
                return false;
            }

            length += Distance2D(last, next);
            return true;
        }

        /// <summary>The nearest point on the rectangle's edge that is clear and can be walked to.</summary>
        private bool TryPushOut(
            EnvironmentController environment, Vector3 start, Vector3 inside, PropRect corners, out Vector3 moved)
        {
            moved = inside;
            float bestDistance = float.MaxValue;
            for (int side = 0; side < 4; side++)
            {
                float x = inside.x;
                float z = inside.z;
                if (side == 0)
                {
                    x = corners.MinX;
                }
                else if (side == 1)
                {
                    x = corners.MaxX;
                }
                else if (side == 2)
                {
                    z = corners.MinZ;
                }
                else
                {
                    z = corners.MaxZ;
                }

                Vector3 candidate = new Vector3(x, inside.y, z);
                float distance = Distance2D(inside, candidate);
                if (distance < bestDistance && PointClear(environment, candidate)
                    && LegClear(environment, start, candidate, true))
                {
                    bestDistance = distance;
                    moved = candidate;
                }
            }

            return bestDistance < float.MaxValue;
        }

        /// <summary>A spot a character may stand on: clear of the walls and outside every near prop's zone.</summary>
        private bool PointClear(EnvironmentController environment, Vector3 point)
        {
            if (FlightOverlaps(environment, point, AvoidWallHalf))
            {
                return false;
            }

            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (InsideRect(_avoidNear[i].Hard, point.x, point.z))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A straight leg that crosses no wall (doors are passable) and, if asked, no near prop's zone.</summary>
        private bool LegClear(EnvironmentController environment, Vector3 origin, Vector3 target, bool checkProps)
        {
            FlightHit wall;
            if (FlightGridSweep(environment, origin, AvoidWallHalf, target - origin, false, out wall))
            {
                return false;
            }

            if (checkProps)
            {
                for (int i = 0; i < _avoidNear.Count; i++)
                {
                    float entry;
                    if (SegmentEntersRect(origin.x, origin.z, target.x, target.z, _avoidNear[i].Hard, out entry))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToRect(PropRect rect, float x, float z)
        {
            float dx = Mathf.Max(Mathf.Max(rect.MinX - x, 0f), x - rect.MaxX);
            float dz = Mathf.Max(Mathf.Max(rect.MinZ - z, 0f), z - rect.MaxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static PropRect GrowRect(PropRect rect, float amount)
        {
            rect.MinX -= amount;
            rect.MaxX += amount;
            rect.MinZ -= amount;
            rect.MaxZ += amount;
            return rect;
        }

        private static bool RectsOverlap(PropRect a, PropRect b)
        {
            return a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
        }

        /// <summary>Strictly inside (the edge itself, where a detour runs, is outside).</summary>
        private static bool InsideRect(PropRect rect, float x, float z)
        {
            return x > rect.MinX + AvoidEpsilon && x < rect.MaxX - AvoidEpsilon
                && z > rect.MinZ + AvoidEpsilon && z < rect.MaxZ - AvoidEpsilon;
        }

        /// <summary>Does the segment run through the inside of the rectangle (touching its edge does not count)?</summary>
        private static bool SegmentEntersRect(
            float ax, float az, float bx, float bz, PropRect rect, out float entry)
        {
            entry = 0f;
            float tMin = 0f;
            float tMax = 1f;
            if (!SlabRange(ax, bx - ax, rect.MinX + AvoidEpsilon, rect.MaxX - AvoidEpsilon, ref tMin, ref tMax)
                || !SlabRange(az, bz - az, rect.MinZ + AvoidEpsilon, rect.MaxZ - AvoidEpsilon, ref tMin, ref tMax))
            {
                return false;
            }

            entry = tMin;
            return tMax - tMin > 0.0005f;
        }
    }

    /// <summary>
    /// The squash of Entity.Squish on a prop (vertical scale 0.5; during the last 3 s sin((3 - t) * 16 pi) * 0.2 + 0.5,
    /// as Entity.Update does). It restores the original scale and removes itself when the time is up.
    /// </summary>
    internal sealed class PropFx : MonoBehaviour
    {
        private const float SquishFactor = 0.5f;
        private const float WobbleSeconds = 3f;

        private Vector3 _baseScale = Vector3.one;
        private bool _captured;
        private float _squishTime;

        public bool Squished
        {
            get { return _squishTime > 0f; }
        }

        public static PropFx GetOrAdd(Transform root)
        {
            PropFx fx = root.GetComponent<PropFx>();
            if (fx == null || !fx.enabled)
            {
                fx = root.gameObject.AddComponent<PropFx>();
            }

            return fx;
        }

        public void Squish(float seconds)
        {
            Capture();
            _squishTime = Mathf.Max(seconds, _squishTime);
            Apply();
        }

        private void Capture()
        {
            if (!_captured)
            {
                _baseScale = transform.localScale;
                _captured = true;
            }
        }

        private void Update()
        {
            if (_squishTime > 0f)
            {
                _squishTime -= Time.deltaTime;
            }

            if (_squishTime <= 0f)
            {
                if (_captured)
                {
                    transform.localScale = _baseScale;
                    _captured = false;
                }

                enabled = false;
                Destroy(this);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float squash = _squishTime <= WobbleSeconds
                ? Mathf.Sin((WobbleSeconds - _squishTime) * 16f * Mathf.PI) * 0.2f + SquishFactor
                : SquishFactor;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * squash, _baseScale.z);
        }
    }
}
