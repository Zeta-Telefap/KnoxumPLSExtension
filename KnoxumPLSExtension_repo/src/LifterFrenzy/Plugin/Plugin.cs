using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// BepInEx entry point for Lifter Frenzy!.
    /// BepInEx discovers this class through the BepInPlugin attribute; the class name is arbitrary.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "knx.LifterFrenzy";
        public const string PluginName = "Lifter Frenzy!";
        public const string PluginVersion = "1.0";

        private Harmony _harmony;
        private GameObject _runtimeObject;

        private void Awake()
        {
            Logger.LogInfo($"Loading {PluginName} ({PluginGuid}) v{PluginVersion}...");

            try
            {
                _runtimeObject = new GameObject("LifterFrenzy.Runtime");
                DontDestroyOnLoad(_runtimeObject);
                _runtimeObject.AddComponent<PropLifter>();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                PropLifter.TryPatchGameInput(_harmony);

                Logger.LogInfo($"{PluginName} loaded. G picks up the prop under the crosshair; LMB places it, RMB throws it, R + mouse wheel rotates it.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"Failed to initialize {PluginName}: {exception}");

                UnpatchHarmony();
                DestroyRuntimeObject();
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            UnpatchHarmony();
            DestroyRuntimeObject();
        }

        private void DestroyRuntimeObject()
        {
            if (_runtimeObject != null)
            {
                Destroy(_runtimeObject);
                _runtimeObject = null;
            }
        }

        private void UnpatchHarmony()
        {
            Harmony harmony = _harmony;
            _harmony = null;

            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
                Logger.LogInfo($"Harmony patches for {PluginName} were removed.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Failed to remove Harmony patches for {PluginName}: {exception}");
            }
        }
    }
}
