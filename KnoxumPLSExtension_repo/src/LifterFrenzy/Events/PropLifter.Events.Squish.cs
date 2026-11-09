using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Squishing props: the vanilla squishers, Balder and Dr Reflex.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Squish = Entity.Squish: vertical scale 0.5, wobbling during the last 3 s (see PropFx).
        private const float SquishTickInterval = 0.12f;
        private const float SquishNearMargin = 14f;
        private const float BalderSquishSeconds = 10f;
        private const float HammerSquishFallbackSeconds = 30f;
        private const float HammerReach = 5f;
        private const float HammerRadius = 4f;

        private static readonly FieldInfo ReflexSquishTimeField = typeof(DrReflex).GetField(
            "squishTime", BindingFlags.Instance | BindingFlags.NonPublic);

        private struct SquishTarget
        {
            public Transform Root;
            public Renderer[] Renderers;
        }
        private Balder_Entity[] _balders = new Balder_Entity[0];
        private DrReflex[] _reflexes = new DrReflex[0];
        private readonly List<Balder_Entity> _balderList = new List<Balder_Entity>();
        private readonly List<DrReflex> _reflexList = new List<DrReflex>();
        private readonly List<SquishTarget> _squishTargets = new List<SquishTarget>();

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
                        Debug.Log("[Lifter Frenzy] A Balder squished " + target.Root.name + " for "
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
                        Debug.Log("[Lifter Frenzy] Dr Reflex's hammer squished " + target.Root.name + ".");
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
                Debug.Log("[Lifter Frenzy] A thrown prop hit a Balder at " + flat.magnitude.ToString("0.0")
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

            Debug.Log("[Lifter Frenzy] A thrown prop hit Dr Reflex at " + flat.magnitude.ToString("0.0")
                + " u/s: he hammered " + state.Root.name + " at once and was not pushed"
                + (hasHammer ? "." : " (no hammer yet: only its sound)."));
            return true;
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
    }
}
