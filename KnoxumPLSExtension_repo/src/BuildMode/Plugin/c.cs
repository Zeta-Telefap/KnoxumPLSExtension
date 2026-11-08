using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RebuildTheSchool
{
    /// <summary>
    /// BepInEx entry point for Rebuild the School!.
    /// BepInEx discovers this class through the BepInPlugin attribute; the class name is arbitrary.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class c : BaseUnityPlugin
    {
        public const string PluginGuid = "knoxum.baldiplus.rebuilders";
        public const string PluginName = "Rebuild the School!";
        public const string PluginVersion = "0.1.1";

        private Harmony _harmony;
        private GameObject _runtimeObject;

        private void Awake()
        {
            Logger.LogInfo($"Loading {PluginName} ({PluginGuid}) v{PluginVersion}...");

            try
            {
                _runtimeObject = new GameObject("RebuildTheSchool.Runtime");
                DontDestroyOnLoad(_runtimeObject);
                _runtimeObject.AddComponent<g>();
                _runtimeObject.AddComponent<g2>();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(c).Assembly);
                g.TryPatchGameInput(_harmony);

                Logger.LogInfo($"{PluginName} loaded. G selects from a narrow vertical strip at screen center; LMB places, RMB throws props only, wheel moves structures to walls, Shift+wheel strafes, R+wheel rotates.");
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
