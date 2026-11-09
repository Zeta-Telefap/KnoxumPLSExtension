using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The event half of the runtime: which events of the floor are running, and the Fx entry points the flight
    /// calls. Every hook is guarded, so a failing event is logged and never breaks the flight.
    /// </summary>
    public sealed partial class PropLifter
    {


        private const float EventScanInterval = 0.5f;
        // The events of the floor are children of the environment (a few dozen children, no tiles): their list is
        // taken again when that number changes and at the latest this often.
        private const float FloorEventsRescanSeconds = 5f;
        private const float EventErrorLogInterval = 5f;
        // A special state (ceiling, student, whirlpool) that somehow lasts this long is dropped.
        private const float EventSpecialMaxSeconds = 120f;
        // Every event the level builder gave the floor (EnvironmentController.AddEvent), running or not.
        private static readonly FieldInfo EnvironmentEventsField = typeof(EnvironmentController).GetField(
            "events", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly List<Collider> EventColliders = new List<Collider>();
        // When each kind of event error may be logged again (a failing step is logged on its own, not hidden by another).
        private static readonly Dictionary<string, float> EventErrorLogTimes = new Dictionary<string, float>();

        private float _nextEventScanTime;
        // The frame a carried prop was thrown or dropped: a wheel click in that frame was that throw, not a kick.
        private int _lastReleaseFrame = -1;
        private float _nextSquishTickTime;
        private float _nextAvoidScanTime;
        private float _nextAvoidLogTime;
        // The events of this floor (RefreshFloorEvents) and what was last written to the log about them.
        private readonly List<RandomEvent> _floorEvents = new List<RandomEvent>();
        private EnvironmentController _floorEventsEnvironment;
        private int _floorEventsChildren = -1;
        private float _nextFloorEventsScan;
        private string _loggedFloorEvents;
        // What was last written to the log about each event's objects (only the start and the end are logged).
        private int _loggedFlippers;
        private int _loggedWhirlpools;
        private int _loggedStudents;
        private EnvironmentController _fixturesEnvironment;

        private static void LogEventError(string what, Exception exception)
        {
            float next;
            if (EventErrorLogTimes.TryGetValue(what, out next) && Time.unscaledTime < next)
            {
                return;
            }

            EventErrorLogTimes[what] = Time.unscaledTime + EventErrorLogInterval;
            Debug.LogError("[Lifter Frenzy] Event handling (" + what + ") failed: " + exception);
        }

        /// <summary>One line when an event starts to run and one when it is over.</summary>
        private static void LogEventRunning(string what, bool running, ref bool logged)
        {
            if (running == logged)
            {
                return;
            }

            logged = running;
            Debug.Log("[Lifter Frenzy] " + what + (running ? " is running." : " is over."));
        }

        /// <summary>One line when an event's objects appear (count > 0) and one when they are all gone.</summary>
        private static void LogEventObjects(string what, int count, ref int logged)
        {
            bool changed = (count > 0) != (logged > 0);
            logged = count;
            if (changed)
            {
                Debug.Log("[Lifter Frenzy] " + what + (count > 0 ? ": " + count + " found." : ": none left."));
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
                Debug.Log("[Lifter Frenzy] Events of this floor: " + (names.Length > 0 ? names : "none") + ".");
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
                Debug.Log("[Lifter Frenzy] This floor has " + _belts.Length + " conveyor belt(s) and "
                    + _cranes.Length + " crane(s).");
            }
        }
    }
}
