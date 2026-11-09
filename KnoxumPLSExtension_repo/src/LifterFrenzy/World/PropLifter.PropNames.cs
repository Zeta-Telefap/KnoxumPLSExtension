using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Which names of the game's objects are props worth picking up, and which are not.
    /// </summary>
    public sealed partial class PropLifter
    {


        private static Transform FindSupportedPropRoot(Transform transform)
        {
            // Use the outermost recognized prop in the local hierarchy. Picking the nearest
            // named child made cabinet doors/plant branches appear as separate hover targets,
            // which could switch the glow and make G appear to miss the object.
            Transform current = transform;
            Transform propRoot = null;
            while (current != null)
            {
                if (current.GetComponent<EnvironmentObject>() != null
                    || current.GetComponent<TileBasedObject>() != null
                    || current.GetComponent<RoomController>() != null
                    || current.GetComponent<NPC>() != null
                    || current.GetComponent<Activity>() != null)
                {
                    break;
                }

                if (IsSupportedPropName(current.name))
                {
                    propRoot = current;
                }

                current = current.parent;
            }

            return propRoot;
        }

        // A name is looked at over and over (every renderer of every copy of a prop has one), so the answer is kept.
        private static readonly Dictionary<string, bool> SupportedNames = new Dictionary<string, bool>();

        private static bool IsSupportedPropName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            bool supported;
            if (SupportedNames.TryGetValue(name, out supported))
            {
                return supported;
            }

            supported = ComputeSupportedPropName(name);
            if (SupportedNames.Count >= 4096)
            {
                SupportedNames.Clear();
            }

            SupportedNames[name] = supported;
            return supported;
        }

        private static bool ComputeSupportedPropName(string name)
        {
            string value = name.ToLowerInvariant();
            if (value.Contains("bedroom") || value.Contains("classroom") || value.Contains("bathroom"))
            {
                return false;
            }

            return value.Contains("desk") || value.Contains("table") || value.Contains("chair")
                || value.Contains("locker") || value.Contains("cabinet") || value.Contains("vending")
                || value.Contains("soda") || value.Contains("zesty") || value.Contains("bed")
                || value.Contains("bench") || value.Contains("bookshelf") || value.Contains("bookcase")
                || value.Contains("shelf") || value.Contains("trash") || value.Contains("bin")
                || value.Contains("plant") || value.Contains("potted") || value.Contains("planter")
                || value.Contains("flower") || value.Contains("fern") || value.Contains("palm")
                || value.Contains("foliage") || value.Contains("sofa") || value.Contains("couch")
                || value.Contains("stool") || value.Contains("piano")
                || value.Contains("cart") || value.Contains("crate") || value.Contains("counter")
                || value.Contains("drawer") || value.Contains("filing");
        }

        private static bool IsForbiddenPropHierarchy(Transform root)
        {
            return root == null || IsForbiddenPickupRoot(root)
                || root.GetComponentInParent<EnvironmentObject>() != null
                || root.GetComponentInParent<Door>() != null
                || root.GetComponentInParent<TileBasedObject>() != null
                || root.GetComponentInParent<NPC>() != null
                || root.GetComponentInParent<Activity>() != null;
        }

        private static readonly string[] ForbiddenNameParts =
        {
            "elevator", "lift", "lamp", "light", "chandelier", "sconce",
            "bsoda", "no squee", "nosquee", "covercloud", "chalkcloud"
        };

        private static bool IsForbiddenPickupRoot(Transform root)
        {
            return ForbiddenPickupReason(root) != null;
        }

        /// <summary>Why a root must never be carried (elevators, items, clouds, real lights...), or null.</summary>
        private static string ForbiddenPickupReason(Transform root)
        {
            if (root == null)
            {
                return "missing object";
            }

            if (root.GetComponentInParent<Elevator>() != null
                || root.GetComponentInChildren<Elevator>(true) != null)
            {
                return "elevator";
            }

            if (root.GetComponentInParent<Item>() != null
                || root.GetComponentInChildren<Item>(true) != null)
            {
                return "item";
            }

            if (root.GetComponentInParent<CoverCloud>() != null
                || root.GetComponentInChildren<CoverCloud>(true) != null)
            {
                return "cover cloud";
            }

            if (HasForeignLight(root))
            {
                return "contains a light (lamp or fixture)";
            }

            string name = root.name.ToLowerInvariant();
            for (int i = 0; i < ForbiddenNameParts.Length; i++)
            {
                if (name.Contains(ForbiddenNameParts[i]))
                {
                    return "name contains '" + ForbiddenNameParts[i] + "'";
                }
            }

            return null;
        }

        /// <summary>
        /// A real light under the root marks a lamp or fixture. The glow this runtime attaches to the very
        /// prop it highlights or carries is also a Light; it must never make that prop "forbidden",
        /// otherwise a highlighted prop could not be picked up.
        /// </summary>
        private static bool HasForeignLight(Transform root)
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.gameObject.name != GlowObjectName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
