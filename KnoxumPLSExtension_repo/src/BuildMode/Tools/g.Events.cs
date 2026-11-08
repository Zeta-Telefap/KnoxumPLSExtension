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
    /// that shove props out of their way. g.cs only calls the Fx* hooks; every hook is guarded, so a failing
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

        // Gravity Chaos.
        private const float GravityFlipRate = 1.6f;
        private const float GravityRollRate = 360f;
        private const float GravityLandFactor = 0.9f;

        // Flood: the numbers of Whirlpool (maxForce 20, sinks at 5 units, sinkSpeed 2, down to 20%).
        private const float WhirlpoolPull = 20f;
        private const float WhirlpoolSinkDistance = 5f;
        private const float WhirlpoolSinkSpeed = 2f;
        private const float WhirlpoolSinkFloor = 0.2f;
        private const float WhirlpoolRiseSeconds = 0.5f;
        private const float WhirlpoolGlideSpeed = 12f;
        private const float WhirlpoolFloorTolerance = 0.3f;
        private const float WhirlpoolMinHoldPull = 0.3f;
        private const float WhirlpoolSlideFriction = 6f;
        private const int WhirlpoolClosingLayer = 19;

        // Student Shuffle.
        private const float CrowdCatchRadius = 3.5f;
        private const float CrowdCatchAbove = 0.5f;
        private const float CrowdHeadOffsetDefault = 3.5f;
        private const float CrowdLiftSpeed = 20f;
        private const float CrowdCenterSpeed = 4f;

        // Characters that are after the player shove a prop that stands in their way.
        private const float NpcObstacleInterval = 0.15f;
        private const float NpcChaseRadius = 25f;
        private const float NpcProbeAhead = 1.5f;
        private const float NpcPropNear = 16f;
        private const float NpcShoveSpeed = 22f;
        private const float NpcShoveLift = 6f;
        private const float NpcShoveCooldown = 2f;
        private const int NpcShovesPerTick = 2;

        private static readonly FieldInfo ReflexSquishTimeField = typeof(DrReflex).GetField(
            "squishTime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SuperDoorSlabField = typeof(LockdownDoor).GetField(
            "door", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WhirlpoolSphereField = typeof(Whirlpool).GetField(
            "sphereCollider", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly List<Collider> EventColliders = new List<Collider>();
        private static float _nextEventErrorLogTime;

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
            public bool RollApplied;
            public bool Pinned;

            // Flood.
            public bool Pulled;
            public bool Sinking;
            public Whirlpool SinkPool;
            public Vector3 SinkCentre;
            public float SinkPercent = 1f;
            public bool PlayRise;

            // Student Shuffle.
            public bool Riding;
            public StudentCrowd_Follower Follower;
            public Vector3 RideOffset;
            public Vector3 RideLast;
            public Vector3 RideVelocity;

            // A prop that went through an event settles where it ends up, like a thrown one.
            public bool KeepLanding;
            public float SpecialSeconds;
        }

        private struct SquishTarget
        {
            public Transform Root;
            public Renderer[] Renderers;
        }

        private float _nextEventScanTime;
        // The frame a carried prop was thrown or dropped: a wheel click in that frame was that throw, not a kick.
        private int _lastReleaseFrame = -1;
        private float _nextSquishTickTime;
        private float _nextNpcObstacleTime;
        private float _crowdHeadOffset = CrowdHeadOffsetDefault;
        private GravityEvent _gravityEvent;
        private Whirlpool[] _whirlpools = new Whirlpool[0];
        private SphereCollider[] _poolSpheres = new SphereCollider[0];
        private GravityFlipper[] _flippers = new GravityFlipper[0];
        private Bounds[] _flipperBounds = new Bounds[0];
        private Balder_Entity[] _balders = new Balder_Entity[0];
        private DrReflex[] _reflexes = new DrReflex[0];
        private LockdownDoor[] _superDoors = new LockdownDoor[0];
        private StudentCrowd_Follower[] _followers = new StudentCrowd_Follower[0];
        private readonly List<SquishTarget> _squishTargets = new List<SquishTarget>();
        private readonly HashSet<Transform> _squishAirborne = new HashSet<Transform>();
        private readonly Dictionary<LockdownDoor, MeshRenderer> _superDoorSlabs =
            new Dictionary<LockdownDoor, MeshRenderer>();
        private readonly Dictionary<LockdownDoor, float> _superDoorBottoms = new Dictionary<LockdownDoor, float>();
        private readonly Dictionary<Transform, float> _shoveUntil = new Dictionary<Transform, float>();

        private static void LogEventError(string what, Exception exception)
        {
            if (Time.unscaledTime < _nextEventErrorLogTime)
            {
                return;
            }

            _nextEventErrorLogTime = Time.unscaledTime + EventErrorLogInterval;
            Debug.LogError("[Rebuild the School] Event handling (" + what + ") failed: " + exception);
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

            try
            {
                if (_held == null && _lastReleaseFrame != Time.frameCount && Input.GetMouseButtonDown(2))
                {
                    TryKick();
                }

                RefreshEventRegistries(false);
                TickSquishers();
                TickNpcObstacles();
            }
            catch (Exception exception)
            {
                LogEventError("update", exception);
            }
        }

        /// <summary>
        /// The event objects exist in the scene whether or not their event runs (GravityEvent.Active says so),
        /// so they are looked up a couple of times per second, never per fixed step.
        /// </summary>
        private void RefreshEventRegistries(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now < _nextEventScanTime)
            {
                return;
            }

            _nextEventScanTime = now + EventScanInterval;
            _gravityEvent = FindObjectOfType<GravityEvent>();
            _balders = FindObjectsOfType<Balder_Entity>();
            _reflexes = FindObjectsOfType<DrReflex>();
            _superDoors = FindObjectsOfType<LockdownDoor>();

            _whirlpools = FindObjectsOfType<Whirlpool>();
            _poolSpheres = new SphereCollider[_whirlpools.Length];
            for (int i = 0; i < _whirlpools.Length; i++)
            {
                _poolSpheres[i] = PoolSphere(_whirlpools[i]);
            }

            _flippers = FindObjectsOfType<GravityFlipper>();
            _flipperBounds = new Bounds[_flippers.Length];
            for (int i = 0; i < _flippers.Length; i++)
            {
                GravityFlipper flipper = _flippers[i];
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

            _followers = FindObjectsOfType<StudentCrowd_Follower>();
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
        // Wheel click: kick
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Not carrying: the prop under the crosshair is picked up and thrown in the same call, exactly like an
        /// RMB throw (speed, push of a hit character, Bullying rule, Whoosh and Bang). Carrying: the wheel click
        /// throws the carried prop (see HandlePlayerClick).
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
            if (!BeginCarry(target, _player, ray, out state))
            {
                return;
            }

            _held = state;
            ReleaseHeld(true, true);
        }

        /// <summary>
        /// A prop lying on the floor would land again in the very first step, so the kick goes a little upward
        /// and never down into the floor.
        /// </summary>
        private static Vector3 KickVelocity(CarryState state, Ray ray)
        {
            Vector3 direction = ray.direction;
            direction.y = Mathf.Max(direction.y, 0f) + KickUpward;
            direction.Normalize();
            return PlayerVelocity(state.Player) * 0.35f + direction * ThrowSpeed + Vector3.up * KickLift;
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
            if (_balders.Length == 0 && _reflexes.Length == 0 && _superDoors.Length == 0)
            {
                return;
            }

            CollectSquishTargets();
            TickBalders();
            TickHammers();
            TickSuperDoors();
        }

        /// <summary>Every prop that can be squished: the ones in flight and the movable props of the registry.</summary>
        private void CollectSquishTargets()
        {
            _squishTargets.Clear();
            _squishAirborne.Clear();
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state == null || state.Root == null || state.Renderers == null)
                {
                    continue;
                }

                _squishAirborne.Add(state.Root);
                SquishTarget target;
                target.Root = state.Root;
                target.Renderers = state.Renderers;
                _squishTargets.Add(target);
            }

            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                if (proxy == null || proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy
                    || (_held != null && proxy.Root == _held.Root) || _squishAirborne.Contains(proxy.Root))
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
                    || (!TryGetOwnerBounds(balder, true, out volume) && !TryGetOwnerBounds(balder, false, out volume)))
                {
                    continue;
                }

                float near = volume.extents.magnitude + SquishNearMargin;
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

        /// <summary>
        /// A super door (LockdownDoor) that is closing on a prop squishes it and goes straight back up. This
        /// runs whenever such a door closes, not only during the Lockdown event.
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

                Bounds slabBounds = slab.bounds;
                float bottom = slabBounds.min.y;
                float previous;
                bool known = _superDoorBottoms.TryGetValue(door, out previous);
                _superDoorBottoms[door] = bottom;
                if (!known || previous - bottom < SlabDescendEpsilon)
                {
                    continue;
                }

                float near = slabBounds.extents.magnitude + SquishNearMargin;
                bool crushed = false;
                for (int j = 0; j < _squishTargets.Count; j++)
                {
                    SquishTarget target = _squishTargets[j];
                    Bounds prop = new Bounds();
                    if (target.Root == null
                        || (target.Root.position - slabBounds.center).sqrMagnitude > near * near
                        || !CombinedBounds(target.Renderers, out prop) || !prop.Intersects(slabBounds))
                    {
                        continue;
                    }

                    crushed = true;
                    if (SquishProp(target.Root, DoorSquishSeconds))
                    {
                        Debug.Log("[Rebuild the School] A closing super door squished " + target.Root.name + ".");
                    }
                }

                if (crushed)
                {
                    // LockdownDoor.Open while the slab is moving only changes its target height: it rises at once.
                    door.Open(true, false);
                }
            }
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
        /// The state a released prop starts with. Gravity Chaos: a prop that is released while the player
        /// stands on the ceiling falls up, to the ceiling (followPlayerGravity).
        /// </summary>
        private void FxOnRelease(CarryState state, bool followPlayerGravity)
        {
            EventFx fx = new EventFx();
            fx.BaseRotation = state.Root.rotation;
            state.Fx = fx;
            try
            {
                RefreshEventRegistries(false);
                if (followPlayerGravity && _gravityEvent != null && _gravityEvent.Active
                    && PlayerIsFlipped(state.Player))
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
            if (fx == null || !(fx.Pinned || fx.Riding || fx.Sinking || fx.Pulled))
            {
                return false;
            }

            fx.SpecialSeconds += Time.deltaTime;
            return fx.SpecialSeconds < EventSpecialMaxSeconds;
        }

        private static void FxBeforeSettle(CarryState state)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return;
            }

            try
            {
                if (fx.KeepLanding)
                {
                    state.Thrown = true;
                }

                if (fx.PlayRise && state.Root != null)
                {
                    // The placement is checked at full size; the rise starts after it (FxAfterSettle).
                    PropFx.GetOrAdd(state.Root).SetUniform(1f);
                }
            }
            catch (Exception exception)
            {
                LogEventError("before settling", exception);
            }
        }

        private static void FxAfterSettle(CarryState state)
        {
            EventFx fx = state.Fx;
            if (fx == null || !fx.PlayRise)
            {
                return;
            }

            fx.PlayRise = false;
            try
            {
                if (state.Root != null)
                {
                    PropFx.GetOrAdd(state.Root).BeginGrow(WhirlpoolSinkFloor, WhirlpoolRiseSeconds);
                }
            }
            catch (Exception exception)
            {
                LogEventError("after settling", exception);
            }
        }

        /// <summary>Inside a whirlpool's pull (or going down it) a prop on the floor slides instead of settling.</summary>
        private static bool FxHoldsOnFloor(CarryState state)
        {
            EventFx fx = state.Fx;
            return fx != null && (fx.Sinking || fx.Pulled);
        }

        /// <summary>
        /// Riding a student or sinking into a whirlpool replace the flight; true when this step was handled
        /// here (StepFlight returns), false for an ordinary step.
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

                if (fx.Riding || _followers.Length > 0)
                {
                    return FxStepRide(state, fx, dt);
                }
            }
            catch (Exception exception)
            {
                LogEventError("special flight step", exception);
                fx.Sinking = false;
                fx.Riding = false;
                fx.Follower = null;
                FxResetScale(state);
            }

            return false;
        }

        /// <summary>A prop that leaves a whirlpool step half way (error, level change) must not stay shrunk.</summary>
        private static void FxResetScale(CarryState state)
        {
            try
            {
                PropFx scale = state != null && state.Root != null ? state.Root.GetComponent<PropFx>() : null;
                if (scale != null && scale.enabled)
                {
                    scale.SetUniform(1f);
                }
            }
            catch (Exception)
            {
                // Nothing is left to restore.
            }
        }

        // --- Gravity Chaos ---

        /// <summary>
        /// +1 = gravity pulls down, -1 = up. A flipper the prop touches is used up (GravityEvent.DestroyFlipper,
        /// like the player and the characters do) and turns the prop's gravity over smoothly; when the event
        /// ends gravity returns to normal.
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
            if (fx == null || (Mathf.Abs(fx.RollAngle) < 0.05f && !fx.RollApplied))
            {
                state.Body.MovePosition(state.FlightPosition);
                return;
            }

            Quaternion roll = Quaternion.AngleAxis(fx.RollAngle, fx.BaseRotation * Vector3.forward);
            state.Body.MovePosition(c - roll * state.FlightCenterOffset);
            state.Body.MoveRotation(roll * fx.BaseRotation);
            fx.RollApplied = Mathf.Abs(fx.RollAngle) >= 0.05f;
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
            }
            else if (!pinned && fx.Pinned)
            {
                fx.Pinned = false;
            }
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
        /// The pull of Whirlpool.Update: a drift toward the middle of the pool, strongest at the middle,
        /// (R - d) / R * 20 units per second, never more than the distance left. A wall in between stops it.
        /// At 5 units from the middle, on the floor, the prop starts to sink (FxStepSink).
        /// </summary>
        private Vector3 FxDrift(CarryState state, Vector3 c, Vector3 half, float dt)
        {
            EventFx fx = state.Fx;
            if (fx == null)
            {
                return Vector3.zero;
            }

            fx.Pulled = false;
            if (_whirlpools.Length == 0)
            {
                return Vector3.zero;
            }

            try
            {
                Bounds box = new Bounds(c, half * 2f);
                float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
                int bestIndex = -1;
                float bestPull = 0f;
                float bestFlat = 0f;
                Vector3 bestCentre = Vector3.zero;
                Vector3 bestDirection = Vector3.zero;
                for (int i = 0; i < _whirlpools.Length; i++)
                {
                    Vector3 centre;
                    float radius;
                    if (!WhirlpoolShape(i, out centre, out radius) || box.SqrDistance(centre) > radius * radius)
                    {
                        continue;
                    }

                    Vector3 flat = new Vector3(centre.x - c.x, 0f, centre.z - c.z);
                    float distance = flat.magnitude;
                    float pull = (radius - Mathf.Min(distance, radius)) / radius * WhirlpoolPull * timeScale;
                    FlightHit wall;
                    if (pull <= bestPull
                        || (FlightGridSweep(state.Environment, c, Vector3.zero, flat, false, out wall)
                            && wall.Kind == FlightHitKind.Wall))
                    {
                        continue;
                    }

                    bestIndex = i;
                    bestPull = pull;
                    bestFlat = distance;
                    bestCentre = centre;
                    bestDirection = distance > 0.001f ? flat / distance : Vector3.zero;
                }

                if (bestIndex < 0)
                {
                    return Vector3.zero;
                }

                if (bestFlat <= WhirlpoolSinkDistance && c.y - half.y <= state.FlightFloorY + WhirlpoolFloorTolerance)
                {
                    fx.Sinking = true;
                    fx.SinkPool = _whirlpools[bestIndex];
                    fx.SinkCentre = bestCentre;
                    fx.SinkPercent = 1f;
                    fx.KeepLanding = true;
                    StopFlightSound(state);
                    Debug.Log("[Rebuild the School] A whirlpool is sucking " + state.Root.name + " in.");
                    return Vector3.zero;
                }

                float speed = bestPull;
                if (speed * dt > bestFlat)
                {
                    speed = bestFlat / Mathf.Max(dt, 0.0001f);
                }

                Vector3 drift = bestDirection * speed;
                fx.Pulled = drift.sqrMagnitude >= WhirlpoolMinHoldPull * WhirlpoolMinHoldPull;
                return drift;
            }
            catch (Exception exception)
            {
                LogEventError("whirlpool pull", exception);
                return Vector3.zero;
            }
        }

        /// <summary>
        /// The prop slides to the middle of the pool and shrinks to 20% (0.4 s), then comes up at a random cell
        /// (the exit of Whirlpool.Teleport) and grows back (FxAfterSettle) while it settles there.
        /// </summary>
        private bool FxStepSink(CarryState state, EventFx fx, float dt)
        {
            float timeScale = state.Environment != null ? state.Environment.EnvironmentTimeScale : 1f;
            Vector3 centre = fx.SinkPool != null ? fx.SinkPool.transform.position : fx.SinkCentre;
            fx.SinkCentre = centre;
            Vector3 c = state.FlightPosition + state.FlightCenterOffset;
            c.x = Mathf.MoveTowards(c.x, centre.x, WhirlpoolGlideSpeed * dt);
            c.z = Mathf.MoveTowards(c.z, centre.z, WhirlpoolGlideSpeed * dt);
            state.FlightPosition = c - state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;
            state.Body.MovePosition(state.FlightPosition);

            fx.SinkPercent = Mathf.Max(WhirlpoolSinkFloor, fx.SinkPercent - dt * timeScale * WhirlpoolSinkSpeed);
            PropFx.GetOrAdd(state.Root).SetUniform(fx.SinkPercent);
            if (fx.SinkPercent > WhirlpoolSinkFloor + 0.0001f)
            {
                return true;
            }

            fx.Sinking = false;
            fx.SinkPool = null;
            fx.PlayRise = true;
            Vector3 landing = c;
            Cell exit;
            if (TryPickWhirlpoolExit(state.Environment, out exit))
            {
                Vector3 floor = exit.FloorWorldPosition;
                landing = new Vector3(floor.x, floor.y + state.FlightFullHalf.y, floor.z);
                state.FlightFloorY = floor.y;
            }

            state.FlightPosition = landing - state.FlightCenterOffset;
            state.Landed = true;
            PlaySoundAt(landing, FloorSoundName, FloorSoundVolume);
            Debug.Log("[Rebuild the School] " + state.Root.name + " came up out of a whirlpool at " + landing + ".");
            return true;
        }

        private static bool TryPickWhirlpoolExit(EnvironmentController environment, out Cell exit)
        {
            exit = null;
            List<Cell> cells = environment != null ? environment.AllTilesNoGarbage(false, false) : null;
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            // AllTilesNoGarbage hands out a shared buffer: take one cell at once and keep nothing else.
            List<Cell> pick = new List<Cell>(1);
            pick.Add(cells[UnityEngine.Random.Range(0, cells.Count)]);
            pick.ConvertEntityUnsafeCells();
            exit = pick.Count > 0 ? pick[0] : null;
            return exit != null;
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
                StudentCrowd_Follower follower = FindFollowerUnder(c, half);
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
            state.FlightPosition = next - state.FlightCenterOffset;
            state.FlightVelocity = Vector3.zero;
            state.Body.MovePosition(state.FlightPosition);
            return true;
        }

        private StudentCrowd_Follower FindFollowerUnder(Vector3 c, Vector3 half)
        {
            // A prop as tall as the room cannot sit on a head under the ceiling.
            if (_followers.Length == 0 || 2f * half.y + 0.2f >= FlightCeilingHeight)
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
                if (sqr >= bestSqr || c.y - half.y > position.y + _crowdHeadOffset + CrowdCatchAbove)
                {
                    continue;
                }

                best = follower;
                bestSqr = sqr;
            }

            return best;
        }

        // ------------------------------------------------------------------------------------------
        // Characters do not ignore props that block their way to the player
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// A character that is after the player (its destination is near the player) looks a few units ahead,
        /// along the way to its next path point. A prop in that box blocks it: the character kicks the prop
        /// out of the way, with the flight of a thrown prop that pushes nobody and that the player is not
        /// blamed for (NpcLaunched). Doors and windows already have their own reactions (open, break); props
        /// had none.
        /// </summary>
        private void TickNpcObstacles()
        {
            float now = Time.unscaledTime;
            if (now < _nextNpcObstacleTime)
            {
                return;
            }

            _nextNpcObstacleTime = now + NpcObstacleInterval;
            EnvironmentController environment = _player.ec;
            if (environment == null || environment.Npcs == null || _proxies.Count == 0)
            {
                return;
            }

            if (_shoveUntil.Count > 64)
            {
                _shoveUntil.Clear();
            }

            Vector3 playerPosition = _player.transform.position;
            List<NPC> npcs = environment.Npcs;
            int shoved = 0;
            for (int i = 0; i < npcs.Count && shoved < NpcShovesPerTick; i++)
            {
                NPC npc = npcs[i];
                Entity entity;
                Bounds ignored;
                if (!TryGetPushableNpc(npc, out entity, out ignored) || !npc.Navigator.HasDestination)
                {
                    continue;
                }

                Vector3 toPlayer = playerPosition - npc.Navigator.CurrentDestination;
                toPlayer.y = 0f;
                Vector3 position = entity.transform.position;
                Vector3 heading = npc.Navigator.NextPoint - position;
                heading.y = 0f;
                if (toPlayer.sqrMagnitude > NpcChaseRadius * NpcChaseRadius || heading.sqrMagnitude < 0.04f)
                {
                    continue;
                }

                heading.Normalize();
                float radius = Mathf.Max(1f, npc.Navigator.Radius);
                Vector3 ahead = position + heading * (radius + NpcProbeAhead);
                float side = 2f * (radius + 0.5f);
                Bounds probe = new Bounds(new Vector3(ahead.x, position.y, ahead.z), new Vector3(side, 8f, side));
                ProxyProp blocker = FindBlockingProp(probe, position, now);
                if (blocker != null && ShoveProp(blocker, npc, position, heading))
                {
                    shoved++;
                }
            }
        }

        private ProxyProp FindBlockingProp(Bounds probe, Vector3 from, float now)
        {
            ProxyProp best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                if (proxy == null || proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy
                    || (_held != null && proxy.Root == _held.Root)
                    || (proxy.Root.position - from).sqrMagnitude > NpcPropNear * NpcPropNear)
                {
                    continue;
                }

                float until;
                Bounds bounds = new Bounds();
                if ((_shoveUntil.TryGetValue(proxy.Root, out until) && now < until)
                    || IsAirborneRoot(proxy.Root)
                    || !CombinedBounds(proxy.Renderers, out bounds) || !bounds.Intersects(probe))
                {
                    continue;
                }

                float sqr = bounds.SqrDistance(from);
                if (sqr < bestSqr)
                {
                    best = proxy;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private bool ShoveProp(ProxyProp proxy, NPC npc, Vector3 origin, Vector3 heading)
        {
            // Also when the prop turns out to be unmovable: do not look at it again for a moment.
            _shoveUntil[proxy.Root] = Time.unscaledTime + NpcShoveCooldown;
            Candidate candidate;
            if (!TryBuildCandidate(_player, proxy.Root, proxy.EnvironmentObject, out candidate)
                || !CanBeginCarry(candidate, _player))
            {
                return false;
            }

            if (_hoveredRoot == proxy.Root)
            {
                ClearPickupHighlight();
            }

            CarryState state = null;
            try
            {
                Ray ray = new Ray(origin, heading);
                if (!BeginCarry(candidate, _player, ray, out state))
                {
                    return false;
                }

                state.Root.SetParent(state.Environment.transform, true);
                RestoreVisualGlow(state);
                BeginFlight(state, ray, true);
                state.FlightVelocity = heading * NpcShoveSpeed + Vector3.up * NpcShoveLift;
                state.FlightDrag = 0.15f;
                state.Age = 0f;
                state.Thrown = true;
                state.NpcLaunched = true;
                state.WallHits = 0;
                state.PushedNpcs = null;
                state.NextImpactSoundTime = 0f;
                FxOnRelease(state, false);
                _airborne.Add(state);
                StartFlightSound(state);
                InvalidateAim();
                PlaySoundAt(state.FlightPosition + state.FlightCenterOffset, BangSoundName, 1f);
                Debug.Log("[Rebuild the School] " + npc.name + " kicked " + proxy.Root.name
                    + ", which stood in its way to the player.");
                return true;
            }
            catch (Exception exception)
            {
                if (state != null)
                {
                    ReturnToOrigin(state);
                }

                LogEventError("character kicking a prop", exception);
                return false;
            }
        }
    }

    /// <summary>
    /// The scale effects an event puts on a prop: the squash of Entity.Squish (vertical scale 0.5; during the
    /// last 3 s sin((3 - t) * 16 pi) * 0.2 + 0.5, as Entity.Update does) and the uniform shrink / grow of a prop
    /// that goes down a whirlpool. It restores the original scale and removes itself when nothing is left.
    /// </summary>
    internal sealed class PropFx : MonoBehaviour
    {
        private const float SquishFactor = 0.5f;
        private const float WobbleSeconds = 3f;

        private Vector3 _baseScale = Vector3.one;
        private bool _captured;
        private float _squishTime;
        private float _uniform = 1f;
        private float _growTime;
        private float _growDuration = 1f;
        private float _growFrom = 1f;

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

        public void SetUniform(float factor)
        {
            Capture();
            _growTime = 0f;
            _uniform = factor;
            Apply();
        }

        public void BeginGrow(float from, float seconds)
        {
            Capture();
            _growFrom = from;
            _growDuration = Mathf.Max(0.01f, seconds);
            _growTime = _growDuration;
            _uniform = from;
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
            float dt = Time.deltaTime;
            if (_squishTime > 0f)
            {
                _squishTime -= dt;
            }

            if (_growTime > 0f)
            {
                _growTime -= dt;
                _uniform = _growTime <= 0f ? 1f : Mathf.Lerp(1f, _growFrom, _growTime / _growDuration);
            }

            if (_squishTime <= 0f && _growTime <= 0f && _uniform >= 0.999f)
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
            float vertical = _uniform;
            if (_squishTime > 0f)
            {
                float squash = _squishTime <= WobbleSeconds
                    ? Mathf.Sin((WobbleSeconds - _squishTime) * 16f * Mathf.PI) * 0.2f + SquishFactor
                    : SquishFactor;
                vertical *= squash;
            }

            transform.localScale = new Vector3(
                _baseScale.x * _uniform, _baseScale.y * vertical, _baseScale.z * _uniform);
        }
    }
}
