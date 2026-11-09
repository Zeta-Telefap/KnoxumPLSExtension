using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The aim under the crosshair: one fresh query per frame feeds both the glow and the G key, so the glowing prop
    /// is by construction the prop G picks.
    /// </summary>
    public sealed partial class PropLifter
    {


        private void UpdatePickupHighlight()
        {
            try
            {
                UpdatePickupHighlightCore();
            }
            catch (Exception exception)
            {
                // Runs every frame now, so one bad prop must not flood the log or break Update.
                if (Time.unscaledTime >= _nextHighlightErrorLogTime)
                {
                    _nextHighlightErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Lifter Frenzy] Pickup highlight failed: " + exception);
                }

                try
                {
                    ClearPickupHighlight();
                }
                catch (Exception)
                {
                    _hoverGlow = null;
                    _hoveredRoot = null;
                    _hoveredCandidate = null;
                }
            }
        }

        private void UpdatePickupHighlightCore()
        {
            // Fresh every frame: no polling interval, no hysteresis against the real target.
            Candidate aim = ResolveAimThisFrame(_player);
            float now = Time.unscaledTime;
            if (aim == null)
            {
                // The grace only bridges single-frame gaps (seams between meshes); it never
                // overrides a fresh target, which is handled below.
                if (_hoveredCandidate != null && _hoveredCandidate.Root != null
                    && now - _lastAimTime <= HighlightGraceTime)
                {
                    return;
                }

                ClearPickupHighlight();
                if (_suppressHoverRoot != null && now - _lastAimTime > SuppressReleaseTime)
                {
                    _suppressHoverRoot = null;
                }

                return;
            }

            _lastAimTime = now;
            if (_suppressHoverRoot != null)
            {
                if (_suppressHoverRoot == aim.Root)
                {
                    // Just placed: keep the prop un-tinted while the crosshair is still on it.
                    ClearPickupHighlight();
                    return;
                }

                // Once the player looks at something else, the placed prop may glow again later.
                _suppressHoverRoot = null;
            }

            if (_hoveredRoot != aim.Root)
            {
                ClearPickupHighlight();
                _hoveredRoot = aim.Root;
                _hoverGlow = new GlowState(aim.Root, aim.Renderers);
                if (VerboseAimLog)
                {
                    Debug.Log("[Lifter Frenzy] Aim -> " + DescribePath(aim.Root) + " ("
                        + aim.AimSource + ", " + aim.AimDistance.ToString("0.0") + " units).");
                }
            }

            _hoveredCandidate = aim;
            if (_hoverGlow != null)
            {
                _hoverGlow.SetColor(ValidPlacementColor);
            }
        }

        private void ClearPickupHighlight()
        {
            if (_hoverGlow != null)
            {
                _hoverGlow.Restore();
                _hoverGlow = null;
            }

            _hoveredRoot = null;
            _hoveredCandidate = null;
        }

        /// <summary>
        /// The single G entry point shared by PropLifter.Update and the PlayerClick hook. It runs the same
        /// per-frame aim query as the glow. Returns true when a prop is under the crosshair (picked, or
        /// refused with a logged reason); false when there is no prop target at all.
        /// </summary>
        internal bool TryPickupHighlighted(PlayerManager player)
        {
            if (_held != null || player == null)
            {
                return false;
            }

            // The fresh query wins. The glow is only a fallback for the instant the crosshair slips off
            // the prop that was glowing a moment ago, and it is re-validated before use.
            Candidate target = ResolveAimThisFrame(player);
            if (target == null && _hoveredCandidate != null && _hoveredCandidate.Root != null
                && Time.unscaledTime - _lastAimTime <= HighlightGraceTime
                && CanBeginCarry(_hoveredCandidate, player))
            {
                target = _hoveredCandidate;
            }

            if (target == null)
            {
                if (_lastNoTargetLogFrame != Time.frameCount)
                {
                    _lastNoTargetLogFrame = Time.frameCount;
                    Debug.Log("[Lifter Frenzy] G: no prop under the crosshair (" + DescribeAimInfo() + ").");
                }

                return false;
            }

            TryPickup(player, target);
            return true;
        }

        // ------------------------------------------------------------------------------------
        // Aim resolution. Everything that decides "which prop is under the crosshair" lives here.
        // ------------------------------------------------------------------------------------

        private void InvalidateAim()
        {
            _aimFrame = -1;
            _aimResult = null;
            _aimCache = null;
        }

        private string DescribeAimInfo()
        {
            if (_aimNote != null)
            {
                return _aimNote;
            }

            if (_aimBlocker == null)
            {
                return "nothing solid within " + PickupRange.ToString("0") + " units";
            }

            string text = "blocked by " + DescribePath(_aimBlocker.transform) + " (layer "
                + _aimBlocker.gameObject.layer + ") at " + _aimBlockerDistance.ToString("0.0");
            EnvironmentObject environmentObject = _aimBlocker.GetComponentInParent<EnvironmentObject>();
            Transform propRoot = environmentObject != null
                ? environmentObject.transform
                : FindSupportedPropRoot(_aimBlocker.transform);
            if (propRoot != null)
            {
                text += "; it looks like a prop (" + DescribePath(propRoot) + ") but cannot be carried: "
                    + DescribeCarryRejection(propRoot, environmentObject, _aimPlayer);
            }

            return text;
        }

        /// <summary>
        /// Diagnostic only (runs when G found nothing to pick): names the first check that makes a
        /// prop-looking object uncarriable, mirroring CanBeginCarry / TryBuildCandidate.
        /// </summary>
        private string DescribeCarryRejection(
            Transform root, EnvironmentObject environmentObject, PlayerManager player)
        {
            if (root == null || player == null || player.ec == null)
            {
                return "player or object missing";
            }

            string forbidden = ForbiddenPickupReason(root);
            if (forbidden != null)
            {
                return "excluded (" + forbidden + ")";
            }

            if (IsAirborneRoot(root))
            {
                return "still in flight";
            }

            if (!root.gameObject.activeInHierarchy)
            {
                return "inactive";
            }

            if (environmentObject != null)
            {
                Cell environmentCell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out environmentCell))
                {
                    return "environment object is not carriable (other level, door/tile/NPC/activity parent, or no cell)";
                }
            }
            else if (IsForbiddenPropHierarchy(root))
            {
                return "it sits inside a door, tile object, NPC, activity or environment object";
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds;
            if (renderers.Length == 0 || !CombinedBounds(renderers, out bounds))
            {
                return "no renderers with usable bounds";
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null && bodies[i].transform != root)
                {
                    return "a child has its own Rigidbody (" + DescribePath(bodies[i].transform) + ")";
                }
            }

            Cell cell;
            if (!TryGetCell(player.ec, root.position, out cell)
                && !TryGetCell(player.ec, bounds.center, out cell))
            {
                return "it is not inside any cell";
            }

            return "parent type, cell or coverage data do not allow carrying";
        }

        /// <summary>
        /// What the crosshair is on, computed at most once per frame and shared by the glow and G.
        /// Rule: the first solid thing on the camera's center ray decides. A prop must really be hit by
        /// the ray; a wall, door, window, NPC or immovable prop in front blocks it, so nothing is ever
        /// picked through geometry. Props the ray does not touch physically (no solid collider at all, or
        /// gaps in the collider) are matched by the boxes of their meshes as aim assist: collider-less props
        /// compete with a physical hit by distance, props that do have colliders are only assisted when the
        /// ray hit no movable prop. In every case the prop must be in front of the first solid hit.
        /// </summary>
        private Candidate ResolveAimThisFrame(PlayerManager player)
        {
            if (_aimFrame == Time.frameCount && _aimPlayer == player)
            {
                return _aimResult;
            }

            _aimFrame = Time.frameCount;
            _aimPlayer = player;
            _aimResult = null;
            _aimBlocker = null;
            _aimBlockerDistance = 0f;
            _aimNote = null;
            if (player == null || player.ec == null || _held != null)
            {
                _aimNote = "not available right now";
                return null;
            }

            Ray ray;
            if (!TryGetCameraRay(player, out ray))
            {
                _aimNote = "camera ray unavailable";
                return null;
            }

            try
            {
                _aimResult = ResolveAim(player, ray, out _aimBlocker, out _aimBlockerDistance);
            }
            catch (Exception exception)
            {
                _aimResult = null;
                _aimNote = "aim query failed: " + exception.Message;
                if (Time.unscaledTime >= _nextAimErrorLogTime)
                {
                    _nextAimErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Lifter Frenzy] Aim query failed: " + exception);
                }
            }

            return _aimResult;
        }

        private Candidate ResolveAim(
            PlayerManager player, Ray ray, out Collider blocker, out float blockerDistance)
        {
            blocker = null;
            blockerDistance = 0f;
            int count = Physics.RaycastNonAlloc(
                ray, RayHits, PickupRange, ~0, QueryTriggerInteraction.Ignore);
            if (count > 1)
            {
                Array.Sort(RayHits, 0, count, RayHitOrder);
            }

            Candidate physical = null;
            float solidDistance = PickupRange;
            for (int i = 0; i < count; i++)
            {
                Collider collider = RayHits[i].collider;
                if (collider == null || IsIgnoredForAim(collider, player))
                {
                    continue;
                }

                // The first solid hit decides: either it belongs to a movable prop (that prop is the
                // target) or it blocks everything behind it. No second chances further along the ray.
                solidDistance = RayHits[i].distance;
                if (TryCandidateFromCollider(player, collider, out physical))
                {
                    physical.AimDistance = solidDistance;
                    physical.AimSource = "collider";
                    physical.AimNormal = RayHits[i].normal;
                }
                else
                {
                    blocker = collider;
                    blockerDistance = solidDistance;
                }

                break;
            }

            Candidate assisted;
            float assistedDistance;
            if (TryFindAssistProp(player, ray, solidDistance - ProxyDepthEpsilon, physical != null,
                out assisted, out assistedDistance))
            {
                assisted.AimDistance = assistedDistance;
                assisted.AimSource = "mesh box";
                assisted.AimNormal = Vector3.zero;
                blocker = null;
                return assisted;
            }

            return physical;
        }

        private bool IsIgnoredForAim(Collider collider, PlayerManager player)
        {
            Transform hit = collider.transform;
            if (player != null && (hit == player.transform || hit.IsChildOf(player.transform)))
            {
                return true;
            }

            // Props that were just dropped or thrown are neither targets nor blockers while airborne.
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state != null && state.Root != null && !FxResumable(state)
                    && (hit == state.Root || hit.IsChildOf(state.Root)))
                {
                    return true;
                }
            }

            // A lever door that is stuck on a prop must not hide that prop: it could never be taken out from under it.
            return FxIsWaitingDoorCollider(collider);
        }

        private bool IsAirborneRoot(Transform root)
        {
            for (int i = 0; i < _airborne.Count; i++)
            {
                CarryState state = _airborne[i];
                if (state != null && state.Root == root)
                {
                    // A prop pinned to the ceiling or riding a student is not flying: it can be picked up again.
                    return !FxResumable(state);
                }
            }

            return false;
        }

        private bool TryCandidateFromCollider(PlayerManager player, Collider collider, out Candidate candidate)
        {
            candidate = null;
            if (player == null || player.ec == null || collider == null)
            {
                return false;
            }

            EnvironmentObject environmentObject = collider.GetComponentInParent<EnvironmentObject>();
            Transform root = environmentObject != null
                ? environmentObject.transform
                : FindSupportedPropRoot(collider.transform);
            return root != null && TryBuildCandidate(player, root, environmentObject, out candidate);
        }

        private bool TryBuildCandidate(
            PlayerManager player, Transform root, EnvironmentObject environmentObject, out Candidate candidate)
        {
            candidate = null;
            if (player == null || player.ec == null || root == null || IsAirborneRoot(root))
            {
                return false;
            }

            // The same prop stays the target for many frames; do not rebuild its renderer arrays
            // and re-run every component query each frame.
            Candidate cached = _aimCache;
            if (cached != null && cached.Root == root && cached.Root.gameObject.activeInHierarchy
                && Time.unscaledTime - _aimCacheTime < AimCandidateReuseTime)
            {
                candidate = cached;
                return true;
            }

            Candidate built;
            if (environmentObject != null)
            {
                Cell cell;
                if (!CanPickupEnvironmentObject(environmentObject, player, out cell))
                {
                    return false;
                }

                built = new Candidate
                {
                    Root = environmentObject.transform,
                    EnvironmentObject = environmentObject,
                    Renderers = environmentObject.GetComponentsInChildren<Renderer>(true),
                    Cell = cell,
                    ParentKind = ParentKind.RoomObjects
                };
            }
            else
            {
                if (IsForbiddenPropHierarchy(root))
                {
                    return false;
                }

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                Bounds bounds;
                bool hasBounds = CombinedBounds(renderers, out bounds);
                Cell propCell;
                bool hasCell = TryGetCell(player.ec, root.position, out propCell);
                if (!hasCell && (!hasBounds || !TryGetCell(player.ec, bounds.center, out propCell)))
                {
                    return false;
                }

                built = new Candidate
                {
                    Root = root,
                    EnvironmentObject = null,
                    Renderers = renderers,
                    Cell = propCell,
                    ParentKind = IsChildOfCellObjectBase(root, propCell)
                        ? ParentKind.CellObjectBase
                        : ParentKind.Room
                };
            }

            if (!CanBeginCarry(built, player))
            {
                return false;
            }

            candidate = built;
            _aimCache = built;
            _aimCacheTime = Time.unscaledTime;
            return true;
        }

        // ------------------------------------------------------------------------------------
        // Aim assist. A physics ray can miss a prop that is clearly under the crosshair: some props have
        // no solid collider at all, others have meshy colliders with gaps (an open chair back). Those are
        // matched by the oriented bounding box of each of their meshes (never the inflated world AABB),
        // only when they are nearer than the first solid hit, from a registry that is built a little per
        // frame instead of scanning every renderer in the scene on every query.
        // ------------------------------------------------------------------------------------

        private bool TryFindAssistProp(
            PlayerManager player, Ray ray, float maxDistance, bool physicalFound,
            out Candidate candidate, out float distance)
        {
            candidate = null;
            distance = float.MaxValue;
            if (maxDistance <= 0f || _proxies.Count == 0)
            {
                return false;
            }

            float pruneSqr = ProxyPruneDistance * ProxyPruneDistance;
            ProxyProp best = null;
            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                // Physics is authoritative for props that have colliders: when the ray already hit a
                // movable prop, only collider-less props (which physics cannot see) may still beat it.
                if (physicalFound && proxy.HasSolidCollider)
                {
                    continue;
                }

                // The far props are dropped on the position kept in the registry, before anything is asked of Unity.
                if ((proxy.Position - ray.origin).sqrMagnitude > pruneSqr)
                {
                    continue;
                }

                if (proxy.Root == null || !proxy.Root.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float proxyDistance = 0f;
                if (!RayHitsProxy(proxy, ray, out proxyDistance)
                    || proxyDistance > maxDistance || proxyDistance >= distance)
                {
                    continue;
                }

                best = proxy;
                distance = proxyDistance;
            }

            return best != null
                && TryBuildCandidate(player, best.Root, best.EnvironmentObject, out candidate);
        }

        private sealed class Candidate
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public Renderer[] Renderers;
            public Cell Cell;
            public ParentKind ParentKind;
            public float AimDistance;
            public string AimSource;
            // The surface normal at the crosshair (zero when the aim came from a mesh box, not a collider).
            public Vector3 AimNormal;
        }
    }
}
