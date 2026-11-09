using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Where the player's input becomes the mod's actions: G, LMB, RMB, R + wheel, the wheel-click kick, and the
    /// PlayerClick patch that hides the game's own click while a prop is held.
    /// </summary>
    public sealed partial class PropLifter
    {


        internal static void BlockGameMouseBriefly()
        {
            _gameMouseBlockedUntil = Time.unscaledTime + GameMouseBlockAfterRelease;
        }

        /// <summary>
        /// Stops the game from using an item (RMB) or switching items (wheel) while the mouse is blocked. The
        /// plugin calls it once after PatchAll; a method that cannot be patched only leaves that input unblocked.
        /// </summary>
        internal static void TryPatchGameInput(Harmony harmony)
        {
            string[] methods = { "UseItem", "IncreaseItemSelection", "DecreaseItemSelection" };
            for (int i = 0; i < methods.Length; i++)
            {
                try
                {
                    MethodInfo target = AccessTools.Method(typeof(ItemManager), methods[i]);
                    MethodInfo skip = typeof(PropLifter).GetMethod(
                        nameof(SkipItemInputWhileBlocked), BindingFlags.Static | BindingFlags.NonPublic);
                    if (target == null || skip == null)
                    {
                        Debug.LogWarning("[Lifter Frenzy] ItemManager." + methods[i]
                            + " was not found; the game keeps reading that input while a prop is carried.");
                        continue;
                    }

                    harmony.Patch(target, prefix: new HarmonyMethod(skip));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Lifter Frenzy] ItemManager." + methods[i]
                        + " could not be blocked while a prop is carried: " + exception.Message);
                }
            }
        }

        private static bool SkipItemInputWhileBlocked()
        {
            return !GameMouseBlocked;
        }

        private static bool PickupPressed()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                return true;
            }

            // Input.inputString also covers the physical G key on non-Latin keyboard layouts.
            string input = Input.inputString;
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            input = input.ToLowerInvariant();
            return input.Contains("g") || input.Contains("п");
        }

        private static bool PlacePressed()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

            try
            {
                InputManager inputManager = Singleton<InputManager>.Instance;
                return inputManager != null
                    && (inputManager.GetDigitalInput("Interact", true)
                        || inputManager.GetDigitalInput("MouseSubmit", true));
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerClick), "Update")]
        private static class PlayerClickUpdatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerClick __instance)
            {
                PropLifter runtime = Instance;
                if (runtime == null)
                {
                    return true;
                }

                try
                {
                    if (runtime.HandlePlayerClick(__instance))
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Lifter Frenzy] PlayerClick input hook failed: " + exception);
                    if (runtime._held != null)
                    {
                        return false;
                    }
                }

                // A prop was just thrown or placed: the click that did it must not also click on something the
                // game can see.
                if (Time.unscaledTime < _gameMouseBlockedUntil)
                {
                    __instance.clickedThisFrame = null;
                    return false;
                }

                return true;
            }
        }
    }
}
