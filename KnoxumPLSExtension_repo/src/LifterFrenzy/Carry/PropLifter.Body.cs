using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Making a prop a rigidbody, ignoring the player's colliders while it is carried, and putting the body back the
    /// way it was.
    /// </summary>
    public sealed partial class PropLifter
    {


        private static void SaveBodySettings(CarryState state)
        {
            Rigidbody body = state.Body;
            state.OriginalMass = body.mass;
            state.OriginalDrag = body.drag;
            state.OriginalAngularDrag = body.angularDrag;
            state.OriginalIsKinematic = body.isKinematic;
            state.OriginalUseGravity = body.useGravity;
            state.OriginalDetectCollisions = body.detectCollisions;
            state.OriginalConstraints = body.constraints;
            state.OriginalCollisionMode = body.collisionDetectionMode;
            state.OriginalInterpolation = body.interpolation;
            state.OriginalVelocity = body.velocity;
            state.OriginalAngularVelocity = body.angularVelocity;
        }

        private static void IgnorePlayerCollisions(CarryState state)
        {
            if (state == null || state.Root == null || state.Player == null)
            {
                return;
            }

            Collider[] playerColliders = state.Player.GetComponentsInChildren<Collider>(true);
            Collider[] propColliders = state.Root.GetComponentsInChildren<Collider>(true);
            if (state.IgnoredCollisionPairs == null)
            {
                state.IgnoredCollisionPairs = new List<IgnoredCollisionPair>();
            }

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                {
                    continue;
                }

                for (int j = 0; j < propColliders.Length; j++)
                {
                    Collider propCollider = propColliders[j];
                    if (propCollider == null || !propCollider.enabled || propCollider.isTrigger)
                    {
                        continue;
                    }

                    try
                    {
                        Physics.IgnoreCollision(playerCollider, propCollider, true);
                        state.IgnoredCollisionPairs.Add(new IgnoredCollisionPair
                        {
                            PlayerCollider = playerCollider,
                            PropCollider = propCollider
                        });
                    }
                    catch
                    {
                        // Ignore pairs that Unity considers invalid; the prop still collides with the world.
                    }
                }
            }
        }

        private static void RestoreVisualGlow(CarryState state)
        {
            if (state != null && state.Glow != null)
            {
                state.Glow.Restore();
                state.Glow = null;
            }
        }

        private static void RestorePlayerCollisions(CarryState state)
        {
            if (state == null || state.IgnoredCollisionPairs == null)
            {
                return;
            }

            for (int i = state.IgnoredCollisionPairs.Count - 1; i >= 0; i--)
            {
                IgnoredCollisionPair pair = state.IgnoredCollisionPairs[i];
                if (pair != null && pair.PlayerCollider != null && pair.PropCollider != null)
                {
                    try
                    {
                        Physics.IgnoreCollision(pair.PlayerCollider, pair.PropCollider, false);
                    }
                    catch
                    {
                        // Either collider may have been destroyed while the prop was carried.
                    }
                }
            }

            state.IgnoredCollisionPairs.Clear();
        }

        private static void RestoreBody(CarryState state, bool restoreVelocity)
        {
            if (state == null)
            {
                return;
            }

            RestorePlayerCollisions(state);
            RestoreVisualGlow(state);
            if (state.Body != null && !state.Body.isKinematic)
            {
                state.Body.isKinematic = true;
            }

            RestoreNonConvex(state.NonConvexColliders);
            if (state.Body != null)
            {
                if (state.AddedBody)
                {
                    Destroy(state.Body);
                    state.Body = null;
                }
                else
                {
                    state.Body.mass = state.OriginalMass;
                    state.Body.drag = state.OriginalDrag;
                    state.Body.angularDrag = state.OriginalAngularDrag;
                    state.Body.useGravity = state.OriginalUseGravity;
                    state.Body.detectCollisions = state.OriginalDetectCollisions;
                    state.Body.constraints = state.OriginalConstraints;
                    state.Body.collisionDetectionMode = state.OriginalCollisionMode;
                    state.Body.interpolation = state.OriginalInterpolation;
                    state.Body.isKinematic = state.OriginalIsKinematic;
                    if (!state.Body.isKinematic)
                    {
                        state.Body.velocity = restoreVelocity ? state.OriginalVelocity : Vector3.zero;
                        state.Body.angularVelocity = restoreVelocity ? state.OriginalAngularVelocity : Vector3.zero;
                    }
                }
            }

            if (state.AddedCollider != null)
            {
                Destroy(state.AddedCollider);
                state.AddedCollider = null;
            }
        }

        private static void RestoreNonConvex(List<MeshCollider> colliders)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = true;
                }
            }
        }

        private sealed class IgnoredCollisionPair
        {
            public Collider PlayerCollider;
            public Collider PropCollider;
        }
    }
}
