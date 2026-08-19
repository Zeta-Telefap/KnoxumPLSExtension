using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.OptionsAPI;
using MTM101BaldAPI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace KnoxumsChaosMode
{


    [HarmonyPatch]
    public static class BaldiRampagePatches
    {
        public static bool TimeOutTriggered;

        [HarmonyPatch(typeof(Baldi), "TakeApple")]
        [HarmonyPrefix]
        static bool Pre_TakeApple(Baldi __instance)
        {
            if (!On || __instance == null) return true;

            if (BaldiRampage.IsHitByThrownApple)
            {
                R.Set(__instance, "appleTime", 3f);
                BaldiRampageController ctl = Ctl(__instance);
                if (ctl != null) ctl.Stun(3f);
                return true;
            }

            try
            {
                PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (pm != null)
                    __instance.CaughtPlayer(pm);
            }
            catch { }
            return false;
        }

        internal static void PlayAppleThanks(Baldi baldi)
        {
            if (baldi == null) return;
            try
            {
                AudioManager aud = R.Get<AudioManager>(baldi, "audMan", null);
                if (aud == null) aud = baldi.GetComponent<AudioManager>();
                if (aud == null) aud = baldi.GetComponentInChildren<AudioManager>();
                SoundObject so = R.Get<SoundObject>(baldi, "audApple", null);
                if (so == null) so = R.Get<SoundObject>(baldi, "applePraise", null);
                if (so == null) so = FindAppleThanksSound();
                if (aud != null && so != null)
                    aud.PlaySingle(so);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogWarning("Apple thanks: " + ex.Message);
            }
        }

        private static SoundObject FindAppleThanksSound()
        {
            SoundObject[] all = Resources.FindObjectsOfTypeAll<SoundObject>();
            SoundObject fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                SoundObject s = all[i];
                if (s == null) continue;
                string n = s.name.ToLowerInvariant();
                if (n.Contains("thank") && n.Contains("apple")) return s;
                if (n.Contains("apple") && (n.Contains("for") || n.Contains("praise") || n.Contains("thanks")))
                    return s;
                if (fallback == null && n.Contains("apple") && (n.Contains("vfx") || n.Contains("baldi")))
                    fallback = s;
            }
            return fallback;
        }

        internal static void SilenceGrappleAfterCatch()
        {
            try
            {
                ITM_GrapplingHook[] hooks = UnityEngine.Object.FindObjectsOfType<ITM_GrapplingHook>(true);
                for (int i = 0; i < hooks.Length; i++)
                {
                    ITM_GrapplingHook h = hooks[i];
                    if (h == null) continue;
                    AudioSource motor = R.Get<AudioSource>(h, "motorAudio", null);
                    if (motor != null)
                    {
                        motor.Stop();
                        motor.mute = true;
                    }
                    AudioManager hookAud = R.Get<AudioManager>(h, "audMan", null);
                    if (hookAud != null) hookAud.FlushQueue(true);
                    try
                    {
                        MethodInfo end = h.GetType().GetMethod(
                            "End", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (end != null && end.GetParameters().Length == 0)
                            end.Invoke(h, null);
                    }
                    catch { }
                    try { UnityEngine.Object.Destroy(h.gameObject); } catch { }
                }
            }
            catch { }
            try
            {
                BaldiGrappleRuntime[] runs = UnityEngine.Object.FindObjectsOfType<BaldiGrappleRuntime>(true);
                for (int i = 0; i < runs.Length; i++)
                    if (runs[i] != null) UnityEngine.Object.Destroy(runs[i].gameObject);
            }
            catch { }
            try
            {
                AudioSource[] srcs = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
                for (int i = 0; i < srcs.Length; i++)
                {
                    AudioSource a = srcs[i];
                    if (a == null || !a.isPlaying) continue;
                    if (a.GetComponentInParent<MusicManager>() != null) continue;
                    string n = (a.gameObject.name + " " + (a.clip != null ? a.clip.name : "")).ToLowerInvariant();
                    if (n.Contains("grappl") || n.Contains("hook") || n.Contains("clang")
                        || (n.Contains("motor") && n.Contains("grap")))
                        a.Stop();
                }
            }
            catch { }
        }

        private static bool ReadIntMember(object obj, string name, out int value, out Action<int> setter)
        {
            value = 0;
            setter = null;
            if (obj == null) return false;
            Type t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(byte)))
                {
                    value = Convert.ToInt32(f.GetValue(obj));
                    setter = v => f.SetValue(obj, Convert.ChangeType(v, f.FieldType));
                    return true;
                }
                PropertyInfo pr = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pr != null && pr.CanRead && (pr.PropertyType == typeof(int) || pr.PropertyType == typeof(byte)))
                {
                    value = Convert.ToInt32(pr.GetValue(obj, null));
                    if (pr.CanWrite)
                        setter = v => pr.SetValue(obj, Convert.ChangeType(v, pr.PropertyType), null);
                    return true;
                }
                t = t.BaseType;
            }
            return false;
        }

        private static bool TakeOneLifeSafe(CoreGameManager cgm)
        {
            if (cgm == null) return false;
            try
            {
                int lives = 0;
                Action<int> setLives = null;
                string[] lifeNames = { "lives", "Lives", "currentLives" };
                bool found = false;
                for (int i = 0; i < lifeNames.Length; i++)
                {
                    if (ReadIntMember(cgm, lifeNames[i], out lives, out setLives) && setLives != null)
                    {
                        found = true;
                        break;
                    }
                }
                int extra = 0;
                Action<int> setExtra = null;
                bool hasExtra = ReadIntMember(cgm, "extraLives", out extra, out setExtra);

                if (!found && !hasExtra)
                {
                    KnoxumsChaosModePlugin.Log.LogWarning("TakeOneLifeSafe: lives not found");
                    return false;
                }

                if (lives <= 0 && extra <= 0)
                {
                    ChaosManager.Instance?.ResetLapsToDefault();
                    cgm.ReturnToMenu();
                    return true;
                }

                if (extra > 0 && setExtra != null)
                    setExtra(extra - 1);
                else if (setLives != null)
                    setLives(lives - 1);

                int att;
                Action<int> setAtt;
                if (ReadIntMember(cgm, "attempts", out att, out setAtt) && setAtt != null)
                    setAtt(att + 1);

                try
                {
                    HudManager hud = cgm.GetHud(0);
                    if (hud != null) hud.ReInit();
                }
                catch { }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("TakeOneLifeSafe: " + ex);
            }
            return false;
        }

        [HarmonyPatch(typeof(LevelBuilder), "CreateRandomItem", new Type[]
        {
            typeof(RoomController),
            typeof(List<WeightedItemObject>),
            typeof(Vector2),
            typeof(System.Random)
        })]
        [HarmonyPrefix]
        static void Pre_CreateRandomItem(ref List<WeightedItemObject> potentialItems)
        {
            if (!On || potentialItems == null || potentialItems.Count == 0) return;
            try
            {
                potentialItems = CloneAndReweightItemList(potentialItems);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage CreateRandomItem: " + ex);
            }
        }

        private static readonly Dictionary<ItemObject, int> weightMultiplierCache =
            new Dictionary<ItemObject, int>();

        private static int GetCachedMultiplier(ItemObject item)
        {
            if (item == null) return 1;
            if (!weightMultiplierCache.TryGetValue(item, out int mult))
            {
                string n = item.name != null ? item.name.ToLower() : "";
                string t = item.itemType.ToString().ToLower();
                if (t.Contains("apple") || n.Contains("apple") || t.Contains("banana") || n.Contains("banana")
                    || n.Contains("nana") || n.Contains("peel") || n.Contains("slip"))
                    mult = 3;
                else if (t.Contains("tape") || n.Contains("tape") || (n.Contains("baldi") && n.Contains("least")))
                    mult = 0;
                else
                    mult = 1;
                weightMultiplierCache[item] = mult;
            }
            return mult;
        }

        [HarmonyPatch(typeof(LevelBuilder), "CreateItem", new Type[]
        {
            typeof(RoomController),
            typeof(ItemObject),
            typeof(Vector2),
            typeof(bool),
            typeof(bool)
        })]
        [HarmonyPrefix]
        static void Pre_CreateItem(ref ItemObject item)
        {
            if (!On || item == null) return;
            try
            {
                int mult = GetCachedMultiplier(item);
                if (mult == 0)
                {
                    if (UnityEngine.Random.value < 0.70f)
                    {
                        ItemObject repl = FindAppleOrBanana();
                        if (repl != null) item = repl;
                    }
                    return;
                }

                if (mult != 3 && UnityEngine.Random.value < 0.12f)
                {
                    ItemObject repl = FindAppleOrBanana();
                    if (repl != null) item = repl;
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage CreateItem: " + ex);
            }
        }

        private static List<WeightedItemObject> CloneAndReweightItemList(List<WeightedItemObject> src)
        {
            List<WeightedItemObject> dst = new List<WeightedItemObject>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                WeightedItemObject w = src[i];
                if (w == null || w.selection == null) continue;
                WeightedItemObject nw = new WeightedItemObject();
                nw.selection = w.selection;
                nw.weight = GetAdjustedWeight(w.selection, w.weight);
                dst.Add(nw);
            }
            return dst;
        }

        private static int GetAdjustedWeight(ItemObject item, int baseWeight)
        {
            const int MaxSafeWeight = 1000000;

            if (baseWeight <= 0) return 0;
            int w = Mathf.Clamp(baseWeight, 1, MaxSafeWeight);
            int mult = GetCachedMultiplier(item);
            if (mult == 3) return Mathf.Clamp((int)Math.Min((long)w * 3L, MaxSafeWeight), 1, MaxSafeWeight);
            if (mult == 0) return Mathf.Clamp(Mathf.RoundToInt(w * 0.30f), 1, MaxSafeWeight);
            return w;
        }

        private static bool IsAppleItem(string n, string t)
        {
            return t.Contains("apple") || n.Contains("apple");
        }

        private static bool IsBananaItem(string n, string t)
        {
            return t.Contains("banana") || n.Contains("banana") || n.Contains("nana")
                || n.Contains("peel") || n.Contains("slip");
        }

        private static ItemObject cachedAppleItem;
        private static ItemObject cachedBananaItem;
        private static bool itemsSearched;

        private static ItemObject FindAppleOrBanana()
        {

            if (!itemsSearched || (cachedAppleItem == null && cachedBananaItem == null))
            {
                itemsSearched = true;
                foreach (ItemObject io in Resources.FindObjectsOfTypeAll<ItemObject>())
                {
                    if (io == null || io.itemType == Items.None
                        || (io.itemSpriteLarge == null && io.itemSpriteSmall == null)) continue;
                    string n = io.name.ToLower();
                    string t = io.itemType.ToString().ToLower();
                    if (cachedAppleItem == null && IsAppleItem(n, t)) cachedAppleItem = io;
                    if (cachedBananaItem == null && IsBananaItem(n, t)) cachedBananaItem = io;
                }
            }
            if (cachedAppleItem != null && cachedBananaItem != null)
                return UnityEngine.Random.value < 0.5f ? cachedAppleItem : cachedBananaItem;
            return cachedAppleItem ?? cachedBananaItem;
        }

        private static bool On => BaldiRampageConfig.IsActive;

        public static BaldiRampageController Ctl(NPC npc)
        {


            if (npc == null || !(npc is Baldi)) return null;
            BaldiRampageController c = npc.GetComponent<BaldiRampageController>();
            if (c == null && On)
                c = npc.gameObject.AddComponent<BaldiRampageController>();
            return c;
        }

        public static bool IsAnyTapePlaying()
        {
            try
            {
                foreach (TapePlayer tp in UnityEngine.Object.FindObjectsOfType<TapePlayer>())
                {
                    if (tp == null) continue;
                    FieldInfo pf = R.Field(tp, "active");
                    if (pf != null && pf.FieldType == typeof(bool) && (bool)pf.GetValue(tp))
                        return true;
                    AudioSource aus = tp.GetComponent<AudioSource>() ?? tp.GetComponentInChildren<AudioSource>();
                    if (aus != null && aus.isPlaying) return true;
                }
            }
            catch { }
            return false;
        }

        [HarmonyPatch(typeof(TapePlayer), "InsertItem")]
        [HarmonyPostfix]
        static void Post_TapeInsert(TapePlayer __instance)
        {
            if (!On) return;
            try
            {
                foreach (Baldi b in UnityEngine.Object.FindObjectsOfType<Baldi>())
                {
                    BaldiRampageController ctl = Ctl(b);
                    if (ctl != null)
                    {
                        ctl.SetTapePlaying(true);
                        ctl.EnforceSpeedAfterSlap();
                    }
                }
                if (ChaosManager.Instance != null)
                    ChaosManager.Instance.StartCoroutine(WatchTapeEnd(__instance));
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage TapeInsert: " + ex);
            }
        }

        private static IEnumerator WatchTapeEnd(TapePlayer player)
        {
            if (player == null) yield break;
            yield return null;
            yield return null;

            while (player != null)
            {
                bool isActive = false;
                try
                {
                    FieldInfo pf = R.Field(player, "active");
                    if (pf != null && pf.FieldType == typeof(bool))
                        isActive = (bool)pf.GetValue(player);
                    else
                    {
                        AudioSource aus = player.GetComponent<AudioSource>() ?? player.GetComponentInChildren<AudioSource>();
                        if (aus != null) isActive = aus.isPlaying;
                    }
                }
                catch { }
                if (!isActive) break;
                yield return null;
            }

            try
            {
                foreach (Baldi b in UnityEngine.Object.FindObjectsOfType<Baldi>())
                {
                    if (b == null) continue;
                    BaldiRampageController ctl = b.GetComponent<BaldiRampageController>();
                    if (ctl != null)
                    {
                        ctl.SetTapePlaying(false);
                        ctl.EnforceSpeedAfterSlap();
                    }
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(Baldi), "Slap")]
        [HarmonyPostfix]
        static void Post_BaldiSlap(Baldi __instance)
        {
            if (!On || __instance == null) return;
            try
            {
                BaldiRampageController ctl = Ctl(__instance);
                if (ctl != null) ctl.EnforceSpeedAfterSlap();
            }
            catch { }
        }

        [HarmonyPatch(typeof(Baldi), "UpdateSlapDistance")]
        [HarmonyPostfix]
        static void Post_UpdateSlapDistance(Baldi __instance)
        {
            if (!On || __instance == null) return;
            try
            {
                BaldiRampageController ctl = Ctl(__instance);
                if (ctl != null) ctl.EnforceSpeedAfterSlap();
            }
            catch { }
        }

        [HarmonyPatch(typeof(NPC), "Initialize")]
        [HarmonyPostfix]
        static void Post_NPC_Init(NPC __instance)
        {
            if (!On) return;
            try
            {
                if (__instance is Baldi)
                    Ctl(__instance);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage NPC.Initialize: " + ex);
            }
        }

        [HarmonyPatch(typeof(BaseGameManager), "BeginSpoopMode")]
        [HarmonyPostfix]
        static void Post_BeginSpoopMode(BaseGameManager __instance)
        {
            if (!On) return;
            try
            {
                if (__instance == null || __instance.Ec == null) return;
                Baldi b = __instance.Ec.GetBaldi();
                if (b == null)
                {
                    foreach (Baldi bb in UnityEngine.Object.FindObjectsOfType<Baldi>())
                    {
                        b = bb;
                        break;
                    }
                }
                if (b != null)
                {
                    BaldiRampageController ctl = Ctl(b);
                    if (ctl != null) ctl.TeleportFarOnce();
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage BeginSpoopMode: " + ex);
            }
        }

        [HarmonyPatch(typeof(BaseGameManager), "AllNotebooks")]
        [HarmonyPrefix]
        static bool Pre_AllNotebooks(BaseGameManager __instance)
        {


            ChaosManager cm = ChaosManager.Instance;
            if (cm != null && cm.IsLapsActive) return true;
            return !On;
        }

        [HarmonyPatch(typeof(BaseGameManager), "AllNotebooks")]
        [HarmonyPostfix]
        static void Post_AllNotebooks(BaseGameManager __instance)
        {
            if (On || (ChaosManager.Instance != null &&
                (ChaosManager.Instance.IsLapTransitionInProgress || ChaosManager.skipElevatorOnLap))) return;
            try { ElevatorUnlockService.OnAllNotebooks(__instance, "BaseGameManager.AllNotebooks"); }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("AllNotebooks elevator unlock: " + ex); }
        }

        [HarmonyPatch(typeof(MainGameManager), "AllNotebooks")]
        [HarmonyPrefix]
        static bool Pre_MainAllNotebooks(MainGameManager __instance)
        {
            ChaosManager cm = ChaosManager.Instance;
            if (cm != null && cm.IsLapsActive) return true;
            return !On;
        }

        [HarmonyPatch(typeof(MainGameManager), "AllNotebooks")]
        [HarmonyPostfix]
        static void Post_MainAllNotebooks(MainGameManager __instance)
        {
            if (On || (ChaosManager.Instance != null &&
                (ChaosManager.Instance.IsLapTransitionInProgress || ChaosManager.skipElevatorOnLap))) return;
            try { ElevatorUnlockService.OnAllNotebooks(__instance, "MainGameManager.AllNotebooks"); }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Main AllNotebooks elevator unlock: " + ex); }
        }

        [HarmonyPatch(typeof(Baldi), "CaughtPlayer")]
        [HarmonyPrefix]
        static bool Pre_CaughtPlayer(Baldi __instance, PlayerManager player)
        {
            if (!On) return true;
            BaldiRampageConfig.CatchingPlayer = true;
            return true;
        }

        internal static bool IsTimeOutRunning()
        {
            if (TimeOutTriggered) return true;
            try
            {
                TimeOut[] evs = UnityEngine.Object.FindObjectsOfType<TimeOut>(true);
                for (int i = 0; i < evs.Length; i++)
                {
                    TimeOut ev = evs[i];
                    if (ev == null) continue;
                    bool active = false;
                    try { active = ev.Active; } catch { }
                    if (active) return true;
                }
            }
            catch { }
            return false;
        }


        internal static bool Pre_EndGame(CoreGameManager __instance)
        {
            if (!On) return true;
            if (IsTimeOutRunning())
            {
                TimeOutTriggered = true;
                timeoutWentThroughEndGame = true;
                return true;
            }
            SnapshotLives(__instance);
            return true;
        }

        internal static void Post_EndGame(CoreGameManager __instance)
        {
            if (!On) return;
            try
            {
                if (!IsTimeOutRunning()) RestoreLivesIfDropped(__instance);
            }
            catch { }
        }

        [HarmonyPatch(typeof(CoreGameManager), "EndSequence")]
        [HarmonyPrefix]
        static bool Pre_EndSequence(CoreGameManager __instance, ref IEnumerator __result)
        {
            if (!On) return true;
            if (IsTimeOutRunning())
            {
                TimeOutTriggered = true;
                return true;
            }
            __result = WaitBlackScreenThenNextFloor(__instance);
            return false;
        }

        private static IEnumerator WaitBlackScreenThenNextFloor(CoreGameManager cm)
        {
            try { SilenceGrappleAfterCatch(); } catch { }
            float glitchRate = 1f;
            try { Shader.SetGlobalColor("_SkyboxColor", Color.black); } catch { }
            while (glitchRate > 0f)
            {
                glitchRate -= Time.unscaledDeltaTime;
                float safeRate = Mathf.Max(0.001f, glitchRate);
                try
                {
                    GameCamera cam = cm.GetCamera(0);
                    if (cam != null)
                    {
                        if (cam.camCom != null) cam.camCom.farClipPlane = Mathf.Max(0.05f, 500f * safeRate);
                        if (cam.billboardCam != null) cam.billboardCam.farClipPlane = Mathf.Max(0.05f, 500f * safeRate);
                    }
                }
                catch { }
                yield return null;
            }
            try
            {
                GameCamera cam = cm.GetCamera(0);
                if (cam != null)
                {
                    if (cam.camCom != null) cam.camCom.farClipPlane = 1000f;
                    if (cam.billboardCam != null) cam.billboardCam.farClipPlane = 1000f;
                    cam.StopRendering(true);
                }
            }
            catch { }
            try { cm.audMan.FlushQueue(true); } catch { }
            try { AudioListener.pause = true; } catch { }

            glitchRate = 2f;
            while (glitchRate > 0f)
            {
                glitchRate -= Time.unscaledDeltaTime;
                yield return null;
            }

            try { RestoreLivesIfDropped(cm); } catch { }
            try
            {
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                if (ChaosManager.Instance != null && bgm != null)
                    ChaosManager.Instance.StartCowardCaughtRestart(bgm);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Coward after black screen: " + ex);
            }
        }

        private static bool timeoutWentThroughEndGame;
        private static bool timeOutSeqStarted;

        internal static void ResetCowardRoundFlags()
        {
            TimeOutTriggered = false;
            timeoutWentThroughEndGame = false;
            timeOutSeqStarted = false;
            BaldiRampageConfig.CatchingPlayer = false;
        }

        private static int snapLives = int.MinValue;
        private static int snapExtra = int.MinValue;
        private static Action<int> snapSetLives;
        private static Action<int> snapSetExtra;

        private static void SnapshotLives(CoreGameManager cgm)
        {
            snapLives = int.MinValue;
            snapExtra = int.MinValue;
            snapSetLives = null;
            snapSetExtra = null;
            if (cgm == null) return;
            string[] names = { "lives", "Lives", "currentLives" };
            for (int i = 0; i < names.Length; i++)
            {
                if (ReadIntMember(cgm, names[i], out snapLives, out snapSetLives) && snapSetLives != null)
                    break;
            }
            ReadIntMember(cgm, "extraLives", out snapExtra, out snapSetExtra);
        }

        private static void RestoreLivesIfDropped(CoreGameManager cgm)
        {
            if (cgm == null || snapLives == int.MinValue) return;
            int now = snapLives;
            Action<int> unused;
            if (snapSetLives != null)
            {
                string[] names = { "lives", "Lives", "currentLives" };
                for (int i = 0; i < names.Length; i++)
                    if (ReadIntMember(cgm, names[i], out now, out unused)) break;
                if (now < snapLives) snapSetLives(snapLives);
            }
            if (snapSetExtra != null)
            {
                int ex;
                if (ReadIntMember(cgm, "extraLives", out ex, out unused) && ex < snapExtra)
                    snapSetExtra(snapExtra);
            }
            try
            {
                HudManager hud = cgm.GetHud(0);
                if (hud != null) hud.ReInit();
            }
            catch { }
        }

        internal static void RestoreCowardCamera(CoreGameManager cm, bool restoreFarClip = true)
        {
            try
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                PropagatedAudioManager.paused = false;
            }
            catch { }
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            catch { }
            if (cm == null) return;

            PlayerManager pm = null;
            try { pm = cm.GetPlayer(0); } catch { }
            try
            {
                GameCamera cam = cm.GetCamera(0);
                if (cam != null)
                {
                    try { ((Behaviour)cam).enabled = true; } catch { }
                    if (cam.camCom != null)
                    {
                        cam.camCom.enabled = true;
                        if (restoreFarClip) cam.camCom.farClipPlane = 1000f;
                    }
                    if (cam.billboardCam != null)
                    {
                        cam.billboardCam.enabled = true;
                        if (restoreFarClip) cam.billboardCam.farClipPlane = 1000f;
                    }
                    cam.StopRendering(false);
                    SetPossibleBoolFields(cam, false, "locked", "cameraLocked", "lookLocked", "reverse",
                        "stopRendering", "stopped", "disabled", "freeze", "frozen");
                    SetPossibleBoolFields(cam, true, "controllable", "control", "follow", "updatePlayers", "active");
                    if (restoreFarClip && pm != null) SnapCameraToPlayer(cam, pm);
                }
            }
            catch { }
        }

        internal static void SnapCameraToPlayer(object cam, PlayerManager pm)
        {
            if (cam == null || pm == null) return;
            try
            {
                Quaternion rot = pm.transform.rotation;
                Vector3 pos = pm.transform.position;
                Component camComp = cam as Component;
                if (camComp != null)
                {
                    Transform camT = camComp.transform;
                    Transform parent = camT.parent;
                    if (parent != null && !camT.IsChildOf(pm.transform))
                    {
                        string pn = parent.name.ToLowerInvariant();
                        if (pn.Contains("baldi") || pn.Contains("npc") || pn.Contains("caught"))
                            camT.SetParent(null, true);
                    }
                    camT.position = pos;
                    camT.rotation = rot;
                }
                R.Set(cam, "cameraRotation", rot);
                R.Set(cam, "rotation", rot);
                R.Set(cam, "targetRotation", rot);
                R.Set(cam, "rotationX", 0f);
                R.Set(cam, "rotationY", rot.eulerAngles.y);
                R.Set(cam, "lookRotation", rot);
                R.Set(cam, "lookTarget", null);
                R.Set(cam, "target", null);
                R.Set(cam, "lookAt", null);
                R.Set(cam, "matchTransform", null);
                SetPossibleBoolFields(cam, false, "looking", "lookAtBaldi", "cinematic", "ending", "caught", "inEndSequence");
            }
            catch { }

            try
            {
                MethodInfo[] methods = cam.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo mi = methods[i];
                    if (mi == null || mi.IsSpecialName) continue;
                    string n = mi.Name;
                    ParameterInfo[] ps = mi.GetParameters();
                    if (n == "UpdateTargets" && ps.Length == 0) mi.Invoke(cam, null);
                    else if (n == "UpdateTargets" && ps.Length == 1 && ps[0].ParameterType == typeof(float))
                        mi.Invoke(cam, new object[] { 0f });
                    else if (n == "UpdateTargets" && ps.Length == 2
                             && ps[0].ParameterType == typeof(float) && ps[1].ParameterType == typeof(int))
                        mi.Invoke(cam, new object[] { 0f, 0 });
                    else if ((n == "SnapToTarget" || n == "UpdateForwardTargets") && ps.Length == 0)
                        mi.Invoke(cam, null);
                    else if ((n == "SetControllable" || n == "SetControl" || n == "SetLocked")
                             && ps.Length == 1 && ps[0].ParameterType == typeof(bool))
                        mi.Invoke(cam, new object[] { n != "SetLocked" });
                }
            }
            catch { }
        }

        internal static void SetPossibleBoolFields(object obj, bool value, params string[] names)
        {
            if (obj == null) return;
            Type t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                foreach (string n in names)
                {
                    try
                    {
                        FieldInfo f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(bool)) f.SetValue(obj, value);
                    }
                    catch { }
                }
                t = t.BaseType;
            }
        }

        [HarmonyPatch(typeof(Baldi), "Hear")]
        [HarmonyPrefix]
        static bool Pre_Hear(Baldi __instance, Vector3 position)
        {
            if (!On || __instance == null) return true;
            try
            {
                BaldiRampageController ctl = Ctl(__instance);
                if (ctl != null) ctl.OnNoise(position);
                return false;
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage Hear: " + ex);
                return true;
            }
        }

        [HarmonyPatch(typeof(ITM_BSODA), "EntityTriggerEnter")]
        [HarmonyPrefix]
        static bool Pre_BsodaHit(ITM_BSODA __instance, Collider other)
        {
            if (!On) return true;
            try
            {
                if (other != null && other.GetComponentInParent<NPC>() is Baldi) return false;
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(BaseGameManager), "CollectNotebooks")]
        [HarmonyPostfix]
        static void Post_Collect(BaseGameManager __instance)
        {
            if (__instance != null && !(__instance is EndlessGameManager))
            {
                try
                {
                    int found = __instance.FoundNotebooks;
                    int total = __instance.NotebookTotal;
                    if (total > 0 && found >= total && !ElevatorUnlockService.ElevatorsUnlockedThisFloor && !On)
                    {

                        ElevatorUnlockService.OnAllNotebooks(__instance, "CollectNotebooks fallback");
                    }
                }
                catch { }
            }
            if (!On) return;
            try
            {
                foreach (NPC n in UnityEngine.Object.FindObjectsOfType<NPC>())
                {
                    if (!(n is Baldi)) continue;
                    BaldiRampageController ctl = Ctl(n);

                    if (ctl != null) ctl.SetNotebooks(__instance.FoundNotebooks);
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage CollectNotebooks: " + ex);
            }
        }

        [HarmonyPatch(typeof(TimeOut), "Begin")]
        [HarmonyPostfix]
        static void Post_TimeOutBegin(TimeOut __instance)
        {
            if (!On || __instance == null) return;
            bool active = false;
            try { active = __instance.Active; } catch { }
            if (active) MarkTimeOutStarted("TimeOut.Begin");
        }

        [HarmonyPatch(typeof(RandomEvent), "Begin")]
        [HarmonyPostfix]
        static void Post_RandomEventBegin_TimeOut(RandomEvent __instance)
        {
            if (!On || !(__instance is TimeOut)) return;
            bool active = false;
            try { active = __instance.Active; } catch { }
            if (active) MarkTimeOutStarted("RandomEvent.Begin/TimeOut");
        }

        private static void MarkTimeOutStarted(string reason)
        {
            if (!On) return;
            TimeOutTriggered = true;
            if (timeOutSeqStarted) return;
            timeOutSeqStarted = true;
            timeoutWentThroughEndGame = false;
            KnoxumsChaosModePlugin.Log.LogInfo("Baldi-coward timeout: " + reason);
            try
            {
                if (ChaosManager.Instance != null)
                    ChaosManager.Instance.StartCoroutine(TimeOutLoseLifeAndRestartSequence());
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage TimeOut: " + ex);
            }
        }

        private static bool MidiIsPlaying()
        {
            try
            {
                MusicManager mm = Singleton<MusicManager>.Instance;
                if (mm == null) return false;
                FieldInfo mf = mm.GetType().GetField("midiPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mf == null) return false;
                object mp = mf.GetValue(mm);
                if (mp == null) return false;
                PropertyInfo isPlaying = mp.GetType().GetProperty("MPTK_IsPlaying", BindingFlags.Public | BindingFlags.Instance);
                return isPlaying != null && (bool)isPlaying.GetValue(mp);
            }
            catch { return false; }
        }

        private static IEnumerator TimeOutLoseLifeAndRestartSequence()
        {
            float elapsed = 0f;
            const float minWait = 4f;
            const float maxWait = 20f;
            while (elapsed < maxWait)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= minWait && !MidiIsPlaying()) break;
                if (timeoutWentThroughEndGame) yield break;
                yield return null;
            }
            if (!TimeOutTriggered || timeoutWentThroughEndGame) yield break;
            try
            {
                CoreGameManager cgm = Singleton<CoreGameManager>.Instance;
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                if (cgm != null && !timeoutWentThroughEndGame)
                {
                    timeoutWentThroughEndGame = true;
                    if (TakeOneLifeSafe(cgm))
                    {
                        ResetCowardRoundFlags();
                        yield break;
                    }
                }
                if (bgm != null) bgm.RestartLevel();
                ResetCowardRoundFlags();
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("TimeOutLoseLifeAndRestartSequence: " + ex);
            }
        }

        [HarmonyPatch(typeof(ItemManager), "UseItem")]
        [HarmonyPrefix]
        static bool Pre_UseItem(ItemManager __instance)
        {
            if (!On) return true;
            try
            {
                if (__instance == null || __instance.items == null || __instance.pm == null) return true;
                if (__instance.selectedItem < 0 || __instance.selectedItem >= __instance.items.Length) return true;
                ItemObject sel = __instance.items[__instance.selectedItem];
                if (sel != null && sel.itemType == Items.Apple)
                {
                    if (AppleChargeHandler.ActiveCharge != null) return false;
                    GameObject chargeObj = new GameObject("AppleChargeHandler");
                    AppleChargeHandler handler = chargeObj.AddComponent<AppleChargeHandler>();
                    handler.Initialize(__instance.pm, __instance, __instance.selectedItem);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("BaldiRampage ItemManager.UseItem: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch]
    public static class CowardEndGameAllPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] ms = typeof(CoreGameManager).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            HashSet<MethodBase> seen = new HashSet<MethodBase>();
            for (int i = 0; i < ms.Length; i++)
            {
                MethodInfo m = ms[i];
                if (m != null && m.Name == "EndGame" && !m.IsAbstract && !m.ContainsGenericParameters && seen.Add(m))
                    yield return m;
            }
        }

        static bool Prefix(CoreGameManager __instance)
        {
            return BaldiRampagePatches.Pre_EndGame(__instance);
        }

        static void Postfix(CoreGameManager __instance)
        {
            BaldiRampagePatches.Post_EndGame(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemManager), "UseItem")]
    public static class ItemMischiefPatch
    {
        private sealed class SwapState
        {
            public ItemObject original;
            public ItemObject fake;
            public int slot = -1;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        static bool Prefix(ItemManager __instance, out SwapState __state)
        {
            __state = new SwapState();
            try
            {
                if (ChaosManager.Instance == null || !ChaosManager.Instance.IsItemMischiefActive) return true;
                if (!ChaosManager.Instance.IsLevelReady || ChaosManager.Instance.IsPitstopActiveForPatches()) return true;
                if (__instance == null || __instance.items == null || __instance.pm == null) return true;
                int slot = __instance.selectedItem;
                if (slot < 0 || slot >= __instance.items.Length) return true;
                ItemObject held = __instance.items[slot];
                if (held == null || held.itemType == Items.None) return true;
                if (BaldiRampageConfig.IsActive && held.itemType == Items.Apple) return true;

                ItemObject fake = ChaosManager.Instance.PickSchoolItem(held);
                if (fake == null || fake.item == null) return true;
                __state.original = held;
                __state.fake = fake;
                __state.slot = slot;
                __instance.items[slot] = fake;
                KnoxumsChaosModePlugin.Log.LogInfo("Item Mischief: " + ItemLabel(held) + " -> " + ItemLabel(fake));
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Item Mischief: " + ex);
            }
            return true;
        }

        [HarmonyPostfix]
        static void Postfix(ItemManager __instance, SwapState __state)
        {
            if (__state == null || __state.original == null || __instance == null || __instance.items == null) return;
            try
            {
                int slot = __state.slot;
                if (slot >= 0 && slot < __instance.items.Length)
                {
                    ItemObject now = __instance.items[slot];
                    bool consumed = now == null || now.itemType == Items.None || now != __state.fake;
                    if (!consumed) __instance.items[slot] = __state.original;
                }
            }
            catch { }
        }

        private static string ItemLabel(ItemObject io)
        {
            if (io == null) return "?";
            return !string.IsNullOrEmpty(io.name) ? io.name : io.itemType.ToString();
        }
    }

    [HarmonyPatch]
    public static class ChaosPatches
    {
        private static bool ShouldSkipPatch() => ChaosManager.Instance == null || !ChaosManager.Instance.IsLevelReady;

        [HarmonyPatch(typeof(BaseGameManager), "ExitedSpawn")]
        [HarmonyPostfix]
        public static void Postfix_ExitedSpawn(BaseGameManager __instance)
        {
            try
            {
                if (ChaosManager.Instance != null && ChaosManager.Instance.IsChaosModeActive)
                {
                    if (Singleton<MusicManager>.Instance != null)
                    {
                        Singleton<MusicManager>.Instance.StopMidi();
                        Singleton<MusicManager>.Instance.StopFile();
                    }
                    __instance.BeginSpoopMode();
                    try { __instance.Ec?.StartEventTimers(); } catch { }
                }
                if (ChaosManager.Instance != null)
                {
                    ChaosManager.Instance.EndFloorIntro();
                    ChaosManager.Instance.IsLevelReady = true;
                    ChaosManager.Instance.ActivateSchoolShuffle();
                    ChaosManager.Instance.ApplyCurrentLapSpeedBoost();
                    ChaosManager.Instance.RegisterOriginalPickups();
                    if (ElevatorUnlockService.IsPitstopManager(__instance))
                    {
                        ChaosManager.Instance.StopFunSettings();
                        ElevatorUnlockService.KeepPitstopElevatorsOpen(__instance);
                    }
                    else ChaosManager.Instance.AllowFunSettings();
                }
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("ExitedSpawn: " + ex.Message); }
        }

        [HarmonyPatch(typeof(BaseGameManager), "BeginSpoopMode")]
        [HarmonyPostfix]
        public static void Postfix_BeginSpoopMode_AudioWindow()
        {


            ChaosManager.Instance?.EndFloorIntro();
        }

        [HarmonyPatch(typeof(BaseGameManager), "CollectNotebooks")]
        [HarmonyPostfix]
        public static void Postfix_CollectNotebooks(BaseGameManager __instance, int count)
        {
            try
            {
                if (ChaosManager.Instance != null && ChaosManager.Instance.IsChaosModeActive && count > 0)
                    ChaosManager.Instance.HandleNotebookCollection(__instance.FoundNotebooks);
                if (ChaosManager.Instance != null && count > 0)
                {
                    ChaosManager.Instance.ShuffleItemPositions();
                    ChaosManager.Instance.ShuffleCharPositions();
                }
                if (__instance != null && !(__instance is EndlessGameManager))
                {
                    int found = __instance.FoundNotebooks;
                    int total = __instance.NotebookTotal;
                    if (total > 0 && found >= total && !ElevatorUnlockService.ElevatorsUnlockedThisFloor
                        && !BaldiRampageConfig.IsActive)
                    {

                        ElevatorUnlockService.OnAllNotebooks(__instance, "ChaosPatches CollectNotebooks");
                    }
                }
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("CollectNotebooks: " + ex.Message); }
        }


        [HarmonyPatch(typeof(BaseGameManager), "AllNotebooks")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix_LapsAllNotebooks(BaseGameManager __instance)
        {
            return true;
        }

        [HarmonyPatch(typeof(MainGameManager), "AllNotebooks")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix_LapsMainAllNotebooks(MainGameManager __instance)
        {
            return true;
        }

        [HarmonyPatch(typeof(MainGameManager), "CreateHappyBaldi")]
        [HarmonyPrefix]
        public static bool Prefix_CreateHappyBaldi()
        {
            if (ChaosManager.Instance != null && ChaosManager.Instance.IsLapsActive) return true;
            return !(ChaosManager.Instance != null && ChaosManager.Instance.IsChaosModeActive);
        }

        [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
        [HarmonyPrefix]
        public static bool Prefix_SpawnNPCs(EnvironmentController __instance)
        {
            if (ChaosManager.Instance != null && ChaosManager.Instance.IsChaosModeActive
                && ChaosManager.Instance.NotebooksCollectedCount == 0 && !ChaosManager.Instance.IsLapsActive)
                return false;
            try
            {
                if (Singleton<CoreGameManager>.Instance != null
                    && Singleton<CoreGameManager>.Instance.currentMode == Mode.Free && __instance.npcsToSpawn != null)
                {
                    for (int i = __instance.npcsToSpawn.Count - 1; i >= 0; i--)
                        if (__instance.npcsToSpawn[i] != null && __instance.npcsToSpawn[i].Character == Character.Baldi)
                            __instance.npcsToSpawn.RemoveAt(i);
                }
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(MusicManager), "PlayMidi", new Type[] { typeof(string), typeof(float), typeof(bool) })]
        [HarmonyPrefix]
        public static bool Prefix_PlayMidi(ref string song)
        {
            if (song != "school" || ChaosManager.Instance == null) return true;
            if (ChaosManager.Instance.FloorIntroActive) return true;
            return !ChaosManager.Instance.IsChaosModeActive;
        }

        [HarmonyPatch(typeof(RandomEvent), "Begin")]
        [HarmonyPrefix]
        public static void Prefix_Begin(RandomEvent __instance)
        {


            if (ChaosManager.Instance != null && ChaosManager.Instance.IsEventPropsShuffleActive)
                ChaosManager.Instance.ShuffleEventProperties(__instance);
        }

        [HarmonyPatch(typeof(RandomEvent), "Begin")]
        [HarmonyPostfix]
        public static void Postfix_Begin(RandomEvent __instance)
        {
            if (__instance == null || ChaosManager.Instance == null || !ChaosManager.Instance.IsChaosModeActive) return;
            try { R.Set(__instance, "active", true); } catch { }
        }

        [HarmonyPatch(typeof(BaseGameManager), "Initialize")]
        [HarmonyPostfix]
        public static void Postfix_Initialize(BaseGameManager __instance)
        {
            try
            {
                ElevatorUnlockService.ResetForNewFloorOrLap();
                int seed = 0;
                Type st = __instance.GetType();
                while (st != null && st != typeof(object))
                {
                    FieldInfo[] fields = st.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    bool got = false;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        FieldInfo f = fields[i];
                        if (!f.Name.ToLowerInvariant().Contains("seed")) continue;
                        object v = f.GetValue(__instance);
                        if (v is int iv) { seed = iv; got = true; break; }
                        if (v is long lv) { seed = (int)lv; got = true; break; }
                    }
                    if (got) break;
                    st = st.BaseType;
                }
                if (seed == 11211994 && ChaosManager.Instance != null && !ChaosManager.Instance.IsChaosModeActive)
                    ChaosManager.Instance.ToggleChaosMode();
                if (ChaosManager.Instance != null)
                {
                    ChaosManager.Instance.IsLevelReady = false;


                    ChaosManager.Instance.ResetSchoolShuffle();
                    ChaosManager.Instance.RestoreLapAfterRestart();

                    if (ElevatorUnlockService.IsPitstopManager(__instance))
                    {
                        ChaosManager.Instance.ClearFloorExitCommit();
                        ChaosManager.Instance.EndFloorIntro();
                    }
                    else ChaosManager.Instance.BeginBaldiCountdownAudioWindow();
                    ChaosManager.Instance.ApplyFunAfterPostGen(__instance);


                }
                if (ChaosManager.Instance != null && ChaosManager.Instance.IsChaosModeActive)
                {
                    ChaosManager.Instance.NotebooksCollectedCount = 0;
                    ChaosManager.Instance.SilenceStartMusic();
                }
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Initialize: " + ex.Message); }
        }

        [HarmonyPatch(typeof(BaseGameManager), "BeginPlay")]
        [HarmonyPostfix]
        public static void Postfix_BeginPlay(BaseGameManager __instance)
        {
            try
            {
                if (ChaosManager.Instance == null) return;
                if (ElevatorUnlockService.IsPitstopManager(__instance))
                    ChaosManager.Instance.EndFloorIntro();
                else ChaosManager.Instance.BeginBaldiCountdownAudioWindow();
                ChaosManager.Instance.CaptureFloorYtpStart();
                ChaosManager.Instance.ActivateSchoolShuffle();
                if (ChaosManager.Instance.IsBuildersErrorActive) ChaosManager.Instance.ClearMapDiscovery(__instance);
                ElevatorUnlockService.ClearClosedElevatorFrontBarriers(__instance);
                if (ElevatorUnlockService.IsPitstopManager(__instance))
                {
                    ChaosManager.Instance.StopFunSettings();
                    ElevatorUnlockService.KeepPitstopElevatorsOpen(__instance);
                    ChaosManager.Instance.ShowPitstopChaosReminder();
                }
                else
                {


                    GameplayModifierManager.Instance?.NotifyBeginPlay(__instance);
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(BaseGameManager), "LoadNextLevel")]
        [HarmonyPrefix]
        public static bool Prefix_LoadNextLevel(BaseGameManager __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.OnFloorLeaving();
                ChaosManager cm = ChaosManager.Instance;


                if (cm != null && cm.IsLapTransitionInProgress) return false;

                if (ElevatorUnlockService.IsPitstopManager(__instance))
                {
                    if (cm != null)
                    {
                        cm.ResetLapsToDefault();
                        cm.ClearFloorExitCommit();
                        cm.IsLevelReady = false;
                        cm.ResetSchoolShuffle();
                    }
                    return true;
                }


                if (cm != null && cm.ShouldStartNewLap())
                {
                    cm.StartInstantNewLap(__instance, true);
                    return false;
                }

                if (cm != null)
                {
                    cm.CommitFloorExitToPitstop();
                    cm.IsLevelReady = false;
                }
                ElevatorUnlockService.MarkLoadNextStarted();
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Base LoadNextLevel lap hook: " + ex);
            }
            return true;
        }

        [HarmonyPatch(typeof(MainGameManager), "LoadNextLevel")]
        [HarmonyPrefix]
        public static bool Prefix_MainLoadNextLevel(MainGameManager __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.OnFloorLeaving();
                ChaosManager cm = ChaosManager.Instance;
                if (cm != null && cm.IsLapTransitionInProgress) return false;
                if (cm != null && cm.ShouldStartNewLap())
                {
                    cm.StartInstantNewLap(__instance, true);
                    return false;
                }
                if (cm != null)
                {
                    cm.CommitFloorExitToPitstop();
                    cm.IsLevelReady = false;
                }
                ElevatorUnlockService.MarkLoadNextStarted();
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Main LoadNextLevel lap hook: " + ex);
            }
            return true;
        }

        [HarmonyPatch(typeof(EndlessGameManager), "LoadNextLevel")]
        [HarmonyPrefix]
        public static bool Prefix_EndlessLoadNextLevel(EndlessGameManager __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.OnFloorLeaving();
                ChaosManager cm = ChaosManager.Instance;
                if (cm != null && cm.IsLapTransitionInProgress) return false;
                if (cm != null && cm.ShouldStartNewLap())
                {
                    cm.StartInstantNewLap(__instance, true);
                    return false;
                }

                if (cm != null) cm.IsLevelReady = false;
                ElevatorUnlockService.MarkLoadNextStarted();
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Endless LoadNextLevel lap hook: " + ex);
            }
            return true;
        }

        [HarmonyPatch(typeof(BaseGameManager), "EndGame")]
        [HarmonyPrefix]
        public static void Prefix_EndGame(BaseGameManager __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.OnFloorLeaving();
                if (ChaosManager.Instance != null && ChaosManager.Instance.IsBuildersErrorActive)
                    ChaosManager.Instance.ClearMapDiscovery(__instance);
            }
            catch { }
        }

        [HarmonyPatch(typeof(BaseGameManager), "RestartLevel")]
        [HarmonyPrefix]
        public static void Prefix_RestartLevel(BaseGameManager __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.OnFloorLeaving();
                ElevatorUnlockService.ResetForNewFloorOrLap();
                if (ChaosManager.Instance != null)
                {
                    if (ChaosManager.Instance.IsBuildersErrorActive) ChaosManager.Instance.ClearMapDiscovery(__instance);
                    ChaosManager.Instance.IsLevelReady = false;
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(LevelBuilder), "StartGenerate")]
        [HarmonyPrefix]
        public static void Prefix_StartGenerate(LevelBuilder __instance)
        {
            try
            {
                GameplayModifierManager.Instance?.PrepareForGeneration(__instance);
                if (ChaosManager.Instance != null)
                {
                    ChaosManager.Instance.MarkGenerationStarted();
                    if (ChaosManager.Instance.IsBuildersErrorActive) ChaosManager.Instance.ApplyBuildersError(__instance);
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(NPC), "Initialize")]
        [HarmonyPostfix]
        public static void Postfix_NPC_Init(NPC __instance)
        {
            try
            {
                if (ChaosManager.Instance == null) return;
                if (ChaosManager.Instance.IsCharPropShuffleActive)
                    ChaosManager.Instance.ShuffleNpcProperties(__instance);


            }
            catch { }
        }

        [HarmonyPatch(typeof(Item), "Use")]
        [HarmonyPrefix]
        public static void Prefix_ItemUse(Item __instance)
        {
            try
            {
                if (ChaosManager.Instance != null && ChaosManager.Instance.IsItemPropShuffleActive)
                    ChaosManager.Instance.ShuffleItemProperties(__instance);
            }
            catch { }
        }

        [HarmonyPatch(typeof(SpriteRenderer), "sprite", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Prefix_SR(SpriteRenderer __instance, ref Sprite value)
        {
            if (ChaosManager.Instance == null || (!ChaosManager.Instance.IsCharacterSpritesShuffleActive
                && !ChaosManager.Instance.IsItemSpritesShuffleActive) || ShouldSkipPatch() || value == null) return;
            try
            {
                if (ChaosManager.Instance.IsCharacterSpritesShuffleActive)
                {
                    NPC n = __instance.GetComponentInParent<NPC>();
                    if (n != null)
                    {
                        value = ChaosManager.Instance.GetShuffledCharacterSprite(value, n);
                        return;
                    }
                }
                if (ChaosManager.Instance.IsItemSpritesShuffleActive
                    && __instance.GetComponentInParent<Pickup>() != null)
                    value = ChaosManager.Instance.GetShuffledItemSprite(value);
            }
            catch { }
        }

        [HarmonyPatch(typeof(Image), "sprite", MethodType.Setter)]
        [HarmonyPrefix]
        public static bool Prefix_Img(Image __instance, ref Sprite value)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsItemSpritesShuffleActive
                || ShouldSkipPatch() || value == null) return true;
            try
            {
                ItemSlotsManager sm = __instance.GetComponentInParent<ItemSlotsManager>();
                if (sm != null)
                {
                    FieldInfo cf = typeof(ItemSlotsManager).GetField("itemCover", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cf != null)
                    {
                        Image[] cv = cf.GetValue(sm) as Image[];
                        if (cv != null)
                            for (int i = 0; i < cv.Length; i++) if (cv[i] == __instance) return true;
                    }
                    value = ChaosManager.Instance.GetShuffledItemSprite(value);
                    return true;
                }
                string n = __instance.gameObject.name.ToLowerInvariant();
                string pn = __instance.transform.parent != null ? __instance.transform.parent.name.ToLowerInvariant() : "";
                if (n.Contains("cursor") || pn.Contains("cursor") || n.Contains("pointer") || pn.Contains("pointer")) return true;
                if (n.Contains("left") || n.Contains("right") || n.Contains("center") || n.Contains("frame")
                    || n.Contains("border") || n.Contains("bg") || n.Contains("background")
                    || pn.Contains("left") || pn.Contains("right") || pn.Contains("center")
                    || pn.Contains("frame") || pn.Contains("border")) return true;
                if (n.Contains("slot") || n.Contains("inventory") || n.Contains("item") || n.Contains("icon")
                    || pn.Contains("slot") || pn.Contains("inventory") || pn.Contains("item") || pn.Contains("icon"))
                    value = ChaosManager.Instance.GetShuffledItemSprite(value);
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(LocalizationManager), "GetLocalizedText", new Type[] { typeof(string) })]
        [HarmonyPostfix]
        public static void Post_Loc1(string key, ref string __result)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsStringsShuffleActive
                || ShouldSkipPatch() || string.IsNullOrEmpty(__result) || IsProtKey(key)) return;
            try { __result = ChaosManager.Instance.GetShuffledString(__result); } catch { }
        }

        [HarmonyPatch(typeof(LocalizationManager), "GetLocalizedText", new Type[] { typeof(string), typeof(bool) })]
        [HarmonyPostfix]
        public static void Post_Loc2(string key, ref string __result)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsStringsShuffleActive
                || ShouldSkipPatch() || string.IsNullOrEmpty(__result) || IsProtKey(key)) return;
            try { __result = ChaosManager.Instance.GetShuffledString(__result); } catch { }
        }

        private static bool IsProtKey(string k)
        {
            if (string.IsNullOrEmpty(k)) return false;
            string l = k.ToLowerInvariant();
            return l.Contains("menu") || l.Contains("pause") || l.Contains("option") || l.Contains("setting")
                || l.Contains("button") || l.Contains("btn_") || l.Contains("elevator") || l.Contains("elev_")
                || l.Contains("seed") || l.Contains("map_") || l.Contains("map ") || l.Contains("sticker")
                || l.Contains("stk_") || l.Contains("pitstop") || l.Contains("pit_") || l.Contains("store")
                || l.Contains("shop") || l.Contains("johnny") || l.Contains("ytp") || l.Contains("mode_")
                || l.Contains("title") || l.Contains("tooltip") || l.Contains("hint") || l.Contains("fieldtrip")
                || l.Contains("field_") || l.Contains("save") || l.Contains("quit") || l.Contains("restart")
                || l.Contains("continue") || l.Contains("notebooks");
        }

        [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Pre_TMP(TMP_Text __instance, ref string value)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsStringsShuffleActive
                || ShouldSkipPatch() || string.IsNullOrEmpty(value)) return;
            try { if (!IsProtUI(__instance)) value = ChaosManager.Instance.GetShuffledString(value); }
            catch { }
        }

        [HarmonyPatch(typeof(TextMesh), "text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Pre_TM(ref string value)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsStringsShuffleActive
                || ShouldSkipPatch() || string.IsNullOrEmpty(value)) return;
            try { value = ChaosManager.Instance.GetShuffledString(value); } catch { }
        }

        internal static bool IsProtUI(TMP_Text t)
        {
            if (t == null) return false;
            Transform c = t.transform;
            for (int d = 0; d < 6 && c != null; d++)
            {
                string n = c.gameObject.name.ToLowerInvariant();
                if (n.Contains("pause") || n.Contains("option") || n.Contains("setting")
                    || (n.Contains("map") && !n.Contains("remap")) || n.Contains("sticker")
                    || n.Contains("elevator") || n.Contains("elev") || n.Contains("seed")
                    || n.Contains("loading") || n.Contains("load") || n.Contains("notebook")
                    || n.Contains("timer") || n.Contains("pitstop") || n.Contains("pit_stop")
                    || n.Contains("johnny") || n.Contains("store") || n.Contains("shop")
                    || n.Contains("fieldtrip") || n.Contains("field_trip") || n.Contains("ytp")) return true;
                c = c.parent;
            }
            return false;
        }

        private static bool SkipAudio(AudioClip c)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsLevelReady
                || !ChaosManager.Instance.IsSoundsShuffleActive || c == null || !ChaosManager.Instance.IsInGame()) return true;
            string l = c.name.ToLowerInvariant();
            return l.Contains("elv_buzz") || l.Contains("pause") || l.Contains("menu") || l.Contains("click")
                || l.Contains("hover") || l.Contains("select") || l.Contains("cursor");
        }

        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip) })]
        [HarmonyPrefix]
        public static void Pre_POS1(ref AudioClip clip)
        {
            if (SoundShuffleDetachedPlaybackPatch.BypassRemap) return;
            if (!SkipAudio(clip)) try { clip = ChaosManager.Instance.GetShuffledAudioClip(clip); } catch { }
        }

        [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip), typeof(float) })]
        [HarmonyPrefix]
        public static void Pre_POS2(ref AudioClip clip)
        {
            if (SoundShuffleDetachedPlaybackPatch.BypassRemap) return;
            if (!SkipAudio(clip)) try { clip = ChaosManager.Instance.GetShuffledAudioClip(clip); } catch { }
        }

        [HarmonyPatch(typeof(AudioSource), "clip", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Pre_AC(AudioSource __instance, ref AudioClip value)
        {
            if (SoundShuffleDetachedPlaybackPatch.BypassRemap) return;
            if (SkipAudio(value))
            {
                SoundShuffleDetachedPlaybackPatch.Unmark(__instance);
                return;
            }
            try
            {
                AudioClip original = value;
                value = ChaosManager.Instance.GetShuffledAudioClip(value);
                SoundShuffleDetachedPlaybackPatch.Mark(
                    __instance, value, original != null ? original.length : 0f);
            }
            catch { SoundShuffleDetachedPlaybackPatch.Unmark(__instance); }
        }

        [HarmonyPatch(typeof(StandardDoor), "Lock")]
        [HarmonyPostfix]
        public static void Post_DoorLock(StandardDoor __instance)
        {
            try
            {
                if (ChaosManager.Instance == null || !ChaosManager.Instance.IsChaosModeActive
                    || !ChaosManager.Instance.IncludeExits || !ChaosManager.Instance.IsLevelReady) return;
                bool isExit = false;
                string nm = __instance.gameObject.name.ToLowerInvariant();
                if (nm.Contains("exit") || nm.Contains("escape")) isExit = true;
                if (!isExit && __instance.transform.parent != null)
                {
                    string pn = __instance.transform.parent.name.ToLowerInvariant();
                    if (pn.Contains("exit") || pn.Contains("escape")) isExit = true;
                }
                if (!isExit)
                {
                    BaseGameManager b = UnityEngine.Object.FindObjectOfType<BaseGameManager>();
                    if (b?.Ec?.Elevators != null)
                        foreach (Elevator e in b.Ec.Elevators)
                            if (e != null && Vector3.Distance(__instance.transform.position, e.transform.position) < 15f)
                            { isExit = true; break; }
                }
                if (isExit)
                    ChaosManager.Instance.HandleNotebookCollection(ChaosManager.Instance.NotebooksCollectedCount + 1);
            }
            catch { }
        }

        [HarmonyPatch(typeof(InputManager), "GetDigitalInput", new Type[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        public static void Pre_Input(ref string id)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsCtrlMapShuffleActive
                || !ChaosManager.Instance.IsLevelReady) return;
            if (id == "CameraX" || id == "CameraY" || id == "CameraXDelta" || id == "CameraYDelta"
                || id == "CursorX" || id == "CursorY" || id == "CursorXDelta" || id == "CursorYDelta"
                || id == "Pause" || id == "Map" || id == "MapPlus" || id == "Stickers"
                || id == "MapZoomPos" || id == "MapZoomNeg" || id == "MouseBoost"
                || id == "MapMovemeX" || id == "MapMovemeY" || id == "MapMoveXDelta" || id == "MapMoveYDelta") return;
            id = ChaosManager.Instance.GetRemappedAction(id);
        }

        [HarmonyPatch(typeof(EnvironmentController), "EventTimer")]
        [HarmonyPostfix]
        public static void Post_EvtTimer(EnvironmentController __instance, ref IEnumerator __result,
            RandomEvent randomEvent, float time, bool timeOut)
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsDoubleEventsActive || timeOut || __result == null) return;
            __result = DblEvtWrap(__result, __instance, randomEvent);
        }

        private static IEnumerator DblEvtWrap(IEnumerator orig, EnvironmentController ec, RandomEvent trigEvt)
        {
            RandomEvent pair = ChaosManager.Instance.FindPairEventFor(ec, trigEvt);
            while (true)
            {
                bool h;
                object c = null;
                try { h = orig.MoveNext(); if (h) c = orig.Current; else break; }
                catch { yield break; }
                yield return c;
            }
            if (pair == null || pair.Active) yield break;
            try
            {
                BaldiTV tv = Singleton<CoreGameManager>.Instance?.GetHud(0)?.BaldiTv;
                if (tv != null && pair.EventIntro != null) tv.AnnounceEvent(pair.EventIntro);
                pair.Begin();
                FieldInfo cef = typeof(EnvironmentController).GetField("currentEvents", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo cetf = typeof(EnvironmentController).GetField("currentEventTypes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                List<RandomEvent> ce = cef != null ? cef.GetValue(ec) as List<RandomEvent> : null;
                if (ce != null && !ce.Contains(pair)) ce.Add(pair);
                List<RandomEventType> ct = cetf != null ? cetf.GetValue(ec) as List<RandomEventType> : null;
                if (ct != null && !ct.Contains(pair.Type)) ct.Add(pair.Type);
                ChaosManager.Instance.MarkEventAsPaired(pair);
            }
            catch { }
        }

        [HarmonyPatch(typeof(HudManager), "Awake")]
        [HarmonyPostfix]
        public static void Postfix_HudManager_Awake(HudManager __instance)
        {
            try
            {
                if (ChaosManager.Instance == null || !ChaosManager.Instance.IsLevelReady) return;
                if (ChaosManager.Instance.IsLapsActive) ChaosManager.Instance.InjectLapsIntoHud(__instance);


            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(ElevatorScreen), "Start")]
    public static class GameplayModifierElevatorScreenPatch
    {
        [HarmonyPostfix]
        static void Postfix(ElevatorScreen __instance)
        {
            GameplayModifierManager.Instance?.OnElevatorScreenStarted(__instance);
        }
    }

    [HarmonyPatch(typeof(ElevatorScreen), "ShowResults")]
    public static class GameplayModifierElevatorResultsPatch
    {
        [HarmonyPrefix]
        static void Prefix(ElevatorScreen __instance)
        {
            GameplayModifierManager.Instance?.OnElevatorResults(__instance);
        }
    }

    [HarmonyPatch]
    public static class SoundShuffleNoAudioWaitPatch
    {
        private static readonly List<MethodBase> waitGetters = FindWaitGetters();

        static bool Prepare()
        {
            try
            {
                KnoxumsChaosModePlugin.Log.LogInfo(
                    "Sound Shuffle no-wait: hooked " + waitGetters.Count
                    + " AudioManager playing getters.");
            }
            catch { }
            return waitGetters.Count > 0;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            for (int i = 0; i < waitGetters.Count; i++)
                yield return waitGetters[i];
        }

        private static List<MethodBase> FindWaitGetters()
        {
            List<MethodBase> result = new List<MethodBase>();
            HashSet<MethodBase> seen = new HashSet<MethodBase>();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic;

            try
            {
                PropertyInfo[] properties = typeof(AudioManager).GetProperties(flags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (property == null || property.PropertyType != typeof(bool)) continue;
                    string name = property.Name.ToLowerInvariant();
                    if (!name.Contains("playing")
                        || (!name.Contains("audio") && !name.Contains("queue")
                            && !name.Contains("sound"))) continue;
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter != null && seen.Add(getter)) result.Add(getter);
                }

                MethodInfo[] methods = typeof(AudioManager).GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method == null || method.IsSpecialName
                        || method.ReturnType != typeof(bool)
                        || method.GetParameters().Length != 0) continue;
                    string name = method.Name.ToLowerInvariant();
                    if (!name.Contains("playing")
                        || (!name.Contains("audio") && !name.Contains("queue")
                            && !name.Contains("sound"))) continue;
                    if (seen.Add(method)) result.Add(method);
                }
            }
            catch { }
            return result;
        }

        internal static bool Active
        {
            get
            {
                try
                {
                    ChaosManager chaos = ChaosManager.Instance;


                    return chaos != null && chaos.IsSoundsShuffleActive
                        && chaos.FloorIntroActive && chaos.IsLevelReady
                        && !chaos.IsPaused();
                }
                catch { return false; }
            }
        }

        static void Postfix(AudioManager __instance, ref bool __result)
        {
            if (!Active) return;
            if (SoundShuffleDetachedPlaybackPatch.TryGetVirtualPlaying(
                __instance, out bool virtualPlaying))
            {


                __result = __result || virtualPlaying;
            }
        }
    }


    [HarmonyPatch]
    public static class SoundShuffleDetachedPlaybackPatch
    {
        private sealed class MarkedSource
        {
            public AudioSource source;
            public AudioClip clip;
            public float originalDuration;
            public float virtualWaitUntil;
        }

        private static readonly Dictionary<int, MarkedSource> marked =
            new Dictionary<int, MarkedSource>();

        [ThreadStatic] private static bool bypassRemap;
        internal static bool BypassRemap => bypassRemap;

        static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods = typeof(AudioSource).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            HashSet<MethodBase> seen = new HashSet<MethodBase>();
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.Name != "Play"
                    || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                bool supported = parameters.Length == 0
                    || (parameters.Length == 1
                        && parameters[0].ParameterType == typeof(ulong));
                if (supported && seen.Add(method)) yield return method;
            }
        }

        static bool Prefix(AudioSource __instance)
        {
            if (bypassRemap || !SoundShuffleNoAudioWaitPatch.Active) return true;
            return !TryPlayDetached(__instance);
        }

        internal static void Mark(AudioSource source, AudioClip clip, float originalDuration)
        {
            if (source == null || clip == null) return;
            int id = source.GetInstanceID();
            if (!marked.TryGetValue(id, out MarkedSource entry)
                || entry == null || entry.source != source)
            {
                entry = new MarkedSource { source = source };
                marked[id] = entry;
            }
            entry.clip = clip;
            entry.originalDuration = Mathf.Max(0f, originalDuration);
        }

        internal static void Unmark(AudioSource source)
        {
            if (source != null) marked.Remove(source.GetInstanceID());
        }

        internal static void ClearMarks()
        {
            marked.Clear();
            bypassRemap = false;
        }

        internal static bool TryGetVirtualPlaying(AudioManager manager, out bool playing)
        {
            if (manager == null) { playing = false; return false; }
            return TryGetVirtualPlaying(manager.audioDevice, out playing);
        }

        internal static bool TryGetVirtualPlaying(AudioSource source, out bool playing)
        {
            playing = false;
            if (source == null) return false;
            int id = source.GetInstanceID();
            if (!marked.TryGetValue(id, out MarkedSource entry)
                || entry == null || entry.source != source)
            {
                marked.Remove(id);
                return false;
            }
            playing = Time.unscaledTime < entry.virtualWaitUntil;
            return true;
        }

        private static bool TryPlayDetached(AudioSource source)
        {
            if (source == null || source.clip == null || source.loop) return false;
            int id = source.GetInstanceID();
            if (!marked.TryGetValue(id, out MarkedSource entry)
                || entry == null || entry.source != source || entry.clip != source.clip)
            {
                marked.Remove(id);
                return false;
            }

            GameObject holder = null;
            try
            {
                holder = new GameObject("SoundShuffleDetachedAudio");
                holder.transform.position = source.transform.position;
                holder.transform.rotation = source.transform.rotation;
                AudioSource detached = holder.AddComponent<AudioSource>();
                detached.playOnAwake = false;
                detached.loop = false;
                detached.outputAudioMixerGroup = source.outputAudioMixerGroup;
                detached.mute = source.mute;
                detached.bypassEffects = source.bypassEffects;
                detached.bypassListenerEffects = source.bypassListenerEffects;
                detached.bypassReverbZones = source.bypassReverbZones;
                detached.priority = source.priority;
                detached.volume = source.volume;
                detached.pitch = source.pitch;
                detached.panStereo = source.panStereo;
                detached.spatialBlend = source.spatialBlend;
                detached.reverbZoneMix = source.reverbZoneMix;
                detached.dopplerLevel = source.dopplerLevel;
                detached.spread = source.spread;
                detached.rolloffMode = source.rolloffMode;
                detached.minDistance = source.minDistance;
                detached.maxDistance = source.maxDistance;
                detached.ignoreListenerPause = source.ignoreListenerPause;
                detached.ignoreListenerVolume = source.ignoreListenerVolume;

                bypassRemap = true;
                detached.clip = source.clip;
                detached.Play();
                bypassRemap = false;

                float pitch = Mathf.Max(.01f, Mathf.Abs(detached.pitch));


                entry.virtualWaitUntil = Time.unscaledTime
                    + entry.originalDuration / pitch;
                UnityEngine.Object.Destroy(holder,
                    Mathf.Max(.1f, detached.clip.length / pitch + .25f));
                return true;
            }
            catch (Exception ex)
            {
                bypassRemap = false;
                Unmark(source);
                if (holder != null) UnityEngine.Object.Destroy(holder);
                try
                {
                    KnoxumsChaosModePlugin.Log.LogWarning(
                        "Sound Shuffle detached playback: " + ex.Message);
                }
                catch { }
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(AudioSource), "get_isPlaying")]
    public static class SoundShuffleVirtualIsPlayingPatch
    {
        [HarmonyPostfix]
        static void Postfix(AudioSource __instance, ref bool __result)
        {
            if (!SoundShuffleNoAudioWaitPatch.Active) return;
            if (SoundShuffleDetachedPlaybackPatch.TryGetVirtualPlaying(
                __instance, out bool virtualPlaying))
                __result = virtualPlaying;
        }
    }

    public static class FunLookInvertPatch
    {
        private static Harmony harmony;
        private static bool analogHooked;
        public static bool NeedPlusLook;

        public static void ManualPatch(Harmony h)
        {
            harmony = h;
            if (h != null) HookCameraLookAnalogOnly();
        }

        public static void TryHookGameCamera()
        {
            if (!analogHooked) HookCameraLookAnalogOnly();
        }

        static void HookCameraLookAnalogOnly()
        {
            if (analogHooked || harmony == null) return;
            try
            {
                MethodInfo pf = typeof(FunLookInvertPatch).GetMethod("PostfixCameraLookAnalog",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (pf == null) return;
                MethodInfo[] methods = typeof(InputManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int n = 0;
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo m = methods[i];
                    if (m == null || m.Name != "GetAnalogInput" || m.ReturnType != typeof(void)) continue;
                    ParameterInfo[] ps = m.GetParameters();
                    bool hasPos = ps.Any(p => p != null && p.Name == "analogPos");
                    bool hasDelta = ps.Any(p => p != null && p.Name == "analogDelta");
                    if (!hasPos || !hasDelta) continue;
                    try { harmony.Patch(m, postfix: new HarmonyMethod(pf)); n++; } catch { }
                }
                analogHooked = n > 0;
                KnoxumsChaosModePlugin.Log.LogInfo("Mirrored: camera-look analog only, hooked " + n);
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("FunLookInvertPatch: " + ex.Message); }
        }

        static bool IsCameraLookData(object analogData)
        {
            if (analogData == null) return false;
            try
            {
                object cam = Singleton<CoreGameManager>.Instance?.GetCamera(0);
                if (cam == null) return false;
                Type t = cam.GetType();
                while (t != null && t != typeof(object))
                {
                    FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        FieldInfo f = fields[i];
                        if (f == null || f.FieldType.IsValueType || !f.FieldType.Name.Contains("AnalogInput")) continue;
                        object v = f.GetValue(cam);
                        if (v != null && ReferenceEquals(v, analogData)) return true;
                    }
                    t = t.BaseType;
                }
            }
            catch { }
            return false;
        }

        static void PostfixCameraLookAnalog(object __0, ref Vector2 analogPos, ref Vector2 analogDelta)
        {
            try
            {
                if (!NeedPlusLook || ChaosManager.Instance == null || !ChaosManager.Instance.IsMirroredActive) return;
                if (!IsCameraLookData(__0)) return;
                analogPos.x = -analogPos.x;
                analogDelta.x = -analogDelta.x;
            }
            catch { }
        }
    }

    [HarmonyPatch]
    public static class NoLapYtpMultiplierPatch
    {
        [ThreadStatic] private static int nestedPointCalls;

        static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types = { typeof(CoreGameManager), typeof(BaseGameManager), typeof(HudManager) };
            HashSet<MethodBase> seen = new HashSet<MethodBase>();
            for (int t = 0; t < types.Length; t++)
            {
                MethodInfo[] methods = types[t].GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo m = methods[i];
                    if (m == null || m.IsAbstract || m.ContainsGenericParameters) continue;
                    string n = m.Name;
                    if (n != "AddPoints" && n != "AddYTPs" && n != "AddYtps" && n != "AwardPoints"
                        && n != "AddPointsNoAnimate") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length < 1 || ps[0].ParameterType != typeof(int)) continue;
                    if (seen.Add(m)) yield return m;
                }
            }
        }

        static void Prefix(ref int __0, out bool __state)
        {
            __state = false;
            try
            {
                if (ChaosManager.Instance == null || !ChaosManager.Instance.IsLapsActive) return;
                nestedPointCalls++;
                __state = true;
                if (nestedPointCalls > 1) return;
                int lap = ChaosManager.Instance.CurrentLap;
                int add = __0;
                if (lap > 1 && add != 0 && add % lap == 0)
                {
                    int raw = add / lap;
                    if (raw == 1 || raw == 5 || raw == 10 || raw == 15 || raw == 20 || raw == 25
                        || raw == 50 || raw == 75 || raw == 100 || raw == 200 || raw == 250 || raw == 500)
                        add = raw;
                }
                __0 = add;
                if (!ChaosManager.Instance.IsPitstopActiveForPatches()
                    && !ChaosManager.Instance.FloorExitToPitstopCommitted)
                    ChaosManager.Instance.TrackFloorYtpDelta(add);
            }
            catch { }
        }

        static void Postfix(bool __state)
        {
            if (__state && nestedPointCalls > 0) nestedPointCalls--;
        }
    }


    public enum ChaosModeType { Chaos, ChaosPlus1, DoubleChaos }
    public enum CloneSpawnPoint { CharPosition, CharSpawnPoint }

    public enum GameplayModifierMode
    {
        WholeRun,
        EveryFloor
    }

    public enum GameplayModifierId
    {
        DoubleTrouble,
        IceFloor,
        LethalTouchers,
        GottaSteal,
        McSpeeders,
        Overtime,
        NoItems,
        Placeholder,
        BrokenEars,
        ItemRoulette,
        Exhaustion,
        PermaEvent,
        NoCooldown,
        PartyStyle,
        Steamgen,
        ClumsyExplorer,
        NegativeStickers,
        Hyperwatchers,
        Overlearned,
        CloudyLenses,
        SneakyTricky,
        Posterizator,
        SqueeshNot
    }

    public static class GameplayModifierCatalog
    {
        private static readonly GameplayModifierId[] all =
            (GameplayModifierId[])Enum.GetValues(typeof(GameplayModifierId));

        private static readonly Dictionary<GameplayModifierId, string> names =
            new Dictionary<GameplayModifierId, string>
            {
                { GameplayModifierId.DoubleTrouble, "Double Trouble" },
                { GameplayModifierId.IceFloor, "Ice Floor" },
                { GameplayModifierId.LethalTouchers, "Lethal Touchers" },
                { GameplayModifierId.GottaSteal, "Gotta Steal, Steal, Steal!" },
                { GameplayModifierId.McSpeeders, "McSpeeders!" },
                { GameplayModifierId.Overtime, "Overtime" },
                { GameplayModifierId.NoItems, "No Items?" },
                { GameplayModifierId.Placeholder, "Placeholder!" },
                { GameplayModifierId.BrokenEars, "Broken Ears" },
                { GameplayModifierId.ItemRoulette, "Item Roulette" },
                { GameplayModifierId.Exhaustion, "Exhaustion" },
                { GameplayModifierId.PermaEvent, "Perma-event" },
                { GameplayModifierId.NoCooldown, "No Cooldown!" },
                { GameplayModifierId.PartyStyle, "Party Style!" },
                { GameplayModifierId.Steamgen, "Steamgen" },
                { GameplayModifierId.ClumsyExplorer, "Clumsy Explorer" },
                { GameplayModifierId.NegativeStickers, "Negative Stickers" },
                { GameplayModifierId.Hyperwatchers, "Hyperwatchers" },
                { GameplayModifierId.Overlearned, "Overlearned" },
                { GameplayModifierId.CloudyLenses, "Cloudy Lenses" },
                { GameplayModifierId.SneakyTricky, "Sneaky-Tricky!" },
                { GameplayModifierId.Posterizator, "Posterizator" },
                { GameplayModifierId.SqueeshNot, "Squeesh-not!" }
            };

        public static GameplayModifierId[] All => all;

        public static string Name(GameplayModifierId id)
        {
            return names.TryGetValue(id, out string name) ? name : id.ToString();
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("mtm101.rulerp.bbplus.baldidevapi", BepInDependency.DependencyFlags.HardDependency)]
    public class KnoxumsChaosModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.knoxum.chaosmode";
        public const string PluginName = "Knoxum's Chaos Mode PUBLIC BETA";
        public const string PluginVersion = "1.0";

        public static KnoxumsChaosModePlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance.Logger;

        public static ConfigEntry<bool> IsChaosModeEnabledConfig { get; private set; }
        public static ConfigEntry<ChaosModeType> SelectedChaosMode { get; private set; }
        public static ConfigEntry<bool> EnableSizeChaos { get; private set; }
        public static ConfigEntry<bool> IsPropShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsCharPropShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsItemPropShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<int> PropShuffleTemperatureConfig { get; private set; }
        public static ConfigEntry<bool> IsCharSpritesShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsItemSpritesShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsStringsShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsSoundsShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsCtrlMapShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsItemsPlaceShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsCharPlaceShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsBuildersErrorEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsDoubleEventsEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsDiscoShuffleEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsBaldiCowardEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsLapsEnabledConfig { get; private set; }
        public static ConfigEntry<int> LapsCountConfig { get; private set; }
        public static ConfigEntry<CloneSpawnPoint> CloneSpawnPointConfig { get; private set; }
        public static ConfigEntry<bool> IncludeExitsConfig { get; private set; }
        public static ConfigEntry<bool> EggConfig { get; private set; }
        public static ConfigEntry<bool> IsLightsOutEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsMirroredEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsGooshoesEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsLbTestSchoolEnabledConfig { get; private set; }
        public static ConfigEntry<bool> IsItemMischiefEnabledConfig { get; private set; }
        public static ConfigEntry<bool> DisableWarningConfig { get; private set; }
        public static ConfigEntry<bool> GameplayModifiersEnabledConfig { get; private set; }
        public static ConfigEntry<GameplayModifierMode> GameplayModifierModeConfig { get; private set; }
        public static ConfigEntry<int> GameplayModifierRollsConfig { get; private set; }

        private Harmony harmony;
        private GameObject chaosManagerObject;

        private void Awake()
        {
            Instance = this;
            IsChaosModeEnabledConfig = Config.Bind("Gameplay", "IsChaosModeEnabled", false, "Enable Chaos Mode. ");
            SelectedChaosMode = Config.Bind("Gameplay", "SelectedChaosMode", ChaosModeType.Chaos, "Chaos Mode type. ");


            EnableSizeChaos = Config.Bind("Gameplay", "EnableSizeChaos", false, "Legacy option (unused). ");
            IsPropShuffleEnabledConfig = Config.Bind("PropsShuffle", "IsPropShuffleEnabled", false, "Event Props Shuffle. ");
            IsCharPropShuffleEnabledConfig = Config.Bind("PropsShuffle", "IsCharPropShuffleEnabled", false, "Char Props Shuffle. ");
            IsItemPropShuffleEnabledConfig = Config.Bind("PropsShuffle", "IsItemPropShuffleEnabled", false, "Item Props Shuffle. ");
            PropShuffleTemperatureConfig = Config.Bind("PropsShuffle", "PropShuffleTemperature", 5, "Temperature 1-15. ");
            IsCharSpritesShuffleEnabledConfig = Config.Bind("SchoolShuffle", "IsCharSpritesShuffleEnabled", false, "Char sprites shuffle. ");
            IsItemSpritesShuffleEnabledConfig = Config.Bind("SchoolShuffle", "IsItemSpritesShuffleEnabled", false, "Item sprites shuffle. ");
            IsStringsShuffleEnabledConfig = Config.Bind("SchoolShuffle", "IsStringsShuffleEnabled", false, "Strings shuffle. ");
            IsSoundsShuffleEnabledConfig = Config.Bind("SchoolShuffle", "IsSoundsShuffleEnabled", false, "Sounds shuffle. ");
            IsCtrlMapShuffleEnabledConfig = Config.Bind("SchoolShuffle", "IsCtrlMapShuffleEnabled", false, "Control map shuffle. ");
            IsItemsPlaceShuffleEnabledConfig = Config.Bind("OtherChaos", "IsItemsPlaceShuffleEnabled", false, "Item positions shuffle. ");
            IsCharPlaceShuffleEnabledConfig = Config.Bind("OtherChaos", "IsCharPlaceShuffleEnabled", false, "Char positions shuffle. ");
            IsBuildersErrorEnabledConfig = Config.Bind("OtherChaos", "IsBuildersErrorEnabled", false, "Cruel school layout. ");
            IsDoubleEventsEnabledConfig = Config.Bind("OtherChaos", "IsDoubleEventsEnabled", false, "Two events at once. ");
            IsDiscoShuffleEnabledConfig = Config.Bind("OtherChaos", "IsDiscoShuffleEnabled", false, "Disco lighting. ");
            IsBaldiCowardEnabledConfig = Config.Bind("OtherChaos", "IsBaldiCowardEnabled", false, "Baldi-coward mode. ");
            IsLapsEnabledConfig = Config.Bind("OtherChaos", "IsLapsEnabled", false, "Enable laps mode. ");

            LapsCountConfig = Config.Bind("OtherChaos", "LapsCount", 2,
                "Number of laps (2-5). Set to 0 for infinite laps. ");
            CloneSpawnPointConfig = Config.Bind("Settings", "CloneSpawnPoint", CloneSpawnPoint.CharPosition, "Clone spawn location. ");
            IncludeExitsConfig = Config.Bind("Settings", "IncludeExits", false, "Spawn on exit lock. ");
            DisableWarningConfig = Config.Bind("Settings", "DisableWarning", false,
                "Disable the Knoxum's Chaos Mode photosensitivity warning on startup. ");
            EggConfig = Config.Bind("Secret", "egg", false, "You know what to do. ");
            IsLightsOutEnabledConfig = Config.Bind("FunSettings", "IsLightsOutEnabled", false, "Lights Out. ");
            IsMirroredEnabledConfig = Config.Bind("FunSettings", "IsMirroredEnabled", false, "Mirrored camera. ");
            IsGooshoesEnabledConfig = Config.Bind("FunSettings", "IsGooshoesEnabled", false, "53045009 / ceiling flip. ");
            IsLbTestSchoolEnabledConfig = Config.Bind("FunSettings", "IsLbTestSchoolEnabled", false, "LB Test School lights. ");
            IsItemMischiefEnabledConfig = Config.Bind("SchoolShuffle", "IsItemMischiefEnabled", false, "Use a random school item instead. ");
            GameplayModifiersEnabledConfig = Config.Bind("GameplayModifiers", "Enabled", false,
                "Enable random gameplay modifiers. ");
            GameplayModifierModeConfig = Config.Bind("GameplayModifiers", "Mode",
                GameplayModifierMode.WholeRun,
                "Keep one set for the whole run or reroll before every school floor. ");
            GameplayModifierRollsConfig = Config.Bind("GameplayModifiers", "Rolls", 3,
                "Number of modifier rolls (1-5). Duplicate rolls stack up to 3. ");

            BaldiRampageConfig.Init(Config);

            if (!(DisableWarningConfig?.Value ?? false))
            {
                try
                {
                    MTM101BaldiDevAPI.AddWarningScreen(
                        "<color=yellow>PHOTOSENSITIVITY / EPILEPSY WARNING</color>\n\n" +
                        "<b>Knoxum's Chaos Mode</b> can show flashing lights, rapid color changes, " +
                        "and bright white flashes.\n\n" +
                        "If you have photosensitive epilepsy or are sensitive to flashing images, " +
                        "stop playing if you feel unwell.", false);
                }
                catch (Exception ex) { Log.LogError("AddWarningScreen: " + ex.Message); }
            }

            try
            {
                harmony = new Harmony(PluginGuid);
                harmony.PatchAll();
                FunLookInvertPatch.ManualPatch(harmony);
            }
            catch (Exception ex) { Log.LogError("Harmony: " + ex); }

            chaosManagerObject = new GameObject("KnoxumsChaosManager");
            chaosManagerObject.AddComponent<ChaosManager>();
            chaosManagerObject.AddComponent<GameplayModifierManager>();
            DontDestroyOnLoad(chaosManagerObject);
        }

        private void Start()
        {
            try
            {
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "MTM101BaldAPI") { RegisterOptionsAPISafely(); break; }
            }
            catch { }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void RegisterOptionsAPISafely() { ChaosOptionsRegistrar.Register(); }

        public void RegisterChaosCategories(CustomOptionsHandler h)
        {
            h.AddCategory<ChaosOptionsCategory>("Knoxum's\nChaos Mode");
        }
    }

    [HarmonyPatch(typeof(WarningScreen), "Advance")]
    static class WarningScreenFitPatch
    {
        [HarmonyPostfix]
        static void Postfix(WarningScreen __instance) { Fit(__instance); }

        static void Fit(WarningScreen __instance)
        {
            if (__instance == null) return;
            TMP_Text box = null;
            try { box = __instance.textBox; } catch { }
            if (box == null)
            {
                try
                {
                    FieldInfo f = typeof(WarningScreen).GetField(
                        "textBox", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    box = f != null ? f.GetValue(__instance) as TMP_Text : null;
                }
                catch { }
            }
            if (box == null) return;
            string shown = box.text ?? "";
            if (shown.IndexOf("PHOTOSENSITIVITY", StringComparison.OrdinalIgnoreCase) < 0
                && shown.IndexOf("Knoxum", StringComparison.OrdinalIgnoreCase) < 0) return;

            try
            {
                RectTransform rt = box.rectTransform;
                if (rt == null) return;
                RectTransform parent = rt.parent as RectTransform;
                float pw = parent != null && parent.rect.width > 1f ? parent.rect.width : 480f;
                float ph = parent != null && parent.rect.height > 1f ? parent.rect.height : 360f;
                float widthNorm = Mathf.Min(1f, (4f / 3f) * (ph / pw));
                float half = widthNorm * 0.5f;
                rt.anchorMin = new Vector2(0.5f - half, 0f);
                rt.anchorMax = new Vector2(0.5f + half, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                box.enableWordWrapping = true;
                box.overflowMode = TextOverflowModes.Overflow;
                box.alignment = TextAlignmentOptions.Center;
                box.enableAutoSizing = true;
                box.fontSizeMin = 8f;
                box.fontSizeMax = 72f;
                box.ForceMeshUpdate();
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("WarningScreen Fit: " + ex.Message); }
        }
    }

    public static class ChaosOptionsRegistrar
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Register() { CustomOptionsCore.OnMenuInitialize += R2; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void R2(OptionsMenu m, CustomOptionsHandler h)
        {


            GameplayModifierManager.CaptureOptionsClipboardVisual(m);
            KnoxumsChaosModePlugin.Instance.RegisterChaosCategories(h);
        }
    }

    public class ChaosOptionsCategory : CustomOptionsCategory
    {
        private TextMeshProUGUI pageTitleText;
        private int activePage;
        private const int PAGES = 7;
        private GameObject p0, p1, p2, p3, p4, pMods, p5;
        private MenuToggle chaosT;
        private TextMeshProUGUI modeT;
        private int modeI;
        private StandardMenuButton modeLA, modeRA;
        private MenuToggle evtPT, chrPT, itmPT;
        private AdjustmentBars tempB;
        private MenuToggle chrST, itmST, strT, sndT, mischiefT;
        private GameObject p3s1, p3s2;
        private int p3page;
        private const int P3PAGES = 2;
        private TextMeshProUGUI p3pageT;
        private StandardMenuButton p3LA, p3RA;
        private MenuToggle iplT, cplT, beT, deT, discoT, baldiCowardT;
        private TextMeshProUGUI spawnT;
        private int spawnI;
        private MenuToggle inclT;
        private StandardMenuButton spawnLA, spawnRA;
        private GameObject p5s1, p5s2;
        private int p5page;
        private const int P5PAGES = 2;
        private StandardMenuButton p5LA, p5RA;
        private MenuToggle warningT;
        private GameObject cowardLapsCover;
        private bool lastLapsVisual;
        private MenuToggle lightsOutT, mirroredT, gooshoesT, lbTestT;
        private MenuToggle modifiersEnableT;
        private TextMeshProUGUI modifiersModeT;
        private StandardMenuButton modifiersModeLA, modifiersModeRA;
        private AdjustmentBars modifiersRollsB;
        private int modifiersModeI;

        public override void Build()
        {
            CreateButton(OnPP, menuArrowLeft, menuArrowLeftHighlight, "PP", new Vector3(-130f, 50f, 0f));
            pageTitleText = CreateText("PT", "", new Vector3(0f, 50f, 0f), BaldiFonts.ComicSans24,
                TextAlignmentOptions.Center, new Vector2(200f, 32f), Color.black, false);
            CreateButton(OnNP, menuArrowRight, menuArrowRightHighlight, "NP", new Vector3(130f, 50f, 0f));

            p0 = MkC("P0");
            chaosT = MkT(p0, "Enable", KnoxumsChaosModePlugin.IsChaosModeEnabledConfig.Value, 0f);
            MkL(p0, "Chaos Type:", -40f);
            modeLA = MkB(p0, OnML, -105f, -75f, true);
            modeT = MkTxt(p0, -75f);
            modeRA = MkB(p0, OnMR, 105f, -75f, false);
            modeI = Mathf.Clamp((int)KnoxumsChaosModePlugin.SelectedChaosMode.Value, 0, 2);
            UpdMode();

            p1 = MkC("P1");
            evtPT = MkT(p1, "Event Props", KnoxumsChaosModePlugin.IsPropShuffleEnabledConfig.Value, 0f);
            chrPT = MkT(p1, "Char. Props", KnoxumsChaosModePlugin.IsCharPropShuffleEnabledConfig.Value, -30f);
            itmPT = MkT(p1, "Item Props", KnoxumsChaosModePlugin.IsItemPropShuffleEnabledConfig.Value, -60f);
            MkL(p1, "Shuffle Temp.", -95f);
            tempB = CreateBars(delegate { }, "TB", new Vector3(-80f, -125f, 0f), 15);
            tempB.transform.SetParent(p1.transform, false);
            tempB.Adjust(Mathf.Clamp(KnoxumsChaosModePlugin.PropShuffleTemperatureConfig.Value, 1, 15));

            p2 = MkC("P2");
            chrST = MkT(p2, "Char. Sprites", KnoxumsChaosModePlugin.IsCharSpritesShuffleEnabledConfig.Value, 0f);
            itmST = MkT(p2, "Item Sprites", KnoxumsChaosModePlugin.IsItemSpritesShuffleEnabledConfig.Value, -30f);
            strT = MkT(p2, "String Names", KnoxumsChaosModePlugin.IsStringsShuffleEnabledConfig.Value, -60f);
            sndT = MkT(p2, "Sounds", KnoxumsChaosModePlugin.IsSoundsShuffleEnabledConfig.Value, -90f);
            mischiefT = MkT(p2, "Item Mischief", KnoxumsChaosModePlugin.IsItemMischiefEnabledConfig.Value, -120f);

            p3 = MkC("P3");
            p3s1 = new GameObject("P3S1", typeof(RectTransform));
            p3s1.transform.SetParent(p3.transform, false);
            p3s1.transform.localPosition = Vector3.zero;
            iplT = MkT(p3s1, "Items Place", KnoxumsChaosModePlugin.IsItemsPlaceShuffleEnabledConfig.Value, 0f);
            cplT = MkT(p3s1, "Char. Place", KnoxumsChaosModePlugin.IsCharPlaceShuffleEnabledConfig.Value, -30f);
            beT = MkT(p3s1, "Builders Error", KnoxumsChaosModePlugin.IsBuildersErrorEnabledConfig.Value, -60f);
            deT = MkT(p3s1, "Double Events", KnoxumsChaosModePlugin.IsDoubleEventsEnabledConfig.Value, -90f);

            p3s2 = new GameObject("P3S2", typeof(RectTransform));
            p3s2.transform.SetParent(p3.transform, false);
            p3s2.transform.localPosition = Vector3.zero;
            discoT = MkT(p3s2, "Disco Shuffle", KnoxumsChaosModePlugin.IsDiscoShuffleEnabledConfig.Value, 0f);
            baldiCowardT = MkT(p3s2, "Baldi-coward", KnoxumsChaosModePlugin.IsBaldiCowardEnabledConfig.Value, -30f);
            try
            {
                cowardLapsCover = MkCover(baldiCowardT.transform, -65f);
                if (cowardLapsCover != null)
                {
                    StandardMenuButton coverBtn = cowardLapsCover.ConvertToButton<StandardMenuButton>(true);
                    coverBtn.audConfirmOverride = silence;
                    AddTooltip(coverBtn,
                        "Incompatible with the hidden Laps feature. Disable Laps in the config file to use Baldi-coward.");
                    Image cim = cowardLapsCover.GetComponent<Image>();
                    if (cim != null) cim.enabled = true;
                }
                bool savedLapsActive = KnoxumsChaosModePlugin.IsLapsEnabledConfig.Value;
                if (savedLapsActive) SetToggle(baldiCowardT, false);
                lastLapsVisual = savedLapsActive;
                RefreshCowardLapsCover();
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Coward/Laps cover: " + ex.Message); }

            p3LA = CreateButton(OnP3L, menuArrowLeft, menuArrowLeftHighlight, "P3L", new Vector3(-50f, -140f, 0f));
            p3LA.transform.SetParent(p3.transform, false);
            p3pageT = CreateText("P3PT", "", new Vector3(0f, -140f, 0f), BaldiFonts.ComicSans24,
                TextAlignmentOptions.Center, new Vector2(80f, 28f), Color.black, false);
            p3pageT.transform.SetParent(p3.transform, false);
            p3RA = CreateButton(OnP3R, menuArrowRight, menuArrowRightHighlight, "P3R", new Vector3(50f, -140f, 0f));
            p3RA.transform.SetParent(p3.transform, false);
            p3page = 0;
            UpdP3();

            p4 = MkC("P4");
            lightsOutT = MkT(p4, "Lights Out", KnoxumsChaosModePlugin.IsLightsOutEnabledConfig.Value, 0f);
            mirroredT = MkT(p4, "Mirrored", KnoxumsChaosModePlugin.IsMirroredEnabledConfig.Value, -30f);
            gooshoesT = MkT(p4, "53045009", KnoxumsChaosModePlugin.IsGooshoesEnabledConfig.Value, -60f);
            lbTestT = MkT(p4, "LB Test School", KnoxumsChaosModePlugin.IsLbTestSchoolEnabledConfig.Value, -90f);

            pMods = MkC("PMods");
            modifiersEnableT = MkT(pMods, "Enable",
                KnoxumsChaosModePlugin.GameplayModifiersEnabledConfig.Value, 0f);
            MkL(pMods, "Mode:", -40f);
            modifiersModeLA = MkB(pMods, OnModifiersModeL, -110f, -75f, true);
            modifiersModeT = MkTxt(pMods, -75f);
            modifiersModeRA = MkB(pMods, OnModifiersModeR, 110f, -75f, false);
            modifiersModeI = Mathf.Clamp(
                (int)KnoxumsChaosModePlugin.GameplayModifierModeConfig.Value, 0, 1);
            UpdModifiersMode();
            MkL(pMods, "Rolls:", -110f);
            modifiersRollsB = CreateBars(delegate { }, "GameplayModifiersRollsBars",
                new Vector3(-80f, -140f, 0f), 5);
            modifiersRollsB.transform.SetParent(pMods.transform, false);
            modifiersRollsB.Adjust(Mathf.Clamp(
                KnoxumsChaosModePlugin.GameplayModifierRollsConfig.Value, 1, 5));

            p5 = MkC("P5");

            p5s1 = new GameObject("P5S1", typeof(RectTransform));
            p5s1.transform.SetParent(p5.transform, false);
            p5s1.transform.localPosition = Vector3.zero;
            MkL(p5s1, "Spawn new clone at...", 0f);
            spawnLA = MkB(p5s1, OnSL, -105f, -40f, true);
            spawnT = MkTxt(p5s1, -40f);
            spawnRA = MkB(p5s1, OnSR, 105f, -40f, false);
            spawnI = Mathf.Clamp((int)KnoxumsChaosModePlugin.CloneSpawnPointConfig.Value, 0, 1);
            UpdSpawn();
            inclT = MkT(p5s1, "Include Exits", KnoxumsChaosModePlugin.IncludeExitsConfig.Value, -90f);

            p5s2 = new GameObject("P5S2", typeof(RectTransform));
            p5s2.transform.SetParent(p5.transform, false);
            p5s2.transform.localPosition = Vector3.zero;


            warningT = MkT(p5s2, "Warning",
                !KnoxumsChaosModePlugin.DisableWarningConfig.Value, 0f);


            p5LA = CreateButton(OnP5L, menuArrowLeft, menuArrowLeftHighlight,
                "P5L", new Vector3(-150f, -55f, 0f));
            p5LA.transform.SetParent(p5.transform, false);
            p5RA = CreateButton(OnP5R, menuArrowRight, menuArrowRightHighlight,
                "P5R", new Vector3(150f, -55f, 0f));
            p5RA.transform.SetParent(p5.transform, false);
            p5page = 0;
            UpdP5();

            AddTooltip(chaosT, "Allows you to enable chaos mode from BBCR.");
            AddTooltip(modeLA, "Chaos: original BBCR chaos mode.\nChaos+1: triangular clone growth.\nDouble Chaos: characters double on notebook pickup.");
            AddTooltip(modeRA, "Chaos: original BBCR chaos mode.\nChaos+1: triangular clone growth.\nDouble Chaos: characters double on notebook pickup.");
            AddTooltip(evtPT, "Shuffle events' properties.");
            AddTooltip(chrPT, "Shuffle characters' properties.");
            AddTooltip(itmPT, "Shuffle items' properties.");
            AddTooltip(chrST, "Shuffle characters' sprites.");
            AddTooltip(itmST, "Shuffle item sprites.");
            AddTooltip(strT, "Shuffle strings.");
            AddTooltip(sndT, "Shuffle sounds and voices.");
            AddTooltip(mischiefT, "Use a random school item instead.");
            AddTooltip(iplT, "Items swap positions on notebook pickup.");
            AddTooltip(cplT, "Characters swap positions on notebook pickup.");
            AddTooltip(beT, "Cruel but passable school layout.");
            AddTooltip(deT, "Two events can be triggered together.");
            AddTooltip(discoT, "All lights randomly change colors every second.");
            AddTooltip(baldiCowardT, "Baldi runs from you and slows on notebook pickup. You must be caught to leave.");
            AddTooltip(spawnLA, "Choose the clone spawn location.");
            AddTooltip(spawnRA, "Choose the clone spawn location.");
            AddTooltip(inclT, "Characters' clones spawn on broken exit lock.");
            AddTooltip(warningT,
                "Show this mod's photosensitivity warning on startup.");
            AddTooltip(lightsOutT, "Dark school with local lantern lighting.");
            AddTooltip(mirroredT, "Mirror the camera and look controls.");
            AddTooltip(gooshoesT, "USE THESE TO STICK TO THE CEILING!");
            AddTooltip(lbTestT, "School lights pulse like the Lightbulb Testing Room.");
            AddTooltip(modifiersEnableT, "Enable random gameplay modifiers.");
            AddTooltip(modifiersModeLA,
                "Whole Run keeps one set for the run. Floor rerolls before each school floor.");
            AddTooltip(modifiersModeRA,
                "Whole Run keeps one set for the run. Floor rerolls before each school floor.");

            activePage = 0;
            UpdPage();
            StandardMenuButton ab = CreateApplyButton(OnApply);
            if (Singleton<CoreGameManager>.Instance != null)
            {
                MkCover(ab.transform);
                Selectable s = ab.GetComponent<Selectable>();
                if (s != null) s.interactable = false;
                ab.enabled = false;
            }
        }

        private GameObject MkC(string n)
        {
            GameObject g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(base.transform, false);
            g.transform.localPosition = Vector3.zero;
            return g;
        }

        private MenuToggle MkT(GameObject p, string label, bool val, float y)
        {
            MenuToggle t = CreateToggle(label, label, val, new Vector3(0f, y, 0f), 220f);
            t.transform.SetParent(p.transform, false);
            return t;
        }

        private void MkL(GameObject p, string txt, float y)
        {
            TextMeshProUGUI t = CreateText(txt, txt, new Vector3(0f, y, 0f), BaldiFonts.ComicSans24,
                TextAlignmentOptions.Center, new Vector2(300f, 32f), Color.black, false);
            t.transform.SetParent(p.transform, false);
        }

        private TextMeshProUGUI MkTxt(GameObject p, float y)
        {
            TextMeshProUGUI t = CreateText("t", "", new Vector3(0f, y, 0f), BaldiFonts.ComicSans24,
                TextAlignmentOptions.Center, new Vector2(200f, 32f), Color.black, false);
            t.transform.SetParent(p.transform, false);
            return t;
        }

        private StandardMenuButton MkB(GameObject p, UnityAction cb, float x, float y, bool left)
        {
            StandardMenuButton b = CreateButton(cb, left ? menuArrowLeft : menuArrowRight,
                left ? menuArrowLeftHighlight : menuArrowRightHighlight, "b", new Vector3(x, y, 0f));
            b.transform.SetParent(p.transform, false);
            return b;
        }

        private static Sprite coverSpr;
        private GameObject MkCover(Transform t, float extraLeft = 0f)
        {
            if (t == null) return null;
            if (coverSpr == null)
            {
                Texture2D tx = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tx.filterMode = FilterMode.Point;
                tx.wrapMode = TextureWrapMode.Repeat;
                tx.SetPixel(0, 0, new Color(1, 1, 1, .55f));
                tx.SetPixel(1, 1, new Color(1, 1, 1, .55f));
                tx.SetPixel(1, 0, new Color(1, 1, 1, 0));
                tx.SetPixel(0, 1, new Color(1, 1, 1, 0));
                tx.Apply();
                coverSpr = Sprite.Create(tx, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 1f);
            }
            GameObject c = new GameObject("Cov", typeof(RectTransform), typeof(Image));
            c.transform.SetParent(t, false);
            RectTransform r = c.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(extraLeft, 0f);
            r.offsetMax = Vector2.zero;
            Image im = c.GetComponent<Image>();
            im.sprite = coverSpr;
            im.type = Image.Type.Tiled;
            im.raycastTarget = true;
            return c;
        }

        private static bool ToggleVal(MenuToggle t)
        {
            if (t == null) return false;
            try
            {
                FieldInfo vf = typeof(MenuToggle).GetField("val", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return vf != null && (bool)vf.GetValue(t);
            }
            catch { return false; }
        }

        private static void SetToggle(MenuToggle t, bool value)
        {
            if (t == null) return;
            try
            {
                FieldInfo vf = typeof(MenuToggle).GetField("val", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (vf != null) vf.SetValue(t, value);
            }
            catch { }
            try
            {
                FieldInfo cf = typeof(MenuToggle).GetField("checkmark", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                GameObject check = cf != null ? cf.GetValue(t) as GameObject : null;
                if (check != null) check.SetActive(value);
            }
            catch { }
            EnsureToggleClickable(t);
        }

        private static void EnsureToggleClickable(MenuToggle t)
        {
            if (t == null) return;
            try
            {
                FieldInfo hf = typeof(MenuToggle).GetField("hotspot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                GameObject hot = hf != null ? hf.GetValue(t) as GameObject : null;
                if (hot != null)
                {
                    hot.SetActive(true);
                    Image img = hot.GetComponent<Image>();
                    if (img != null) img.enabled = true;
                    StandardMenuButton btn = hot.GetComponent<StandardMenuButton>();
                    if (btn != null) btn.enabled = true;
                }
            }
            catch { }
        }

        private void RefreshCowardLapsCover()
        {

            bool lapsOn = KnoxumsChaosModePlugin.IsLapsEnabledConfig?.Value ?? false;
            if (lapsOn) SetToggle(baldiCowardT, false);
            if (cowardLapsCover != null)
            {
                cowardLapsCover.SetActive(lapsOn);
                cowardLapsCover.transform.SetAsLastSibling();
                Image im = cowardLapsCover.GetComponent<Image>();
                if (im != null) im.enabled = true;
            }
            if (!lapsOn) EnsureToggleClickable(baldiCowardT);
        }

        private void LateUpdate()
        {
            bool lapsVisual = KnoxumsChaosModePlugin.IsLapsEnabledConfig?.Value ?? false;
            if (lapsVisual && !lastLapsVisual) SetToggle(baldiCowardT, false);
            lastLapsVisual = lapsVisual;
            RefreshCowardLapsCover();
        }

        private void OnPP() { activePage = (activePage - 1 + PAGES) % PAGES; UpdPage(); }
        private void OnNP() { activePage = (activePage + 1) % PAGES; UpdPage(); }

        private void UpdPage()
        {
            if (pageTitleText == null) return;
            p0.SetActive(activePage == 0); p1.SetActive(activePage == 1); p2.SetActive(activePage == 2);
            p3.SetActive(activePage == 3); p4.SetActive(activePage == 4);
            pMods.SetActive(activePage == 5); p5.SetActive(activePage == 6);
            pageTitleText.text = new[] { "Chaos Mode", "Props Shuffle", "School Shuffle",
                "Other Chaos", "Fun Settings", "Gameplay Modifiers", "Settings" }[activePage];
        }

        private void OnML() { modeI = (modeI - 1 + 3) % 3; UpdMode(); }
        private void OnMR() { modeI = (modeI + 1) % 3; UpdMode(); }
        private void UpdMode() { modeI = Mathf.Clamp(modeI, 0, 2); if (modeT != null) modeT.text = new[] { "Chaos", "Chaos+1", "Double Chaos" }[modeI]; }
        private void OnModifiersModeL() { modifiersModeI = (modifiersModeI + 1) % 2; UpdModifiersMode(); }
        private void OnModifiersModeR() { modifiersModeI = (modifiersModeI + 1) % 2; UpdModifiersMode(); }
        private void UpdModifiersMode()
        {
            if (modifiersModeT != null)
                modifiersModeT.text = modifiersModeI == 0 ? "Whole Run" : "Floor";
        }
        private void OnSL() { spawnI = (spawnI - 1 + 2) % 2; UpdSpawn(); }
        private void OnSR() { spawnI = (spawnI + 1) % 2; UpdSpawn(); }
        private void UpdSpawn() { spawnI = Mathf.Clamp(spawnI, 0, 1); if (spawnT != null) spawnT.text = spawnI == 0 ? "char. position" : "char. spawn point"; }
        private void OnP3L() { p3page = (p3page - 1 + P3PAGES) % P3PAGES; UpdP3(); }
        private void OnP3R() { p3page = (p3page + 1) % P3PAGES; UpdP3(); }
        private void UpdP3()
        {
            if (p3pageT == null) return;
            p3s1.SetActive(p3page == 0); p3s2.SetActive(p3page == 1); p3pageT.text = (p3page + 1) + "/" + P3PAGES;
        }
        private void OnP5L() { p5page = (p5page - 1 + P5PAGES) % P5PAGES; UpdP5(); }
        private void OnP5R() { p5page = (p5page + 1) % P5PAGES; UpdP5(); }
        private void UpdP5()
        {
            if (p5s1 == null || p5s2 == null) return;
            p5page = Mathf.Clamp(p5page, 0, P5PAGES - 1);
            p5s1.SetActive(p5page == 0);
            p5s2.SetActive(p5page == 1);
        }
        private void OnApply()
        {
            try
            {
                FieldInfo vf = typeof(MenuToggle).GetField("val", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                bool ch = vf != null && (bool)vf.GetValue(chaosT);
                if (ChaosManager.Instance != null) ChaosManager.Instance.SetChaosModeActiveState(ch);
                KnoxumsChaosModePlugin.IsChaosModeEnabledConfig.Value = ch;
                KnoxumsChaosModePlugin.SelectedChaosMode.Value = (ChaosModeType)Mathf.Clamp(modeI, 0, 2);
                if (vf != null)
                {
                    KnoxumsChaosModePlugin.IsPropShuffleEnabledConfig.Value = (bool)vf.GetValue(evtPT);
                    KnoxumsChaosModePlugin.IsCharPropShuffleEnabledConfig.Value = (bool)vf.GetValue(chrPT);
                    KnoxumsChaosModePlugin.IsItemPropShuffleEnabledConfig.Value = (bool)vf.GetValue(itmPT);
                    KnoxumsChaosModePlugin.IsCharSpritesShuffleEnabledConfig.Value = (bool)vf.GetValue(chrST);
                    KnoxumsChaosModePlugin.IsItemSpritesShuffleEnabledConfig.Value = (bool)vf.GetValue(itmST);
                    KnoxumsChaosModePlugin.IsStringsShuffleEnabledConfig.Value = (bool)vf.GetValue(strT);
                    KnoxumsChaosModePlugin.IsSoundsShuffleEnabledConfig.Value = (bool)vf.GetValue(sndT);
                    KnoxumsChaosModePlugin.IsItemMischiefEnabledConfig.Value = (bool)vf.GetValue(mischiefT);
                    KnoxumsChaosModePlugin.IsItemsPlaceShuffleEnabledConfig.Value = (bool)vf.GetValue(iplT);
                    KnoxumsChaosModePlugin.IsCharPlaceShuffleEnabledConfig.Value = (bool)vf.GetValue(cplT);
                    KnoxumsChaosModePlugin.IsBuildersErrorEnabledConfig.Value = (bool)vf.GetValue(beT);
                    KnoxumsChaosModePlugin.IsDoubleEventsEnabledConfig.Value = (bool)vf.GetValue(deT);
                    KnoxumsChaosModePlugin.IsDiscoShuffleEnabledConfig.Value = (bool)vf.GetValue(discoT);
                    bool hiddenLapsOn = KnoxumsChaosModePlugin.IsLapsEnabledConfig.Value;
                    KnoxumsChaosModePlugin.IsBaldiCowardEnabledConfig.Value = hiddenLapsOn
                        ? false : (bool)vf.GetValue(baldiCowardT);
                    KnoxumsChaosModePlugin.IncludeExitsConfig.Value = (bool)vf.GetValue(inclT);
                    KnoxumsChaosModePlugin.IsLightsOutEnabledConfig.Value = (bool)vf.GetValue(lightsOutT);
                    KnoxumsChaosModePlugin.IsMirroredEnabledConfig.Value = (bool)vf.GetValue(mirroredT);
                    KnoxumsChaosModePlugin.IsGooshoesEnabledConfig.Value = (bool)vf.GetValue(gooshoesT);
                    KnoxumsChaosModePlugin.IsLbTestSchoolEnabledConfig.Value = (bool)vf.GetValue(lbTestT);
                    KnoxumsChaosModePlugin.DisableWarningConfig.Value = !(bool)vf.GetValue(warningT);
                    KnoxumsChaosModePlugin.GameplayModifiersEnabledConfig.Value =
                        (bool)vf.GetValue(modifiersEnableT);
                }
                KnoxumsChaosModePlugin.PropShuffleTemperatureConfig.Value = Mathf.Clamp(tempB.GetRaw(), 1, 15);
                KnoxumsChaosModePlugin.CloneSpawnPointConfig.Value = (CloneSpawnPoint)Mathf.Clamp(spawnI, 0, 1);
                KnoxumsChaosModePlugin.GameplayModifierModeConfig.Value =
                    (GameplayModifierMode)Mathf.Clamp(modifiersModeI, 0, 1);
                KnoxumsChaosModePlugin.GameplayModifierRollsConfig.Value =
                    Mathf.Clamp(modifiersRollsB.GetRaw(), 1, 5);
                KnoxumsChaosModePlugin.Instance.Config.Save();
                GameplayModifierManager.Instance?.OnSettingsChanged();
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Apply: " + ex.Message); }
        }
    }


    public class FunMirrorMode : MonoBehaviour
    {
        private readonly Camera[] cameraToMirror = new Camera[2];
        private bool active;
        private bool playerReversed;
        private bool audioReversed;
        public bool Ready => active;

        public void Initialize()
        {
            FunLookInvertPatch.TryHookGameCamera();
            if (active || !GrabCameras()) return;
            active = true;
            ApplyProjection();
            RenderPipelineManager.beginCameraRendering += ReverseCulling;
            RenderPipelineManager.endCameraRendering += ReturnCulling;
            Camera.onPreRender += PreRender;
            Camera.onPostRender += PostRender;
            try { Singleton<SubtitleManager>.Instance.Reverse(); } catch { }
            audioReversed = InvokeGameCamera("ReverseAudio");
            playerReversed = TryInvokeReverse(Singleton<CoreGameManager>.Instance?.GetPlayer(0));
            FunLookInvertPatch.NeedPlusLook = !playerReversed;
        }

        private bool GrabCameras()
        {
            try
            {
                CoreGameManager gc = Singleton<CoreGameManager>.Instance;
                GameCamera cam = gc != null ? gc.GetCamera(0) : null;
                if (cam == null || cam.camCom == null) return false;
                cameraToMirror[0] = cam.camCom;
                cameraToMirror[1] = cam.billboardCam;
                return true;
            }
            catch { return false; }
        }

        private void ApplyProjection() { for (int i = 0; i < cameraToMirror.Length; i++) PushFlip(cameraToMirror[i]); }
        private static void PushFlip(Camera camera)
        {
            if (camera == null) return;
            try { camera.ResetProjectionMatrix(); camera.projectionMatrix *= Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)); }
            catch { }
        }
        private static bool TryInvokeReverse(object obj)
        {
            if (obj == null) return false;
            try
            {
                MethodInfo mi = obj.GetType().GetMethod("Reverse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (mi == null) return false;
                mi.Invoke(obj, null); return true;
            }
            catch { return false; }
        }
        private static bool InvokeGameCamera(string method)
        {
            try
            {
                object cam = Singleton<CoreGameManager>.Instance?.GetCamera(0);
                if (cam == null) return false;
                MethodInfo mi = cam.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (mi == null) return false;
                mi.Invoke(cam, null); return true;
            }
            catch { return false; }
        }
        private bool IsOurs(Camera camera) { return camera != null && (camera == cameraToMirror[0] || camera == cameraToMirror[1]); }

        private void OnDisable()
        {
            FunLookInvertPatch.NeedPlusLook = false;
            if (!active) return;
            active = false;
            RenderPipelineManager.beginCameraRendering -= ReverseCulling;
            RenderPipelineManager.endCameraRendering -= ReturnCulling;
            Camera.onPreRender -= PreRender;
            Camera.onPostRender -= PostRender;
            try { Singleton<SubtitleManager>.Instance.Reverse(); } catch { }
            if (audioReversed) { InvokeGameCamera("ReverseAudio"); audioReversed = false; }
            if (playerReversed)
            {
                TryInvokeReverse(Singleton<CoreGameManager>.Instance?.GetPlayer(0));
                playerReversed = false;
            }
            for (int i = 0; i < cameraToMirror.Length; i++)
                if (cameraToMirror[i] != null) cameraToMirror[i].ResetProjectionMatrix();
            try { GL.invertCulling = false; } catch { }
        }

        public void ReverseCulling(ScriptableRenderContext context, Camera camera)
        { if (IsOurs(camera)) { PushFlip(camera); GL.invertCulling = true; } }
        public void ReturnCulling(ScriptableRenderContext context, Camera camera)
        { if (IsOurs(camera)) GL.invertCulling = false; }
        private void PreRender(Camera camera) { if (IsOurs(camera)) { PushFlip(camera); GL.invertCulling = true; } }
        private void PostRender(Camera camera) { if (IsOurs(camera)) GL.invertCulling = false; }
    }

    [DefaultExecutionOrder(32000)]
    public class FunCameraFlip : MonoBehaviour
    {
        private readonly Camera[] cams = new Camera[2];
        private bool hooked;
        private bool gooshoesOffset;
        private bool mirrorOn;
        private bool gooshoesOn;
        private bool mirrorEffectsOn;
        private bool playerReversed;
        private bool subtitleReversed;
        private bool audioReversed;
        private bool mapFlipped;
        public bool HasCameras => cams[0] != null || cams[1] != null;

        public void Apply(bool mirror, bool gooshoes)
        {
            mirrorOn = mirror;
            gooshoesOn = gooshoes;
            GrabCameras();
            ApplyMirrorEffects(mirror);
            PushMatrix();
            ApplyGooshoesOffset(gooshoes);
            ApplyEntityFlip(gooshoes);
            if ((mirror || gooshoes) && !hooked)
            {
                hooked = true;
                try { RenderPipelineManager.beginCameraRendering += ReverseCulling; } catch { }
                try { RenderPipelineManager.endCameraRendering += ReturnCulling; } catch { }
                Camera.onPreRender += PreRender;
                Camera.onPostRender += PostRender;
            }
            if (!mirror && !gooshoes) Shutdown();
        }

        private void LateUpdate()
        {
            if (ChaosManager.Instance != null && !ChaosManager.Instance.IsLevelReady) return;
            if (mirrorOn || gooshoesOn) { GrabCameras(); PushMatrix(); }
        }

        private void PushMatrix()
        {
            float sx = mirrorOn ? -1f : 1f;
            float sy = gooshoesOn ? -1f : 1f;
            for (int i = 0; i < cams.Length; i++) PushMatrixOn(cams[i], sx, sy);
        }

        private static void PushMatrixOn(Camera cam, float sx, float sy)
        {
            if (cam == null) return;
            try
            {
                cam.ResetProjectionMatrix();
                if (sx < 0f || sy < 0f) cam.projectionMatrix *= Matrix4x4.Scale(new Vector3(sx, sy, 1f));
            }
            catch { }
        }

        private void GrabCameras()
        {
            try
            {
                CoreGameManager gc = Singleton<CoreGameManager>.Instance;
                GameCamera cam = gc != null ? gc.GetCamera(0) : null;
                if (cam != null) { cams[0] = cam.camCom; cams[1] = cam.billboardCam; }
            }
            catch { }
        }

        private static bool TryInvokeNoArg(object obj, string name)
        {
            if (obj == null) return false;
            try
            {
                MethodInfo mi = obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (mi == null) return false;
                mi.Invoke(obj, null); return true;
            }
            catch { return false; }
        }

        private void ApplyMirrorEffects(bool on)
        {
            if (on == mirrorEffectsOn) return;
            mirrorEffectsOn = on;
            if (on)
            {
                try { Singleton<SubtitleManager>.Instance.Reverse(); subtitleReversed = true; } catch { }
                try { audioReversed = TryInvokeNoArg(Singleton<CoreGameManager>.Instance?.GetCamera(0), "ReverseAudio"); } catch { }
                playerReversed = TryInvokeNoArg(Singleton<CoreGameManager>.Instance?.GetPlayer(0), "Reverse");
                FunLookInvertPatch.NeedPlusLook = !playerReversed;
            }
            else
            {
                FunLookInvertPatch.NeedPlusLook = false;
                if (subtitleReversed) { try { Singleton<SubtitleManager>.Instance.Reverse(); } catch { } subtitleReversed = false; }
                if (audioReversed) { TryInvokeNoArg(Singleton<CoreGameManager>.Instance?.GetCamera(0), "ReverseAudio"); audioReversed = false; }
                if (playerReversed) { TryInvokeNoArg(Singleton<CoreGameManager>.Instance?.GetPlayer(0), "Reverse"); playerReversed = false; }
            }
        }

        private void ApplyEntityFlip(bool on)
        {
            if (on == mapFlipped) return;
            bool ok = false;
            try
            {
                PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                Entity ent = pm != null && pm.plm != null ? pm.plm.Entity : null;
                if (ent == null && pm != null) ent = R.Get<Entity>(pm, "entity", null);
                ok = TryInvokeNoArg(ent, "Flip");
            }
            catch { }
            mapFlipped = on;
            if (!ok) KnoxumsChaosModePlugin.Log.LogWarning("53045009: Entity.Flip() not found");
        }

        private void ApplyGooshoesOffset(bool on)
        {
            if (on == gooshoesOffset) return;
            Transform t = null;
            try
            {
                GameCamera cam = Singleton<CoreGameManager>.Instance?.GetCamera(0);
                if (cam != null) t = ((Component)cam).transform;
            }
            catch { }
            if (t == null && cams[0] != null) t = cams[0].transform;
            if (t == null) return;
            Vector3 p = t.localPosition;
            p.y += on ? -1f : 1f;
            t.localPosition = p;
            gooshoesOffset = on;
        }

        public void Shutdown()
        {
            if (hooked)
            {
                hooked = false;
                try { RenderPipelineManager.beginCameraRendering -= ReverseCulling; } catch { }
                try { RenderPipelineManager.endCameraRendering -= ReturnCulling; } catch { }
                Camera.onPreRender -= PreRender;
                Camera.onPostRender -= PostRender;
            }
            ApplyMirrorEffects(false);
            ApplyGooshoesOffset(false);
            ApplyEntityFlip(false);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null) try { cams[i].ResetProjectionMatrix(); } catch { }
                cams[i] = null;
            }
            mirrorOn = false;
            gooshoesOn = false;
            try { GL.invertCulling = false; } catch { }
        }

        private void OnDisable() { Shutdown(); }
        private bool IsOurs(Camera camera) { return camera != null && (camera == cams[0] || camera == cams[1]); }
        private void ReverseCulling(ScriptableRenderContext ctx, Camera camera)
        { if (IsOurs(camera)) { PushMatrixOn(camera, mirrorOn ? -1f : 1f, gooshoesOn ? -1f : 1f); GL.invertCulling = true; } }
        private void ReturnCulling(ScriptableRenderContext ctx, Camera camera) { if (IsOurs(camera)) GL.invertCulling = false; }
        private void PreRender(Camera camera)
        { if (IsOurs(camera)) { PushMatrixOn(camera, mirrorOn ? -1f : 1f, gooshoesOn ? -1f : 1f); GL.invertCulling = true; } }
        private void PostRender(Camera camera) { if (IsOurs(camera)) GL.invertCulling = false; }
    }

    public class FunLanternMode : MonoBehaviour
    {
        private static readonly Color PlayerColor = new Color(0xE2 / 255f, 0xC3 / 255f, 0x7F / 255f, 1f);
        private const float PlayerStrength = 6f;
        private const float PrincipalStrength = 4f;


        private const float VoidFogStart = 42f;
        private const float VoidFogMax = 58f;

        private EnvironmentController ec;
        private readonly List<FunLanternSource> sources = new List<FunLanternSource>();
        private readonly List<IntVector2> prevLit = new List<IntVector2>();
        private readonly Dictionary<Light, bool> savedLightStates = new Dictionary<Light, bool>();
        private Color _color;
        private Vector3 _position;
        private float refreshNpc;
        private Color savedDark = Color.black;
        private bool savedDarkOk;
        private Fog voidFog;
        private bool voidReady;
        private bool darknessInitialized;

        private Camera savedMainCam, savedBillboardCam;
        private CameraClearFlags savedMainFlags, savedBillboardFlags;
        private Color savedMainBackground, savedBillboardBackground;
        private bool savedMainCamera, savedBillboardCamera;
        private Color savedSkyboxColor, savedFogColor;
        private float savedFogStart, savedFogMax, savedFogStrength;
        private int savedFogActive;
        private bool shaderStateSaved;

        private class FunLanternSource
        {
            public Transform transform;
            public float strength;
            public Color color;
        }

        public void Initialize(EnvironmentController environment)
        {
            if (ec != null && ec != environment) Shutdown();
            ec = environment;
            if (ec != null && !savedDarkOk)
            {
                try { savedDark = ec.standardDarkLevel; savedDarkOk = true; ec.standardDarkLevel = Color.black; } catch { }
            }
            if (ec != null) ec.lightingOverride = true;
            CaptureRenderState();
            ApplyVoidLook();
            KillSchoolLightFixtures();
            RebuildSources();
        }

        private void CaptureRenderState()
        {
            if (!shaderStateSaved)
            {
                shaderStateSaved = true;
                try { savedSkyboxColor = Shader.GetGlobalColor("_SkyboxColor"); } catch { }
                try { savedFogColor = Shader.GetGlobalColor("_FogColor"); } catch { }
                try { savedFogStart = Shader.GetGlobalFloat("_FogStartDistance"); } catch { }
                try { savedFogMax = Shader.GetGlobalFloat("_FogMaxDistance"); } catch { }
                try { savedFogStrength = Shader.GetGlobalFloat("_FogStrength"); } catch { }
                try { savedFogActive = Shader.GetGlobalInt("_FogActive"); } catch { }
            }
            try
            {
                GameCamera cam = Singleton<CoreGameManager>.Instance?.GetCamera(0);
                if (cam != null && cam.camCom != null && !savedMainCamera)
                {
                    savedMainCam = cam.camCom; savedMainFlags = cam.camCom.clearFlags;
                    savedMainBackground = cam.camCom.backgroundColor; savedMainCamera = true;
                }
                if (cam != null && cam.billboardCam != null && !savedBillboardCamera)
                {
                    savedBillboardCam = cam.billboardCam; savedBillboardFlags = cam.billboardCam.clearFlags;
                    savedBillboardBackground = cam.billboardCam.backgroundColor; savedBillboardCamera = true;
                }
            }
            catch { }
        }

        private void ApplyVoidLook()
        {
            if (!voidReady)
            {
                voidReady = true;
                try
                {
                    voidFog = new Fog { color = Color.black, startDist = VoidFogStart, maxDist = VoidFogMax, strength = 1f, priority = 999 };
                    if (ec != null) ec.AddFog(voidFog);
                }
                catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogWarning("Lights Out void fog: " + ex.Message); }
            }
            PushVoidShaders();
        }

        private static void PushVoidShaders()
        {
            try { Shader.SetGlobalColor("_SkyboxColor", Color.black); } catch { }
            try { Shader.SetGlobalColor("_FogColor", Color.black); } catch { }
            try { Shader.SetGlobalFloat("_FogStartDistance", VoidFogStart); } catch { }
            try { Shader.SetGlobalFloat("_FogMaxDistance", VoidFogMax); } catch { }
            try { Shader.SetGlobalFloat("_FogStrength", 1f); } catch { }
            try { Shader.SetGlobalInt("_FogActive", 1); } catch { }
            try
            {
                GameCamera cam = Singleton<CoreGameManager>.Instance?.GetCamera(0);
                if (cam != null && cam.camCom != null)
                { cam.camCom.clearFlags = CameraClearFlags.SolidColor; cam.camCom.backgroundColor = Color.black; }
                if (cam != null && cam.billboardCam != null)
                { cam.billboardCam.clearFlags = CameraClearFlags.SolidColor; cam.billboardCam.backgroundColor = Color.black; }
            }
            catch { }
        }

        private void RebuildSources()
        {
            sources.Clear();
            try
            {
                PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (pm != null) AddSource(pm.transform, PlayerStrength, PlayerColor);
            }
            catch { }
            try
            {
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                List<NPC> npcs = bgm != null && bgm.Ec != null ? bgm.Ec.Npcs : null;
                if (npcs != null)
                {
                    for (int i = 0; i < npcs.Count; i++)
                    {
                        NPC n = npcs[i];
                        if (n == null) continue;
                        bool principal = false;
                        try { principal = n.Character == Character.Principal; } catch { }
                        if (!principal)
                        {
                            string nm = n.Character.ToString();
                            principal = nm != null && nm.IndexOf("Principal", StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        if (principal) AddSource(n.transform, PrincipalStrength, Color.white);
                    }
                }
            }
            catch { }
            KillSchoolLightFixtures();
        }

        private void KillSchoolLightFixtures()
        {
            if (ec == null) return;
            CoreGameManager cgm = null;
            try { cgm = Singleton<CoreGameManager>.Instance; } catch { }
            HashSet<Cell> seen = new HashSet<Cell>();
            try { if (ec.lights != null) foreach (Cell c in ec.lights) if (c != null && seen.Add(c)) KillOneSchoolLight(c, cgm); } catch { }
            try { if (ec.hallLights != null) foreach (Cell c in ec.hallLights) if (c != null && seen.Add(c)) KillOneSchoolLight(c, cgm); } catch { }
            try
            {
                if (ec.rooms != null)
                    foreach (RoomController room in ec.rooms)
                        if (room != null && room.lights != null)
                            foreach (Cell c in room.lights) if (c != null && seen.Add(c)) KillOneSchoolLight(c, cgm);
            }
            catch { }
        }

        private void KillOneSchoolLight(Cell c, CoreGameManager cgm)
        {
            if (c == null) return;
            try { c.lightColor = Color.black; c.SetLight(false); } catch { }
            try { if (cgm != null) cgm.UpdateLighting(Color.black, c.position); } catch { }
            try
            {
                if (c.TileTransform == null) return;
                Light[] ls = c.TileTransform.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < ls.Length; i++)
                {
                    Light l = ls[i]; if (l == null) continue;
                    if (!savedLightStates.ContainsKey(l)) savedLightStates[l] = l.enabled;
                    l.enabled = false;
                }
            }
            catch { }
        }

        private void AddSource(Transform tr, float strength, Color color)
        {
            if (tr == null) return;
            for (int i = 0; i < sources.Count; i++) if (sources[i].transform == tr) return;
            sources.Add(new FunLanternSource { transform = tr, strength = strength, color = color });
        }

        private void BlackoutWholeGrid(CoreGameManager cgm)
        {
            if (darknessInitialized || ec == null || cgm == null) return;
            if (ec.levelSize.x <= 0 || ec.levelSize.z <= 0) return;
            try
            {
                for (int x = 0; x < ec.levelSize.x; x++)
                    for (int z = 0; z < ec.levelSize.z; z++)
                    {
                        IntVector2 position;
                        position.x = x;
                        position.z = z;
                        cgm.UpdateLighting(Color.black, position);
                    }
                ec.UpdateQueuedLightChanges();
                darknessInitialized = true;
            }
            catch { }
        }

        private void Update()
        {
            if (ec == null) return;
            refreshNpc -= Time.unscaledDeltaTime;
            if (refreshNpc <= 0f) { refreshNpc = 1.5f; RebuildSources(); PushVoidShaders(); }
            if (ec.levelSize.x <= 0 || ec.levelSize.z <= 0) return;
            CoreGameManager cgm = null;
            try { cgm = Singleton<CoreGameManager>.Instance; } catch { }
            if (cgm == null) return;
            BlackoutWholeGrid(cgm);

            List<IntVector2> now = new List<IntVector2>();
            HashSet<int> keys = new HashSet<int>();
            for (int k = 0; k < sources.Count; k++)
            {
                FunLanternSource src = sources[k];
                if (src.transform == null) { sources.RemoveAt(k--); continue; }
                Vector3 sp = src.transform.position;
                int cx = Mathf.RoundToInt((sp.x / 10f) - 0.5f);
                int cz = Mathf.RoundToInt((sp.z / 10f) - 0.5f);
                int r = Mathf.CeilToInt(src.strength) + 1;
                int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(ec.levelSize.x - 1, cx + r);
                int z0 = Mathf.Max(0, cz - r), z1 = Mathf.Min(ec.levelSize.z - 1, cz + r);
                for (int ix = x0; ix <= x1; ix++)
                    for (int iz = z0; iz <= z1; iz++)
                    {
                        _position = new Vector3(ix * 10f + 5f, 5f, iz * 10f + 5f);
                        if (Vector3.Distance(sp, _position) / 10f >= src.strength) continue;
                        int key = ix + iz * ec.levelSize.x;
                        if (keys.Add(key)) { IntVector2 cell; cell.x = ix; cell.z = iz; now.Add(cell); }
                    }
            }

            for (int n = 0; n < prevLit.Count; n++)
            {
                IntVector2 prev = prevLit[n];
                int key = prev.x + prev.z * ec.levelSize.x;
                if (!keys.Contains(key)) try { cgm.UpdateLighting(Color.black, prev); } catch { }
            }

            for (int n = 0; n < now.Count; n++)
            {
                IntVector2 cell = now[n];
                _color = Color.black;
                _position = new Vector3(cell.x * 10f + 5f, 5f, cell.z * 10f + 5f);
                for (int k = 0; k < sources.Count; k++)
                {
                    FunLanternSource src = sources[k];
                    if (src.transform == null) continue;
                    float dist = Vector3.Distance(src.transform.position, _position) / 10f;
                    if (dist >= src.strength) continue;
                    float fall = 1f - dist / src.strength; fall *= fall;
                    _color += src.color * fall * (Color.white - _color);
                }
                try { cgm.UpdateLighting(_color, cell); } catch { }
            }
            prevLit.Clear(); prevLit.AddRange(now);
        }

        public void Shutdown()
        {
            if (ec != null)
            {
                try { ec.lightingOverride = false; } catch { }
                if (voidFog != null) { try { ec.RemoveFog(voidFog); } catch { } voidFog = null; }
                if (savedDarkOk) { try { ec.standardDarkLevel = savedDark; } catch { } savedDarkOk = false; }
            }
            foreach (KeyValuePair<Light, bool> kv in savedLightStates)
                if (kv.Key != null) try { kv.Key.enabled = kv.Value; } catch { }
            savedLightStates.Clear();
            try
            {
                if (savedMainCamera && savedMainCam != null)
                { savedMainCam.clearFlags = savedMainFlags; savedMainCam.backgroundColor = savedMainBackground; }
                if (savedBillboardCamera && savedBillboardCam != null)
                { savedBillboardCam.clearFlags = savedBillboardFlags; savedBillboardCam.backgroundColor = savedBillboardBackground; }
            }
            catch { }
            if (shaderStateSaved)
            {
                try { Shader.SetGlobalColor("_SkyboxColor", savedSkyboxColor); } catch { }
                try { Shader.SetGlobalColor("_FogColor", savedFogColor); } catch { }
                try { Shader.SetGlobalFloat("_FogStartDistance", savedFogStart); } catch { }
                try { Shader.SetGlobalFloat("_FogMaxDistance", savedFogMax); } catch { }
                try { Shader.SetGlobalFloat("_FogStrength", savedFogStrength); } catch { }
                try { Shader.SetGlobalInt("_FogActive", savedFogActive); } catch { }
            }
            try
            {
                if (ec != null && ec.lights != null)
                    for (int i = 0; i < ec.lights.Count; i++) if (ec.lights[i] != null) ec.UpdateLightingAtCell(ec.lights[i]);
                if (ec != null) { ec.UpdateQueuedLightChanges(); ec.UpdateFog(); }
            }
            catch { }
            ec = null;
            voidReady = false;
            darknessInitialized = false;
            shaderStateSaved = false;
            savedMainCamera = savedBillboardCamera = false;
            savedMainCam = savedBillboardCam = null;
            sources.Clear(); prevLit.Clear();
        }

        private void OnDisable() { Shutdown(); }
    }

    public class SpriteShuffler : MonoBehaviour
    {
        private SpriteRenderer[] rr;
        private Sprite[] originalSprites;
        private NPC npc;

        private void Start()
        {
            rr = GetComponentsInChildren<SpriteRenderer>(true);
            originalSprites = new Sprite[rr.Length];
            for (int i = 0; i < rr.Length; i++) originalSprites[i] = rr[i] != null ? rr[i].sprite : null;
            npc = GetComponent<NPC>() ?? GetComponentInParent<NPC>();
            if (npc != null) ChaosManager.Instance?.EnsureInstanceVisual(npc.GetInstanceID(), npc.Character);
        }

        private void LateUpdate()
        {
            if (ChaosManager.Instance == null || !ChaosManager.Instance.IsCharacterSpritesShuffleActive
                || !ChaosManager.Instance.IsLevelReady || npc == null || rr == null) return;

            for (int i = 0; i < rr.Length; i++)
                if (rr[i] != null && originalSprites[i] != null)
                    rr[i].sprite = ChaosManager.Instance.GetShuffledCharacterSprite(originalSprites[i], npc);
        }
    }


    public class GameplayModifierManager : MonoBehaviour
    {
        public static GameplayModifierManager Instance { get; private set; }
        private static Sprite optionsClipboardSprite;
        private static TMP_FontAsset optionsClipboardFont;

        private readonly List<GameplayModifierId> activeRolls =
            new List<GameplayModifierId>();
        private readonly Dictionary<GameplayModifierId, int> stacks =
            new Dictionary<GameplayModifierId, int>();
        private System.Random random;
        private bool runSetCreated;
        private string selectedFloorKey = "";
        private bool revealPending;
        private bool beginPlayReached;
        private GameplayModifierMode selectedMode;
        private int selectedRollCount;
        private Coroutine revealRoutine;
        private GameObject revealObject;
        private GameObject pauseObject;
        private Transform pauseParent;
        private float pauseRefresh;
        private int resultsElevatorScreenId;

        public IReadOnlyList<GameplayModifierId> ActiveRolls => activeRolls;
        public bool Enabled =>
            KnoxumsChaosModePlugin.GameplayModifiersEnabledConfig?.Value ?? false;

        public static void CaptureOptionsClipboardVisual(OptionsMenu menu)
        {
            if (menu == null) return;
            try
            {
                TMP_Text[] labels = menu.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    TMP_Text label = labels[i];
                    if (label == null || label.font == null) continue;
                    string path = TransformPath(label.transform).ToLowerInvariant();
                    if (path.Contains("optionsclipboard") || path.Contains("clipboard"))
                    {
                        optionsClipboardFont = label.font;
                        break;
                    }
                }

                Transform exactRoot = menu.transform;
                while (exactRoot != null)
                {
                    if (exactRoot.name.Equals("OptionsClipboard",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Image rootImage = exactRoot.GetComponent<Image>();
                        if (rootImage != null && rootImage.sprite != null)
                        { optionsClipboardSprite = rootImage.sprite; return; }
                        SpriteRenderer rootRenderer = exactRoot.GetComponent<SpriteRenderer>();
                        if (rootRenderer != null && rootRenderer.sprite != null)
                        { optionsClipboardSprite = rootRenderer.sprite; return; }
                    }
                    exactRoot = exactRoot.parent;
                }

                Image best = null;
                float bestScore = float.MinValue;
                Image[] images = menu.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];
                    if (image == null || image.sprite == null) continue;
                    string path = TransformPath(image.transform).ToLowerInvariant();
                    string spriteName = image.sprite.name.ToLowerInvariant();
                    bool clipboard = path.Contains("optionsclipboard")
                        || spriteName.Contains("optionsclipboard")
                        || path.Contains("clipboard");
                    if (!clipboard) continue;
                    float score = image.sprite.rect.width * image.sprite.rect.height;
                    if (path.Contains("optionsclipboard")) score += 10000000f;
                    if (spriteName.Contains("optionsclipboard")) score += 20000000f;
                    if (path.Contains("clipboard")) score += 5000000f;
                    if (score > bestScore) { bestScore = score; best = image; }
                }
                if (best != null) optionsClipboardSprite = best.sprite;

                if (optionsClipboardSprite == null)
                {
                    SpriteRenderer[] renderers = menu.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        SpriteRenderer renderer = renderers[i];
                        if (renderer == null || renderer.sprite == null) continue;
                        string path = TransformPath(renderer.transform).ToLowerInvariant();
                        string spriteName = renderer.sprite.name.ToLowerInvariant();
                        if (path.Contains("optionsclipboard")
                            || spriteName.Contains("optionsclipboard")
                            || path.Contains("clipboard"))
                        { optionsClipboardSprite = renderer.sprite; break; }
                    }
                }

                if (optionsClipboardSprite == null)
                {
                    Transform cursor = menu.transform;
                    while (cursor != null)
                    {
                        if (cursor.name.IndexOf("OptionsClipboard",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Image image = cursor.GetComponent<Image>()
                                ?? cursor.GetComponentInChildren<Image>(true);
                            if (image != null && image.sprite != null)
                                optionsClipboardSprite = image.sprite;
                            break;
                        }
                        cursor = cursor.parent;
                    }
                }
            }
            catch { }
        }

        private static string TransformPath(Transform transform)
        {
            string path = "";
            for (int i = 0; i < 10 && transform != null; i++)
            {
                path = transform.name + "/" + path;
                transform = transform.parent;
            }
            return path;
        }

        private static Sprite ResolveOptionsClipboardSprite()
        {
            if (optionsClipboardSprite != null) return optionsClipboardSprite;
            try
            {
                Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite sprite = sprites[i];
                    if (sprite == null) continue;
                    string name = sprite.name.ToLowerInvariant();
                    if (name == "optionsclipboard" || name.Contains("optionsclipboard"))
                    { optionsClipboardSprite = sprite; return sprite; }
                }

                GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objects.Length; i++)
                {
                    GameObject gameObject = objects[i];
                    if (gameObject == null || gameObject.name.IndexOf(
                        "OptionsClipboard", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Image image = gameObject.GetComponent<Image>()
                        ?? gameObject.GetComponentInChildren<Image>(true);
                    if (image != null && image.sprite != null)
                    { optionsClipboardSprite = image.sprite; return image.sprite; }
                    SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>()
                        ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer != null && renderer.sprite != null)
                    { optionsClipboardSprite = renderer.sprite; return renderer.sprite; }
                }
            }
            catch { }
            return null;
        }

        private void Awake()
        {
            Instance = this;
            random = new System.Random(Environment.TickCount ^ GetInstanceID());
            selectedMode = KnoxumsChaosModePlugin.GameplayModifierModeConfig?.Value
                ?? GameplayModifierMode.WholeRun;
            selectedRollCount = Mathf.Clamp(
                KnoxumsChaosModePlugin.GameplayModifierRollsConfig?.Value ?? 3, 1, 5);
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            StopReveal();
            DestroyPauseDisplay();
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene,
            UnityEngine.SceneManagement.Scene newScene)
        {
            DestroyPauseDisplay();
            if (LooksLikeMenuScene(newScene.name)) ResetRun();
        }

        private static bool LooksLikeMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            string name = sceneName.ToLowerInvariant();
            return name.Contains("mainmenu") || name == "menu"
                || name.Contains("title") || name.Contains("warning");
        }

        public void OnSettingsChanged()
        {
            GameplayModifierMode mode =
                KnoxumsChaosModePlugin.GameplayModifierModeConfig?.Value
                ?? GameplayModifierMode.WholeRun;
            int rolls = Mathf.Clamp(
                KnoxumsChaosModePlugin.GameplayModifierRollsConfig?.Value ?? 3, 1, 5);
            if (!Enabled || mode != selectedMode || rolls != selectedRollCount)
                ResetRun();
            selectedMode = mode;
            selectedRollCount = rolls;
        }

        public void ResetRun()
        {
            activeRolls.Clear();
            stacks.Clear();
            runSetCreated = false;
            selectedFloorKey = "";
            revealPending = false;
            beginPlayReached = false;
            resultsElevatorScreenId = 0;
            StopReveal();
            DestroyPauseDisplay();
        }

        public int GetStacks(GameplayModifierId id)
        {
            return stacks.TryGetValue(id, out int count) ? count : 0;
        }

        public bool Has(GameplayModifierId id) { return GetStacks(id) > 0; }

        public void OnElevatorScreenStarted(ElevatorScreen screen)
        {
            if (!Enabled || screen == null) return;
            if (resultsElevatorScreenId == screen.GetInstanceID()) return;
            EnsureSetForCurrentFloor();
            if (activeRolls.Count == 0) return;
            revealPending = true;
            beginPlayReached = false;
            StopReveal();
            Transform parent = null;
            try { parent = screen.Canvas != null ? screen.Canvas.transform : null; }
            catch { }
            StartElevatorScreenReveal(parent, screen.transform);
        }

        public void PrepareForGeneration(LevelBuilder builder)
        {
            if (!Enabled)
            {
                if (activeRolls.Count > 0) ResetRun();
                return;
            }
            if (IsPitstopScene()) return;

            EnsureSetForCurrentFloor();
            revealPending = activeRolls.Count > 0;
            resultsElevatorScreenId = 0;
            if (revealPending && revealRoutine == null && revealObject == null)
            {
                ElevatorScreen screen = Singleton<ElevatorScreen>.Instance;
                Transform parent = null;
                try { parent = screen != null && screen.Canvas != null
                    ? screen.Canvas.transform : null; }
                catch { }
                StartElevatorScreenReveal(parent,
                    screen != null ? screen.transform : null);
            }
        }

        private void StartElevatorScreenReveal(Transform preferredParent = null,
            Transform preferredMarker = null)
        {
            if (revealRoutine != null || revealObject != null
                || activeRolls.Count == 0) return;
            try
            {
                KnoxumsChaosModePlugin.Log.LogInfo(
                    "Gameplay Modifiers Elevator Screen reveal started with "
                    + activeRolls.Count + " rolls.");
            }
            catch { }
            revealRoutine = StartCoroutine(RevealRoutine(preferredParent,
                preferredMarker));
        }

        private void EnsureSetForCurrentFloor()
        {
            selectedMode = KnoxumsChaosModePlugin.GameplayModifierModeConfig?.Value
                ?? GameplayModifierMode.WholeRun;
            selectedRollCount = Mathf.Clamp(
                KnoxumsChaosModePlugin.GameplayModifierRollsConfig?.Value ?? 3, 1, 5);
            string floorKey = BuildFloorKey();
            bool needRoll = activeRolls.Count == 0
                || (selectedMode == GameplayModifierMode.WholeRun
                    ? !runSetCreated
                    : !string.Equals(selectedFloorKey, floorKey,
                        StringComparison.Ordinal));

            if (!needRoll) return;
            RollSet(selectedRollCount);
            runSetCreated = true;
            selectedFloorKey = floorKey;
        }

        private void RollSet(int count)
        {
            activeRolls.Clear();
            stacks.Clear();
            GameplayModifierId[] all = GameplayModifierCatalog.All;
            for (int slot = 0; slot < count && all.Length > 0; slot++)
            {
                GameplayModifierId pick = all[0];
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    pick = all[random.Next(0, all.Length)];
                    if (GetStacks(pick) < 3) break;
                }
                if (GetStacks(pick) >= 3) continue;
                activeRolls.Add(pick);
                stacks[pick] = GetStacks(pick) + 1;
            }

            try
            {
                KnoxumsChaosModePlugin.Log.LogInfo(
                    "Gameplay Modifiers rolled: " + string.Join(", ",
                        activeRolls.Select(x => GameplayModifierCatalog.Name(x)).ToArray()));
            }
            catch { }
        }

        private string BuildFloorKey()
        {
            string scene = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name ?? "";
            int level = -1;
            string title = "";
            try
            {
                CoreGameManager core = Singleton<CoreGameManager>.Instance;
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                if (bgm != null) level = bgm.CurrentLevel;
                if (core != null)
                {
                    int coreLevel = R.Get<int>(core, "currentLevel", level);
                    if (coreLevel >= 0) level = coreLevel;
                    if (core.sceneObject != null)
                        title = core.sceneObject.levelTitle ?? "";
                }
            }
            catch { }

            return scene + "|" + level + "|" + title;
        }

        private static bool IsPitstopScene()
        {
            try
            {
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                if (ElevatorUnlockService.IsPitstopManager(bgm)) return true;
                string scene = UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().name.ToLowerInvariant();
                return scene.Contains("pitstop") || scene.Contains("pit_stop");
            }
            catch { return false; }
        }

        public void NotifyBeginPlay(BaseGameManager bgm)
        {
            if (bgm == null || ElevatorUnlockService.IsPitstopManager(bgm)) return;
            beginPlayReached = true;
            StopReveal();
        }

        public void OnElevatorResults(ElevatorScreen screen)
        {
            OnFloorLeaving();
            resultsElevatorScreenId = screen != null ? screen.GetInstanceID() : 0;
        }

        public void OnFloorLeaving()
        {
            beginPlayReached = false;
            StopReveal();
            DestroyPauseDisplay();
        }

        private IEnumerator RevealRoutine(Transform preferredParent,
            Transform preferredMarker)
        {
            Transform elevatorScreenMarker = preferredMarker;
            Transform displayParent = preferredParent;
            float findTime = 12f;
            while (findTime > 0f && displayParent == null)
            {
                displayParent = FindElevatorScreenParent(out elevatorScreenMarker);
                if (displayParent != null) break;
                findTime -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (displayParent == null)
            {
                try
                {
                    displayParent = Singleton<CoreGameManager>.Instance?
                        .GetHud(0)?.Canvas()?.transform;
                }
                catch { }
            }
            if (displayParent == null) { revealRoutine = null; yield break; }

            revealObject = BuildClipboardDisplay(displayParent, true);
            if (revealObject == null) { revealRoutine = null; yield break; }
            RectTransform rect = revealObject.GetComponent<RectTransform>();
            Vector2 shown = new Vector2(10f, -8f);
            Vector2 hidden = new Vector2(10f, -260f);
            rect.anchoredPosition = hidden;
            float slide = 0f;
            while (slide < 1f && revealObject != null)
            {
                slide += Time.unscaledDeltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(slide), 3f);
                rect.anchoredPosition = Vector2.Lerp(hidden, shown, eased);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = shown;

            float safety = 90f;
            float minimumVisible = 1f;
            while (safety > 0f && revealObject != null)
            {
                if (minimumVisible > 0f)
                    minimumVisible -= Time.unscaledDeltaTime;
                else if (beginPlayReached
                    || (elevatorScreenMarker != null
                        && !elevatorScreenMarker.gameObject.activeInHierarchy))
                    break;
                safety -= Time.unscaledDeltaTime;
                yield return null;
            }

            float down = 0f;
            while (down < 1f && revealObject != null)
            {
                down += Time.unscaledDeltaTime;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(down));
                rect.anchoredPosition = Vector2.Lerp(shown, hidden, eased);
                yield return null;
            }
            if (revealObject != null) Destroy(revealObject);
            revealObject = null;
            revealRoutine = null;
        }

        private static Transform FindElevatorScreenParent(out Transform marker)
        {
            marker = null;
            try
            {
                RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rect = rects[i];
                    if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                    bool elevator = false;
                    bool screen = false;
                    Transform cursor = rect.transform;
                    Transform bestMarker = null;
                    for (int depth = 0; depth < 8 && cursor != null; depth++)
                    {
                        string name = cursor.name.ToLowerInvariant();
                        if (name.Contains("elevator")) elevator = true;
                        if (name.Contains("screen")) screen = true;
                        if (name.Contains("elevatorscreen")
                            || name.Contains("elevator_screen")) bestMarker = cursor;
                        cursor = cursor.parent;
                    }
                    if (!(elevator && screen))
                    {
                        MonoBehaviour[] behaviours = rect.GetComponents<MonoBehaviour>();
                        for (int b = 0; b < behaviours.Length; b++)
                        {
                            MonoBehaviour behaviour = behaviours[b];
                            if (behaviour == null) continue;
                            string typeName = behaviour.GetType().Name.ToLowerInvariant();
                            if (typeName.Contains("elevator")
                                && typeName.Contains("screen"))
                            {
                                elevator = screen = true;
                                bestMarker = rect.transform;
                                break;
                            }
                        }
                    }
                    if (!elevator || !screen) continue;
                    marker = bestMarker ?? rect.transform;
                    Canvas canvas = rect.GetComponentInParent<Canvas>();
                    return canvas != null ? canvas.transform : marker;
                }
            }
            catch { }
            return null;
        }

        private GameObject BuildClipboardDisplay(Transform parent, bool animated)
        {
            if (parent == null) return null;
            GameObject root = new GameObject(animated
                ? "GameplayModifiersReveal" : "GameplayModifiersPause",
                typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.sizeDelta = new Vector2(230f, 230f);
            rootRect.anchoredPosition = new Vector2(10f, -8f);

            GameObject clipboard = BuildOriginalOptionsClipboard(root.transform);
            if (clipboard == null)
            {
                try
                {
                    KnoxumsChaosModePlugin.Log.LogError(
                        "Gameplay Modifiers: original OptionsClipboard sprite was not found.");
                }
                catch { }
                Destroy(root);
                return null;
            }
            RectTransform clipboardRect = clipboard.GetComponent<RectTransform>();
            if (clipboardRect != null)
            {
                clipboardRect.anchorMin = clipboardRect.anchorMax = new Vector2(0f, 0f);
                clipboardRect.pivot = new Vector2(0f, 0f);
                clipboardRect.anchoredPosition = Vector2.zero;
                clipboardRect.sizeDelta = new Vector2(220f, 185f);
                clipboardRect.localScale = Vector3.one;
            }

            HudManager modifierHud = null;
            try { modifierHud = Singleton<CoreGameManager>.Instance?.GetHud(0); }
            catch { }
            TMP_FontAsset font = modifierHud != null
                ? ChaosManager.Instance?.GetComicSansFont(modifierHud) : null;
            if (font == null)
            {
                ElevatorScreen elevatorScreen = Singleton<ElevatorScreen>.Instance;
                TMP_Text floorLabel = R.Get<TMP_Text>(elevatorScreen, "floorText", null);
                if (floorLabel != null) font = floorLabel.font;
            }
            if (font == null) font = FindComicFont();


            CreateRuntimeText(root.transform, "ModifiersTitleShadow",
                "Modifiers:", font, 24f, Color.black,
                TextAlignmentOptions.Center, new Vector2(2f, 190f),
                new Vector2(220f, 34f));
            CreateRuntimeText(root.transform, "ModifiersTitle",
                "Modifiers:", font, 24f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 192f),
                new Vector2(220f, 34f));

            List<KeyValuePair<GameplayModifierId, int>> grouped = GroupForDisplay();
            for (int i = 0; i < grouped.Count && i < 5; i++)
            {
                KeyValuePair<GameplayModifierId, int> entry = grouped[i];
                string text = GameplayModifierCatalog.Name(entry.Key);
                if (entry.Value > 1)
                    text += "\n<space=26px><size=9><color=#707070>×" + entry.Value
                        + "</color></size>";


                CreateRuntimeText(root.transform, "ModifierRow" + i, text, font,
                    14f, Color.black, TextAlignmentOptions.TopLeft,
                    new Vector2(34f, 112f - i * 22f), new Vector2(172f, 26f));
            }

            root.transform.SetAsLastSibling();
            PlaceBelowCursor(root.transform);
            return root;
        }

        private List<KeyValuePair<GameplayModifierId, int>> GroupForDisplay()
        {
            List<KeyValuePair<GameplayModifierId, int>> result =
                new List<KeyValuePair<GameplayModifierId, int>>();
            HashSet<GameplayModifierId> seen = new HashSet<GameplayModifierId>();
            for (int i = 0; i < activeRolls.Count; i++)
            {
                GameplayModifierId id = activeRolls[i];
                if (seen.Add(id))
                    result.Add(new KeyValuePair<GameplayModifierId, int>(id,
                        GetStacks(id)));
            }
            return result;
        }

        private static GameObject BuildOriginalOptionsClipboard(Transform parent)
        {
            if (parent == null) return null;
            Sprite sprite = ResolveOptionsClipboardSprite();
            if (sprite == null) return null;
            GameObject clipboard = new GameObject("GameplayModifiersOptionsClipboard",
                typeof(RectTransform), typeof(Image));
            clipboard.transform.SetParent(parent, false);
            Image image = clipboard.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;


            try
            {
                if (sprite.texture != null)
                {
                    sprite.texture.filterMode = FilterMode.Point;
                    sprite.texture.wrapMode = TextureWrapMode.Clamp;
                    sprite.texture.anisoLevel = 0;
                }
            }
            catch { }
            return clipboard;
        }

        private static void PlaceBelowCursor(Transform visual)
        {
            if (visual == null || visual.parent == null) return;
            CursorController cursor = CursorController.Instance;
            if (cursor == null)
            {
                visual.SetAsLastSibling();
                return;
            }
            Transform branch = cursor.transform;
            while (branch.parent != null && branch.parent != visual.parent)
                branch = branch.parent;
            if (branch.parent != visual.parent)
            {
                visual.SetAsLastSibling();
                return;
            }
            branch.SetAsLastSibling();
            visual.SetSiblingIndex(Mathf.Max(0, visual.parent.childCount - 2));
        }

        private void EnsurePauseOverlay()
        {
            if (pauseObject == null) return;
            Canvas visualCanvas = pauseObject.GetComponent<Canvas>();
            if (visualCanvas == null) visualCanvas = pauseObject.AddComponent<Canvas>();
            visualCanvas.overrideSorting = true;
            visualCanvas.sortingOrder = 32000;
            CursorController cursor = CursorController.Instance;
            if (cursor != null)
            {
                Canvas cursorCanvas = cursor.GetComponent<Canvas>();
                if (cursorCanvas == null) cursorCanvas = cursor.gameObject.AddComponent<Canvas>();
                cursorCanvas.overrideSorting = true;
                cursorCanvas.sortingOrder = 32001;
            }
            PlaceBelowCursor(pauseObject.transform);
        }

        private void LateUpdate()
        {
            if (revealObject != null) PlaceBelowCursor(revealObject.transform);
            if (pauseObject != null) EnsurePauseOverlay();
        }

        private static TMP_FontAsset FindComicFont()
        {
            if (optionsClipboardFont != null) return optionsClipboardFont;
            try
            {
                TMP_FontAsset comic = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                    .FirstOrDefault(x => x != null
                        && x.name.ToLowerInvariant().Contains("comic"));
                if (comic != null)
                {
                    optionsClipboardFont = comic;
                    return comic;
                }
                optionsClipboardFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                    .FirstOrDefault(x => x != null);
                return optionsClipboardFont;
            }
            catch { return null; }
        }

        private static TextMeshProUGUI CreateRuntimeText(Transform parent,
            string name, string value, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return text;
        }

        private void Update()
        {
            if (pauseObject != null) DestroyPauseDisplay();
        }

        private Transform FindPauseParent()
        {
            Transform best = null;
            int bestScore = int.MinValue;
            try
            {
                MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null || !behaviour.gameObject.activeInHierarchy) continue;
                    if (pauseObject != null && (behaviour.transform == pauseObject.transform
                        || behaviour.transform.IsChildOf(pauseObject.transform))) continue;
                    string typeName = behaviour.GetType().Name.ToLowerInvariant();
                    string objectName = behaviour.gameObject.name.ToLowerInvariant();
                    if (typeName.Contains("gameplaymodifier")
                        || objectName.Contains("gameplaymodifier")) continue;
                    int score = 0;
                    if (typeName.Contains("pausemenu")) score += 1000;
                    else if (typeName.Contains("pause")) score += 500;
                    if (objectName.Contains("pausemenu")) score += 800;
                    else if (objectName.Contains("pause")) score += 300;
                    if (score <= 0) continue;
                    Canvas canvas = behaviour.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.gameObject.activeInHierarchy
                        && score > bestScore)
                    { bestScore = score; best = canvas.transform; }
                }

                RectTransform[] rects = Resources.FindObjectsOfTypeAll<RectTransform>();
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rect = rects[i];
                    if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                    if (pauseObject != null && (rect.transform == pauseObject.transform
                        || rect.transform.IsChildOf(pauseObject.transform))) continue;
                    string name = rect.name.ToLowerInvariant();
                    if (!name.Contains("pause") || name.Contains("gameplaymodifiers")) continue;
                    int score = name.Contains("pausemenu") ? 700 : 200;
                    Canvas canvas = rect.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.gameObject.activeInHierarchy
                        && score > bestScore)
                    { bestScore = score; best = canvas.transform; }
                }
            }
            catch { }
            return best;
        }

        private void StopReveal()
        {
            if (revealRoutine != null)
            {
                try { StopCoroutine(revealRoutine); } catch { }
                revealRoutine = null;
            }
            if (revealObject != null) Destroy(revealObject);
            revealObject = null;
        }

        private void DestroyPauseDisplay()
        {
            if (pauseObject != null) Destroy(pauseObject);
            pauseObject = null;
            pauseParent = null;
        }
    }

    public class ChaosManager : MonoBehaviour
    {
        public static ChaosManager Instance { get; private set; }

        public bool IsChaosModeActive => KnoxumsChaosModePlugin.IsChaosModeEnabledConfig?.Value ?? false;
        public bool IsEventPropsShuffleActive => KnoxumsChaosModePlugin.IsPropShuffleEnabledConfig?.Value ?? false;
        public bool IsCharPropShuffleActive => KnoxumsChaosModePlugin.IsCharPropShuffleEnabledConfig?.Value ?? false;
        public bool IsItemPropShuffleActive => KnoxumsChaosModePlugin.IsItemPropShuffleEnabledConfig?.Value ?? false;
        public bool IsCharacterSpritesShuffleActive => KnoxumsChaosModePlugin.IsCharSpritesShuffleEnabledConfig?.Value ?? false;
        public bool IsItemSpritesShuffleActive => KnoxumsChaosModePlugin.IsItemSpritesShuffleEnabledConfig?.Value ?? false;
        public bool IsStringsShuffleActive => KnoxumsChaosModePlugin.IsStringsShuffleEnabledConfig?.Value ?? false;
        public bool IsSoundsShuffleActive => KnoxumsChaosModePlugin.IsSoundsShuffleEnabledConfig?.Value ?? false;
        public bool IsCtrlMapShuffleActive => KnoxumsChaosModePlugin.IsCtrlMapShuffleEnabledConfig?.Value ?? false;
        public bool IsItemsPlaceShuffleActive => KnoxumsChaosModePlugin.IsItemsPlaceShuffleEnabledConfig?.Value ?? false;
        public bool IsCharPlaceShuffleActive => KnoxumsChaosModePlugin.IsCharPlaceShuffleEnabledConfig?.Value ?? false;
        public bool IsBuildersErrorActive => KnoxumsChaosModePlugin.IsBuildersErrorEnabledConfig?.Value ?? false;
        public bool IsDoubleEventsActive => KnoxumsChaosModePlugin.IsDoubleEventsEnabledConfig?.Value ?? false;
        public bool IsDiscoShuffleActive => KnoxumsChaosModePlugin.IsDiscoShuffleEnabledConfig?.Value ?? false;
        public bool IsBaldiCowardActive => (KnoxumsChaosModePlugin.IsBaldiCowardEnabledConfig?.Value ?? false)
            && !(KnoxumsChaosModePlugin.IsLapsEnabledConfig?.Value ?? false);
        public bool IsLapsActive => KnoxumsChaosModePlugin.IsLapsEnabledConfig?.Value ?? false;
        public bool IsLightsOutActive => KnoxumsChaosModePlugin.IsLightsOutEnabledConfig?.Value ?? false;
        public bool IsMirroredActive => KnoxumsChaosModePlugin.IsMirroredEnabledConfig?.Value ?? false;
        public bool IsGooshoesActive => KnoxumsChaosModePlugin.IsGooshoesEnabledConfig?.Value ?? false;
        public bool IsLbTestSchoolActive => KnoxumsChaosModePlugin.IsLbTestSchoolEnabledConfig?.Value ?? false;
        public bool IsItemMischiefActive => KnoxumsChaosModePlugin.IsItemMischiefEnabledConfig?.Value ?? false;
        public bool InfiniteLaps => IsLapsActive
            && (KnoxumsChaosModePlugin.LapsCountConfig?.Value ?? 2) <= 0;
        public int LapsCount => InfiniteLaps
            ? int.MaxValue
            : Mathf.Clamp(KnoxumsChaosModePlugin.LapsCountConfig?.Value ?? 2, 2, 5);
        public int CurrentLap { get; set; } = 1;
        public CloneSpawnPoint CurrentCloneSpawnPoint => KnoxumsChaosModePlugin.CloneSpawnPointConfig?.Value ?? CloneSpawnPoint.CharPosition;
        public bool IncludeExits => KnoxumsChaosModePlugin.IncludeExitsConfig?.Value ?? false;
        public ChaosModeType CurrentChaosMode => KnoxumsChaosModePlugin.SelectedChaosMode?.Value ?? ChaosModeType.Chaos;
        public int NotebooksCollectedCount { get; set; }
        public bool IsLevelReady { get; set; }
        public int BuildersErrorRetries { get; set; }
        public bool IsEggActive { get; private set; }

        private const int MaxClonesPerCharacter = 24;
        private readonly Dictionary<string, float> origSpeeds = new Dictionary<string, float>();
        private bool chaosInitialSpawnDone;
        private BaseGameManager cBGM;
        private float cBGMT;
        private const float BGMC = .5f;

        private readonly List<string> dbgE = new List<string>(), dbgC = new List<string>(), dbgI = new List<string>();
        private string eggBuf = "", codeBuf = "";
        private static string EggPath => Path.Combine(Application.persistentDataPath, "knx_ChaosMode", "egg.cfg");

        private List<string> strPool = new List<string>
        {
            "Baldi", "Principal of the Thing", "Playtime", "It's a Bully", "Gotta Sweep", "Arts and Crafters",
            "First Prize", "Beans", "Mrs. Pomp", "The Test", "Johnny", "You need to find a way out!",
            "I hear every door you open!", "No running in the halls!", "Detention for you!", "When will you learn?",
            "You should know better!", "I want to play with someone!", "Give me something good!",
            "GOTTA SWEEP SWEEP SWEEP!", "Quarter", "BSODA", "Energy Flavored Zesty Bar", "Principal's Keys",
            "Alarm Clock", "Safety Scissors", "Dirty Chalk Eraser", "Invisibility Elixir", "WD-NoSquee",
            "Big Ol' Boots", "Apple for Baldi", "Teleporter", "No entering faculty rooms!", "No eating!",
            "No drinking!", "No escaping detention!", "Yellow Door Lock", "Baldi's Basics Plus",
            "Congratulations!", "You did great!"
        };
        private readonly Dictionary<string, string> strMap = new Dictionary<string, string>();
        private readonly List<string> wordPool = new List<string>();

        private readonly Dictionary<Character, List<Sprite>> pfxSpr = new Dictionary<Character, List<Sprite>>();
        private readonly List<Character> allCT = new List<Character>();
        private readonly Dictionary<int, Character> instVis = new Dictionary<int, Character>();
        private readonly Dictionary<Sprite, Character> sprOwn = new Dictionary<Sprite, Character>();
        private readonly Dictionary<long, Sprite> instSprC = new Dictionary<long, Sprite>();
        private readonly Dictionary<int, Character> npcIC = new Dictionary<int, Character>();
        private readonly List<Sprite> itmSP = new List<Sprite>();
        private readonly Dictionary<Sprite, Sprite> itmSM = new Dictionary<Sprite, Sprite>();
        private readonly HashSet<Sprite> itmMappedValues = new HashSet<Sprite>();
        private readonly List<AudioClip> audP = new List<AudioClip>();
        private readonly Dictionary<AudioClip, AudioClip> audM = new Dictionary<AudioClip, AudioClip>();
        private readonly HashSet<int> pairedEvts = new HashSet<int>();
        private readonly Dictionary<string, string> ctrlMap = new Dictionary<string, string>();
        private bool ctrlShuffled;
        private static readonly string[] shuffleActions =
            { "Run", "Interact", "UseItem", "LookBack", "ItemRight", "ItemLeft", "MouseSubmit" };

        private float invT, sndT2, sprT, discoT2;
        private bool wasR;
        private bool beApplied;
        private LevelGenerationParameters beLd;
        private IntVector2 beMinSz, beMaxSz;
        private int beMinPl, beMaxPl, beMinHR, beMaxHR, beMinRH, beMaxRH, beBTC, beATC, beDEB,
            beEC, beMinSR, beMaxSR, beMaxIV, beMinEv, beMaxEv, beMaxLD, beStdLS;
        private float beCW, beDV, beDP, beEDC, beHP, beIEG, beMinEG, beMaxEG, bePC;
        private float[] beSTH;

        private GameObject lapsHudObj;
        private Image lapsHudIcon;
        private TMP_Text lapsHudText;
        private GameObject lapsFlashObj;
        private Image lapsFlashImage;
        private GameObject lapsBlackObj;
        private Image lapsBlackImage;
        private Sprite arrowsSprite;
        private bool playerBaseSpeedCaptured;
        private float playerBaseWalkSpeed, playerBaseRunSpeed;

        private Coroutine activeLapCoroutine;
        private bool lapTransitionInProgress;
        private static int pendingLap;
        private static bool lapRestartPending;
        private static bool lapFadeOutPending;
        public static bool skipElevatorOnLap;
        private bool floorExitToPitstopCommitted;
        public bool FloorExitToPitstopCommitted => floorExitToPitstopCommitted;
        private bool skipRemainingLaps;
        public bool SkipRemainingLaps => skipRemainingLaps;
        private bool floorIntroActive;
        public bool FloorIntroActive => floorIntroActive;

        private bool ytpFloorStartCaptured;
        private int ytpFloorStart, ytpRawEarned;
        private bool lapsUsedThisFloor;
        private int pendingPitstopYtpStart, pendingPitstopYtpRaw, pendingPitstopTubes, pendingPitstopLap;
        private bool pendingPitstopYtpFix;
        private int pitstopYtpCorrect = int.MinValue;
        private float pitstopYtpEnforce;

        private GameObject betaWatermarkObj;
        private TMP_Text betaWatermarkText;
        private float watermarkRetryTimer;
        private GameObject pitstopReminderObject;
        private Coroutine pitstopReminderRoutine;
        private Dictionary<int, ItemObject> originalPickupItems = new Dictionary<int, ItemObject>();
        private Dictionary<int, ItemObject> previousLapPickupItems = new Dictionary<int, ItemObject>();

        private FunLanternMode funLantern;
        private FunCameraFlip funCam;
        private Coroutine lbTestRoutine;
        private readonly Dictionary<Cell, bool> lbTestOriginalLights = new Dictionary<Cell, bool>();
        private EnvironmentController lbTestEc;
        private float funCamRetry, pitstopDoorTimer;
        private bool generationBusy, funCanRun;
        private Coroutine funWaitRoutine;
        private List<ItemObject> schoolItemCache;
        private float schoolItemCacheAt;

        private void Awake() { Instance = this; LoadEgg(); }
        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSc;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnUn;
        }
        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSc;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnUn;
            StopFunSettings();
            StopPitstopChaosReminder();
        }
        private void OnSc(UnityEngine.SceneManagement.Scene a, UnityEngine.SceneManagement.Scene b)
        { IsLevelReady = false; ResetSchoolShuffle(); }
        private void OnUn(UnityEngine.SceneManagement.Scene s)
        { IsLevelReady = false; ResetSchoolShuffle(); }

        public bool IsAnyChaosOptionActive()
        {
            return IsChaosModeActive || IsEventPropsShuffleActive || IsCharPropShuffleActive || IsItemPropShuffleActive
                || IsCharacterSpritesShuffleActive || IsItemSpritesShuffleActive || IsStringsShuffleActive
                || IsSoundsShuffleActive || IsCtrlMapShuffleActive || IsItemsPlaceShuffleActive
                || IsCharPlaceShuffleActive || IsBuildersErrorActive || IsDoubleEventsActive || IsDiscoShuffleActive
                || IsBaldiCowardActive || IsLapsActive || IsLightsOutActive || IsMirroredActive || IsGooshoesActive
                || IsLbTestSchoolActive || IsItemMischiefActive;
        }

        private void Update()
        {
            HandleCode(); HandleEgg();
            if (wasR && IsLevelReady && Singleton<BaseGameManager>.Instance == null)
            { IsLevelReady = false; ResetSchoolShuffle(); }
            wasR = IsLevelReady;
            bool ig = IsInGame();
            if (ig && IsStringsShuffleActive && (invT += Time.deltaTime) >= .1f) { invT = 0f; strMap.Clear(); }
            if (ig && IsSoundsShuffleActive && (sndT2 += Time.deltaTime) >= .1f) { sndT2 = 0f; audM.Clear(); }
            if (ig && IsCharacterSpritesShuffleActive && (sprT += Time.deltaTime) >= .5f)
            { sprT = 0f; AttachShufflers(); }
            if (ig && IsDiscoShuffleActive && !IsLightsOutActive) UpdateDisco();
            if (funCanRun && IsLevelReady && IsGameActive()) TickFunSettings();
            if (IsPitstopActive() && !ElevatorUnlockService.PitstopExitArmed) TickPitstopElevators();
            else if (IsGameActive() && !generationBusy) TickClosedElevatorBarriers();
            EnforcePitstopYtp();
            if (IsLapsActive) SyncLapsHudWithNotebooks();


        }

        private void HandleCode()
        {
            if (!Input.anyKeyDown) return;
            foreach (char c in Input.inputString)
            {
                if (char.IsDigit(c))
                {
                    codeBuf += c;
                    if (codeBuf.Length > 8) codeBuf = codeBuf.Substring(codeBuf.Length - 8);
                    if (codeBuf == "11211994") { ToggleChaosMode(); codeBuf = ""; }
                }
                else if (char.IsLetter(c)) codeBuf = "";
            }
        }

        private void HandleEgg()
        {
            if (!Input.anyKeyDown || !IsInGame()) return;
            foreach (char c in Input.inputString)
            {
                if (char.IsLetter(c))
                {
                    eggBuf += char.ToLowerInvariant(c);
                    if (eggBuf.Length > 3) eggBuf = eggBuf.Substring(eggBuf.Length - 3);
                    if (eggBuf == "egg")
                    {
                        IsEggActive = !IsEggActive; SaveEgg(); eggBuf = "";
                        if (IsEggActive) ShowEgg();
                    }
                }
                else if (char.IsDigit(c)) eggBuf = "";
            }
        }

        public bool IsPaused() => Time.timeScale == 0f;
        public bool IsInGame()
        {
            if (!IsLevelReady || IsPaused()) return false;
            return IsGameActive();
        }
        public bool IsGameActive()
        {
            if (cBGM == null || Time.unscaledTime - cBGMT > BGMC)
            { cBGM = FindObjectOfType<BaseGameManager>(); cBGMT = Time.unscaledTime; }
            return cBGM != null;
        }
        public void ResetGameManagerCache() { cBGM = null; cBGMT = 0f; }
        public bool IsPitstopActiveForPatches() { return IsPitstopActive(); }

        public void SetChaosModeActiveState(bool active)
        {
            if (KnoxumsChaosModePlugin.IsChaosModeEnabledConfig == null
                || KnoxumsChaosModePlugin.IsChaosModeEnabledConfig.Value == active) return;
            KnoxumsChaosModePlugin.IsChaosModeEnabledConfig.Value = active;
            KnoxumsChaosModePlugin.Instance.Config.Save();
            if (active)
            {
                NotebooksCollectedCount = 0; chaosInitialSpawnDone = false;
                origSpeeds.Clear(); SilenceStartMusic(); ApplyChaosItemSpawns();
            }
            else
            {
                NotebooksCollectedCount = 0; chaosInitialSpawnDone = false; RestoreOrigSpeeds();
            }
        }
        public void ToggleChaosMode() { SetChaosModeActiveState(!IsChaosModeActive); }
        public void SilenceStartMusic()
        {
            if (!IsChaosModeActive) return;
            try
            {
                MusicManager mm = Singleton<MusicManager>.Instance;
                if (mm != null) { mm.StopMidi(); mm.StopFile(); }
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("SilenceStartMusic: " + ex); }
        }

        public void ResetSchoolShuffle()
        {
            ElevatorUnlockService.ResetForNewFloorOrLap();
            StopLapCoroutine();
            if (beApplied && beLd != null) try { RestoreBuildersError(beLd); } catch { }
            beApplied = false; beLd = null;
            strMap.Clear(); instSprC.Clear(); itmSM.Clear(); itmMappedValues.Clear(); audM.Clear();
            SoundShuffleDetachedPlaybackPatch.ClearMarks();
            pfxSpr.Clear(); allCT.Clear(); instVis.Clear(); sprOwn.Clear(); npcIC.Clear();
            itmSP.Clear(); audP.Clear(); pairedEvts.Clear(); origSpeeds.Clear();
            ResetCtrlShuffle(); ClearEggLog(); BuildersErrorRetries = 0; discoT2 = 0f;
            chaosInitialSpawnDone = false; EndFloorIntro();
            if (!lapRestartPending) CurrentLap = 1;
            DestroyLapsHud(); DestroyBetaWatermarkHud(); StopPitstopChaosReminder();
            playerBaseSpeedCaptured = false; playerBaseWalkSpeed = playerBaseRunSpeed = 0f;
            ResetGameManagerCache(); funCanRun = false; generationBusy = false;
            StopFunWait(); StopFunSettings();
        }

        public void ClearFloorExitCommit() { floorExitToPitstopCommitted = false; }
        public void ResetLapsToDefault()
        {
            CurrentLap = 1; lapRestartPending = false; lapTransitionInProgress = false; pendingLap = 1;
            lapFadeOutPending = false; skipElevatorOnLap = false;
            floorExitToPitstopCommitted = false; skipRemainingLaps = false; EndFloorIntro();
            originalPickupItems.Clear(); previousLapPickupItems.Clear(); StopLapCoroutine();
            ElevatorUnlockService.ResetForNewFloorOrLap();
        }

        public void TrackFloorYtpDelta(int delta)
        {
            if (delta == 0) return;
            if (!ytpFloorStartCaptured) CaptureFloorYtpStart();
            ytpRawEarned += delta;
            if (CurrentLap > 1) lapsUsedThisFloor = true;
        }
        public void CaptureFloorYtpStart()
        {
            if (ytpFloorStartCaptured) return;
            ytpFloorStart = ReadCurrentYtps(); ytpRawEarned = 0; ytpFloorStartCaptured = true;
        }
        public void MarkLapsUsedThisFloor() { lapsUsedThisFloor = true; }

        public void StripLapYtpMultiplierOnly()
        {
            if (!IsLapsActive)
            {
                pendingPitstopYtpFix = false; pitstopYtpEnforce = 0f; pitstopYtpCorrect = int.MinValue;
                ytpFloorStartCaptured = false; ytpRawEarned = 0; lapsUsedThisFloor = false; return;
            }
            pendingPitstopYtpStart = ytpFloorStartCaptured ? ytpFloorStart : ReadCurrentYtps();
            pendingPitstopYtpRaw = ytpRawEarned;
            pendingPitstopTubes = GetPowerTubeCount();
            pendingPitstopLap = CurrentLap > 1 ? CurrentLap : (lapsUsedThisFloor ? 2 : 1);
            pendingPitstopYtpFix = true;
            ytpFloorStartCaptured = false; ytpRawEarned = 0; lapsUsedThisFloor = false;
            StartCoroutine(StripLapYtpAfterResults());
        }

        private IEnumerator StripLapYtpAfterResults()
        {
            int start = pendingPitstopYtpStart, raw = pendingPitstopYtpRaw;
            int tubes = Mathf.Clamp(pendingPitstopTubes, 1, 3), lap = Mathf.Max(1, pendingPitstopLap);
            int expected = start + raw * tubes;
            float maxWait = 6f, stable = 0f;
            int last = int.MinValue;
            while (maxWait > 0f)
            {
                maxWait -= Time.unscaledDeltaTime;
                int cur = ReadCurrentYtps();
                if (cur == last) stable += Time.unscaledDeltaTime; else { last = cur; stable = 0f; }
                bool pit = false; try { pit = IsPitstopActive(); } catch { }
                if ((pit && stable >= .35f) || (!pit && stable >= .75f && maxWait < 4f)) break;
                yield return null;
            }
            if (!pendingPitstopYtpFix) yield break;
            pendingPitstopYtpFix = false;
            int now = ReadCurrentYtps(), target = now;
            if (lap <= 1) { pitstopYtpCorrect = int.MinValue; yield break; }
            if (now == expected)
            { pitstopYtpCorrect = expected; pitstopYtpEnforce = 4f; yield break; }
            if ((expected != 0 && now == expected * lap) || now == start + raw * tubes * lap) target = expected;
            else if (now > expected && (now - start) % lap == 0)
            {
                int candidate = start + (now - start) / lap;
                if (candidate >= expected && candidate < now) target = candidate;
            }
            if (target == now) { pitstopYtpCorrect = int.MinValue; yield break; }
            WriteCurrentYtps(target); pitstopYtpCorrect = target; pitstopYtpEnforce = 4f;
            KnoxumsChaosModePlugin.Log.LogInfo("YTP: stripped lap multiplier: " + now + " -> " + target);
        }

        private static int GetPowerTubeCount()
        {
            try
            {
                CoreGameManager cm = Singleton<CoreGameManager>.Instance;
                if (cm == null) return 3;
                return Mathf.Clamp(R.Get<int>(cm, "lives", 0) + R.Get<int>(cm, "extraLives", 0), 1, 3);
            }
            catch { return 3; }
        }
        private static readonly string[] YtpNames = { "currentYtps", "ytps", "youThoughtPoints" };
        private static int ReadCurrentYtps()
        {
            try
            {
                CoreGameManager cm = Singleton<CoreGameManager>.Instance;
                if (cm == null) return 0;
                for (int i = 0; i < YtpNames.Length; i++)
                {
                    FieldInfo f = R.Field(cm, YtpNames[i]);
                    if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(cm);
                    PropertyInfo p = cm.GetType().GetProperty(YtpNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(cm, null);
                }
            }
            catch { }
            return 0;
        }
        private static void WriteCurrentYtps(int value)
        {
            try
            {
                CoreGameManager cm = Singleton<CoreGameManager>.Instance;
                if (cm == null) return;
                for (int i = 0; i < YtpNames.Length; i++)
                {
                    FieldInfo f = R.Field(cm, YtpNames[i]);
                    if (f != null && f.FieldType == typeof(int)) f.SetValue(cm, value);
                    PropertyInfo p = cm.GetType().GetProperty(YtpNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(int) && p.CanWrite) p.SetValue(cm, value, null);
                }
                HudManager hud = cm.GetHud(0);
                if (hud != null)
                {
                    string[] names = { "UpdatePointsText", "UpdateYTPs", "SetPoints", "RefreshPoints" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        MethodInfo mi = hud.GetType().GetMethod(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (mi == null) continue;
                        ParameterInfo[] ps = mi.GetParameters();
                        if (ps.Length == 0) mi.Invoke(hud, null);
                        else if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) mi.Invoke(hud, new object[] { value });
                    }
                }
            }
            catch { }
        }
        private void EnforcePitstopYtp()
        {
            if (pitstopYtpEnforce <= 0f || pitstopYtpCorrect == int.MinValue) return;
            pitstopYtpEnforce -= Time.unscaledDeltaTime;
            try { if (!IsPitstopActive() && pitstopYtpEnforce < 3.5f) return; } catch { }
            if (ReadCurrentYtps() != pitstopYtpCorrect) WriteCurrentYtps(pitstopYtpCorrect);
            if (pitstopYtpEnforce <= 0f) pitstopYtpCorrect = int.MinValue;
        }

        public void BeginBaldiCountdownAudioWindow() { floorIntroActive = true; }
        public void EndFloorIntro() { floorIntroActive = false; }
        public void StartFloorIntro(BaseGameManager bgm)
        {
            BeginBaldiCountdownAudioWindow();
            try
            {
                MusicManager mm = Singleton<MusicManager>.Instance;
                if (mm != null) { mm.StopMidi(); mm.StopFile(); mm.PlayMidi("school", true); }
            }
            catch { }
            try
            {
                MainGameManager main = bgm as MainGameManager;
                if (main != null && bgm.Ec != null)
                {
                    MethodInfo mi = typeof(MainGameManager).GetMethod("CreateHappyBaldi",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    mi?.Invoke(main, null);
                }
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("StartFloorIntro CreateHappyBaldi: " + ex); }
        }
        public bool ShouldStartNewLap()
        {
            return !floorExitToPitstopCommitted && !skipRemainingLaps && IsLapsActive
                && !lapTransitionInProgress && CurrentLap < LapsCount;
        }
        public bool IsLastLap()
        {
            if (!IsLapsActive || skipRemainingLaps) return true;
            if (InfiniteLaps) return false;
            return CurrentLap >= LapsCount;
        }
        public void LeaveToPitstopNow(string reason)
        {
            if (floorExitToPitstopCommitted) return;
            BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
            if (bgm == null || bgm is EndlessGameManager) return;
            skipRemainingLaps = true; CommitFloorExitToPitstop();
            try
            {
                PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                ElevatorUnlockService.CloseElevatorDoors(FindSpawnElevatorForLap(bgm.Ec, pm));
            }
            catch { }
            try { bgm.LoadNextLevel(); }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("LeaveToPitstopNow: " + ex); }
        }
        public void CommitFloorExitToPitstop()
        {
            if (floorExitToPitstopCommitted) return;
            floorExitToPitstopCommitted = true;
            if (IsLapsActive) StripLapYtpMultiplierOnly();
            KnoxumsChaosModePlugin.Log.LogInfo("Laps: floor exit committed once.");
        }
        public void StopLapCoroutine()
        {
            if (activeLapCoroutine != null) { try { StopCoroutine(activeLapCoroutine); } catch { } activeLapCoroutine = null; }
            lapTransitionInProgress = false;
        }
        public bool IsLapTransitionInProgress => lapTransitionInProgress;

        private void LoadSubs()
        {
            try
            {
                string p = Path.Combine(Application.streamingAssetsPath, "Subtitles_En.json");
                if (!File.Exists(p)) { BuildWP(); return; }
                string j = File.ReadAllText(p);
                List<string> loaded = new List<string>();
                int i = 0;
                while ((i = j.IndexOf("\"value\"", i, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    int colon = j.IndexOf(':', i), start = colon >= 0 ? j.IndexOf('"', colon) : -1;
                    if (start < 0) break;
                    int end = start + 1;
                    while (end < j.Length)
                    {
                        if (j[end] == '"')
                        {
                            int slashes = 0, q = end - 1;
                            while (q >= start && j[q--] == '\\') slashes++;
                            if ((slashes & 1) == 0) break;
                        }
                        end++;
                    }
                    if (end >= j.Length) break;
                    string v = j.Substring(start + 1, end - start - 1)
                        .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    if (!string.IsNullOrWhiteSpace(v) && v.Trim().Length > 1) loaded.Add(v);
                    i = end + 1;
                }
                if (loaded.Count > 0) { strPool.Clear(); strPool.AddRange(loaded); }
                BuildWP();
            }
            catch { BuildWP(); }
        }
        private void BuildWP()
        {
            wordPool.Clear();
            foreach (string s in strPool)
            {
                if (string.IsNullOrEmpty(s)) continue;
                foreach (string w in s.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':', '"', '[', ']', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries))
                    if (w.Length > 1 && !wordPool.Contains(w)) wordPool.Add(w);
            }
        }

        public void AttachShufflers()
        {
            BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
            IEnumerable<NPC> npcs;
            if (bgm != null && bgm.Ec != null && bgm.Ec.Npcs != null)
                npcs = bgm.Ec.Npcs;
            else
                npcs = FindObjectsOfType<NPC>();

            foreach (NPC n in npcs)
                if (n != null && n.GetComponent<SpriteShuffler>() == null)
                    n.gameObject.AddComponent<SpriteShuffler>();
        }
        public void PopCharSpr()
        {
            try
            {
                pfxSpr.Clear(); allCT.Clear(); instVis.Clear(); sprOwn.Clear(); instSprC.Clear(); npcIC.Clear();
                foreach (NPC np in Resources.FindObjectsOfTypeAll<NPC>())
                {
                    if (np == null || np.gameObject.scene.IsValid()) continue;
                    Character ct = np.Character;
                    foreach (SpriteRenderer sr in np.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (sr == null || sr.sprite == null) continue;
                        if (!pfxSpr.ContainsKey(ct)) pfxSpr[ct] = new List<Sprite>();
                        if (!pfxSpr[ct].Contains(sr.sprite)) { pfxSpr[ct].Add(sr.sprite); sprOwn[sr.sprite] = ct; }
                    }
                }
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (KeyValuePair<Character, List<Sprite>> kv in pfxSpr)
                {
                    List<Sprite> own = kv.Value;
                    HashSet<string> prefixes = new HashSet<string>();
                    foreach (Sprite s in own)
                    {
                        string prefix = "";
                        foreach (char ch in s.name) { if (ch == '_' || char.IsDigit(ch)) break; prefix += ch; }
                        if (prefix.Length >= 3) prefixes.Add(prefix.ToLowerInvariant());
                    }
                    foreach (Sprite s in all)
                    {
                        if (s == null || sprOwn.ContainsKey(s)) continue;
                        string name = s.name.ToLowerInvariant();
                        if (prefixes.Any(name.StartsWith)) { own.Add(s); sprOwn[s] = kv.Key; }
                    }
                    own.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                }
                allCT.AddRange(pfxSpr.Keys); RegLiveNpcs();
            }
            catch { }
        }
        public void RegLiveNpcs()
        {
            npcIC.Clear();
            foreach (NPC n in FindObjectsOfType<NPC>()) if (n != null) RegNpcCh(n);
        }
        public void RegNpcCh(NPC n)
        {
            if (n == null) return;
            npcIC[n.GetInstanceID()] = n.Character; EnsureInstanceVisual(n.GetInstanceID(), n.Character);
        }
        public void EnsureInstanceVisual(int id, Character own)
        {
            if (instVis.ContainsKey(id) || allCT.Count <= 1) return;
            List<Character> candidates = allCT.Where(c => c != own).ToList();
            instVis[id] = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : own;
        }
        public void PopItmSpr()
        {
            itmSP.Clear(); itmSM.Clear(); itmMappedValues.Clear();
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                string n = s.name.ToLowerInvariant();
                if ((n.Contains("item") || n.Contains("icon") || n.Contains("quarter") || n.Contains("bsoda")
                    || n.Contains("zesty") || n.Contains("key") || n.Contains("clock") || n.Contains("scissors")
                    || n.Contains("eraser") || n.Contains("elixir") || n.Contains("squee") || n.Contains("boots")
                    || n.Contains("apple") || n.Contains("teleporter") || n.Contains("lock"))
                    && !n.Contains("left") && !n.Contains("right") && !n.Contains("center") && !n.Contains("frame")
                    && !n.Contains("border") && !n.Contains("slot") && !itmSP.Contains(s)) itmSP.Add(s);
            }
        }
        public void PopAud()
        {
            audP.Clear(); audM.Clear();
            foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
                if (c != null && c.length > 0f && !audP.Contains(c)) audP.Add(c);
        }
        public void FSwapNpc()
        {
            if (!IsCharacterSpritesShuffleActive) return;
            foreach (NPC n in FindObjectsOfType<NPC>())
                if (n != null)
                    foreach (SpriteRenderer sr in n.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr != null && sr.sprite != null) sr.sprite = GetShuffledCharacterSprite(sr.sprite, n);
        }
        public void FSwapItm()
        {
            if (!IsItemSpritesShuffleActive) return;
            foreach (Pickup p in FindObjectsOfType<Pickup>())
                if (p != null)
                    foreach (SpriteRenderer sr in p.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr != null && sr.sprite != null) sr.sprite = GetShuffledItemSprite(sr.sprite);
        }
        public void FShufTMP()
        {
            if (!IsStringsShuffleActive) return;
            foreach (TMP_Text t in FindObjectsOfType<TMP_Text>())
                if (t != null && !string.IsNullOrEmpty(t.text) && !ChaosPatches.IsProtUI(t))
                    t.text = GetShuffledString(t.text);
        }

        public void MarkGenerationStarted()
        { generationBusy = true; funCanRun = false; IsLevelReady = false; StopFunSettings(); }
        public void ApplyFunAfterPostGen(BaseGameManager bgm)
        {
            StopFunWait();
            if (IsGeneratorBusy()) { StartFunAfterGeneration(); return; }
            generationBusy = false;
            if ((bgm != null && ElevatorUnlockService.IsPitstopManager(bgm)) || IsPitstopActive())
            {
                StopFunSettings(); ElevatorUnlockService.KeepPitstopElevatorsOpen(bgm);
                ElevatorUnlockService.ClearClosedElevatorFrontBarriers(bgm); return;
            }
            if (!PlayerAndCameraReady()) { StartFunAfterGeneration(); return; }
            IsLevelReady = true;
            try { ElevatorUnlockService.ClearClosedElevatorFrontBarriers(bgm ?? Singleton<BaseGameManager>.Instance); } catch { }
            AllowFunSettings();
            CreateLapsHud();

            RefreshSchoolItemPool();
        }

        public ItemObject PickSchoolItem(ItemObject exclude)
        {
            if (schoolItemCache == null || Time.unscaledTime - schoolItemCacheAt > 4f) RefreshSchoolItemPool();
            if (schoolItemCache == null || schoolItemCache.Count == 0) return null;
            if (schoolItemCache.Count == 1) return schoolItemCache[0];
            for (int i = 0; i < 8; i++)
            {
                ItemObject pick = schoolItemCache[Random.Range(0, schoolItemCache.Count)];
                if (pick != exclude) return pick;
            }
            return schoolItemCache[Random.Range(0, schoolItemCache.Count)];
        }
        public void RefreshSchoolItemPool() { schoolItemCache = CollectSchoolItems(); schoolItemCacheAt = Time.unscaledTime; }
        private List<ItemObject> CollectSchoolItems()
        {
            Dictionary<int, ItemObject> map = new Dictionary<int, ItemObject>();
            foreach (KeyValuePair<int, ItemObject> kv in originalPickupItems) AddSchoolItem(map, kv.Value);
            try
            {
                foreach (Pickup p in FindObjectsOfType<Pickup>())
                {
                    if (p == null || IsShopTransform(p.transform)) continue;
                    AddSchoolItem(map, R.Get<ItemObject>(p, "item", null));
                }
            }
            catch { }
            try
            {
                PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (pm != null && pm.itm != null && pm.itm.items != null)
                    foreach (ItemObject item in pm.itm.items) AddSchoolItem(map, item);
            }
            catch { }
            try
            {
                foreach (SodaMachine sm in FindObjectsOfType<SodaMachine>())
                {
                    if (sm == null) continue;
                    foreach (FieldInfo f in sm.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object value = null; try { value = f.GetValue(sm); } catch { }
                        if (value is ItemObject io) AddSchoolItem(map, io);
                        else if (value is ItemObject[] arr) foreach (ItemObject x in arr) AddSchoolItem(map, x);
                        else if (value is WeightedItemObject w) AddSchoolItem(map, w.selection);
                        else if (value is WeightedItemObject[] wa) foreach (WeightedItemObject x in wa) if (x != null) AddSchoolItem(map, x.selection);
                    }
                }
            }
            catch { }
            return new List<ItemObject>(map.Values);
        }
        private static void AddSchoolItem(Dictionary<int, ItemObject> map, ItemObject io)
        {
            if (io == null || io.itemType == Items.None || io.item == null
                || (io.itemSpriteLarge == null && io.itemSpriteSmall == null)) return;
            int id = io.GetInstanceID(); if (!map.ContainsKey(id)) map[id] = io;
        }
        private static bool IsShopTransform(Transform tr)
        {
            for (int i = 0; i < 3 && tr != null; i++, tr = tr.parent)
            {
                string n = tr.name.ToLowerInvariant();
                if (n.Contains("shop") || n.Contains("store") || n.Contains("johnny")) return true;
            }
            return false;
        }

        public void StartFunAfterGeneration()
        {
            StopFunWait(); funWaitRoutine = StartCoroutine(WaitGenThenFun());
        }
        private void StopFunWait()
        {
            if (funWaitRoutine == null) return;
            try { StopCoroutine(funWaitRoutine); } catch { }
            funWaitRoutine = null;
        }
        private static bool IsGeneratorBusy()
        {
            try
            {
                LevelBuilder[] builders = UnityEngine.Object.FindObjectsOfType<LevelBuilder>();
                for (int i = 0; i < builders.Length; i++)
                    if (builders[i] != null && builders[i].isActiveAndEnabled && LooksLikeStillGenerating(builders[i])) return true;
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                return bgm != null && !ElevatorUnlockService.IsPitstopManager(bgm) && !PlayerAndCameraReady();
            }
            catch { return false; }
        }
        private static bool LooksLikeStillGenerating(LevelBuilder lb)
        {
            string[] names = { "generating", "isGenerating", "inProgress", "working", "busy" };
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo f = R.Field(lb, names[i]);
                if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(lb)) return true;
            }
            return false;
        }
        private static bool PlayerAndCameraReady()
        {
            try
            {
                CoreGameManager cg = Singleton<CoreGameManager>.Instance;
                if (cg == null || cg.GetPlayer(0) == null) return false;
                GameCamera cam = cg.GetCamera(0); return cam != null && cam.camCom != null;
            }
            catch { return false; }
        }
        private IEnumerator WaitGenThenFun()
        {
            float safety = 180f;
            while (safety > 0f && IsGeneratorBusy())
            {
                if (IsPitstopActive()) { StopFunSettings(); funWaitRoutine = null; yield break; }
                safety -= Time.unscaledDeltaTime; yield return null;
            }
            generationBusy = false;
            float wait = 20f;
            while (wait > 0f && !PlayerAndCameraReady())
            {
                if (IsPitstopActive()) { StopFunSettings(); funWaitRoutine = null; yield break; }
                wait -= Time.unscaledDeltaTime; yield return null;
            }
            yield return null;
            if (IsPitstopActive()) { StopFunSettings(); funWaitRoutine = null; yield break; }
            IsLevelReady = true;
            ElevatorUnlockService.ClearClosedElevatorFrontBarriers(Singleton<BaseGameManager>.Instance);
            AllowFunSettings();
            CreateLapsHud();

            yield return new WaitForSecondsRealtime(.4f);
            ElevatorUnlockService.ClearClosedElevatorFrontBarriers(Singleton<BaseGameManager>.Instance);
            funWaitRoutine = null;
        }
        private void TickPitstopElevators()
        {
            if ((pitstopDoorTimer -= Time.unscaledDeltaTime) > 0f) return;
            pitstopDoorTimer = .35f;
            try { ElevatorUnlockService.KeepPitstopElevatorsOpen(Singleton<BaseGameManager>.Instance); } catch { }
        }
        private void TickClosedElevatorBarriers()
        {
            if ((pitstopDoorTimer -= Time.unscaledDeltaTime) > 0f) return;
            pitstopDoorTimer = .75f;
            try { ElevatorUnlockService.ClearClosedElevatorFrontBarriers(Singleton<BaseGameManager>.Instance); } catch { }
        }
        public void AllowFunSettings()
        {
            if (generationBusy || IsGeneratorBusy()) return;
            funCanRun = true; ApplyFunSettings();
        }
        public void ApplyFunSettings()
        {
            if (generationBusy || IsGeneratorBusy() || !funCanRun || !IsLevelReady) return;
            if (IsPitstopActive()) { StopFunSettings(); return; }
            try { ApplyFunCamera(); } catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Fun camera: " + ex); }
            try { ApplyLightsOut(); } catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Lights Out: " + ex); }
            try { ApplyLbTestSchool(); } catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("LB Test: " + ex); }
        }
        public void StopFunSettings()
        {
            funCamRetry = 0f;
            if (lbTestRoutine != null) { try { StopCoroutine(lbTestRoutine); } catch { } lbTestRoutine = null; }
            RestoreLbTestLights();
            if (funLantern != null)
            { try { funLantern.Shutdown(); } catch { } Destroy(funLantern); funLantern = null; }
            if (funCam != null)
            { try { funCam.Shutdown(); } catch { } Destroy(funCam); funCam = null; }
        }
        private void TickFunSettings()
        {
            if (IsPitstopActive()) { if (funLantern != null || funCam != null || lbTestRoutine != null) StopFunSettings(); return; }
            if (IsMirroredActive || IsGooshoesActive)
            {
                funCamRetry -= Time.unscaledDeltaTime;
                if ((funCam == null || !funCam.HasCameras) && funCamRetry <= 0f)
                { funCamRetry = .5f; ApplyFunCamera(); }
            }
            if (IsLightsOutActive && funLantern == null) ApplyLightsOut();
        }
        private void ApplyFunCamera()
        {

            if (IsMirroredActive || IsGooshoesActive)
            {
                if (funCam == null) funCam = gameObject.AddComponent<FunCameraFlip>();
                funCam.Apply(IsMirroredActive, IsGooshoesActive);
            }
            else if (funCam != null) { funCam.Shutdown(); Destroy(funCam); funCam = null; }
        }
        private void ApplyLightsOut()
        {
            BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
            EnvironmentController environment = bgm != null ? bgm.Ec : null;
            if (!IsLightsOutActive)
            {
                if (funLantern != null) { funLantern.Shutdown(); Destroy(funLantern); funLantern = null; }
                return;
            }
            if (environment == null) return;
            if (funLantern == null) funLantern = gameObject.AddComponent<FunLanternMode>();
            funLantern.Initialize(environment);
        }
        private void ApplyLbTestSchool()
        {
            if (lbTestRoutine != null) { StopCoroutine(lbTestRoutine); lbTestRoutine = null; RestoreLbTestLights(); }
            if (!IsLbTestSchoolActive || IsLightsOutActive) return;
            BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
            if (bgm == null || bgm.Ec == null) return;
            lbTestRoutine = StartCoroutine(LbTestSchoolRoutine(bgm.Ec));
        }
        private static bool ReadCellLight(Cell c)
        {
            if (c == null) return false;
            string[] names = { "lightOn", "lit", "lightActive", "on" };
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo f = R.Field(c, names[i]);
                if (f != null && f.FieldType == typeof(bool)) try { return (bool)f.GetValue(c); } catch { }
            }
            return true;
        }
        private IEnumerator LbTestSchoolRoutine(EnvironmentController environment)
        {
            yield return null; yield return null;
            if (environment == null) yield break;
            lbTestEc = environment;
            List<Cell> cells = environment.lights != null ? new List<Cell>(environment.lights) : new List<Cell>();
            lbTestOriginalLights.Clear();
            foreach (Cell c in cells) if (c != null && !lbTestOriginalLights.ContainsKey(c)) lbTestOriginalLights[c] = ReadCellLight(c);
            for (int i = cells.Count - 1; i > 0; i--)
            { int j = Random.Range(0, i + 1); Cell tmp = cells[i]; cells[i] = cells[j]; cells[j] = tmp; }
            for (int i = 0; i < cells.Count; i += 2) try { if (cells[i] != null) cells[i].SetLight(false); } catch { }
            float cycle = 10f;
            while (IsLbTestSchoolActive && !IsLightsOutActive && environment != null)
            {
                cycle -= Time.deltaTime * Mathf.Max(.01f, environment.EnvironmentTimeScale);
                if (cycle <= 0f)
                {
                    cycle += 10f;
                    foreach (Cell c in cells) try { if (c != null) c.SetLight(false); } catch { }
                    float wait = 1f;
                    while (wait > 0f && IsLbTestSchoolActive)
                    { wait -= Time.deltaTime * Mathf.Max(.01f, environment.EnvironmentTimeScale); yield return null; }
                    if (!IsLbTestSchoolActive) break;
                    foreach (Cell c in cells) try { if (c != null) c.SetLight(Random.Range(0, 2) > 0); } catch { }
                }
                yield return null;
            }
            RestoreLbTestLights(); lbTestRoutine = null;
        }
        private void RestoreLbTestLights()
        {
            foreach (KeyValuePair<Cell, bool> kv in lbTestOriginalLights)
                if (kv.Key != null) try { kv.Key.SetLight(kv.Value); } catch { }
            try
            {
                if (lbTestEc != null) { lbTestEc.UpdateQueuedLightChanges(); lbTestEc.UpdateFog(); }
            }
            catch { }
            lbTestOriginalLights.Clear(); lbTestEc = null;
        }

        public void ActivateSchoolShuffle()
        {
            PopCharSpr(); PopItmSpr(); PopAud(); AttachShufflers(); FSwapNpc(); FSwapItm();
            FShufTMP(); ShuffleControls(); CreateLapsHud();

            ApplyCurrentLapSpeedBoost(); UpdateLapsHud();
            if (lapFadeOutPending) lapFadeOutPending = false;
        }
        public string GetShuffledString(string original)
        {
            if (string.IsNullOrEmpty(original) || original.Trim().Length <= 1 || !IsGameActive()
                || (IsLevelReady && IsPaused())) return original;
            if (strPool.Count <= 40) LoadSubs();
            if (strMap.TryGetValue(original, out string mapped)) return mapped;
            if (wordPool.Count == 0) BuildWP();
            string[] words = original.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || wordPool.Count == 0) return original;
            for (int i = 0; i < words.Length; i++) words[i] = wordPool[Random.Range(0, wordPool.Count)];
            mapped = string.Join(" ", words); strMap[original] = mapped; return mapped;
        }
        public Sprite GetShuffledCharacterSprite(Sprite original, NPC src = null)
        {
            if (original == null || src == null) return original;
            int id = src.GetInstanceID(); Character own = src.Character; EnsureInstanceVisual(id, own);
            long key = ((long)id << 32) | (uint)original.GetInstanceID();
            if (instSprC.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            if (!instVis.TryGetValue(id, out Character target) || own == target)
            { instSprC[key] = original; return original; }

            if (sprOwn.TryGetValue(original, out Character actualOwner) && actualOwner == target)
            { instSprC[key] = original; return original; }
            if (!pfxSpr.TryGetValue(own, out List<Sprite> ownSprites)
                || !pfxSpr.TryGetValue(target, out List<Sprite> targetSprites)
                || ownSprites.Count == 0 || targetSprites.Count == 0)
            { instSprC[key] = original; return original; }
            int index = ownSprites.IndexOf(original);
            if (index < 0)
            {

                if (sprOwn.TryGetValue(original, out actualOwner) && actualOwner != own)
                { instSprC[key] = original; return original; }
                ownSprites.Add(original); sprOwn[original] = own;
                ownSprites.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                index = ownSprites.IndexOf(original);
            }
            Sprite result = targetSprites[index % targetSprites.Count]; instSprC[key] = result; return result;
        }
        public Sprite GetShuffledItemSprite(Sprite original)
        {
            if (original == null) return null;
            if (itmMappedValues.Contains(original)) return original;
            if (itmSM.TryGetValue(original, out Sprite mapped) && mapped != null) return mapped;
            if (!itmSP.Contains(original)) itmSP.Add(original);
            if (itmSP.Count == 0) return original;
            mapped = NormSpr(itmSP[Random.Range(0, itmSP.Count)], original);
            itmSM[original] = mapped;
            if (mapped != null) itmMappedValues.Add(mapped);
            return mapped;
        }
        private Sprite NormSpr(Sprite ns, Sprite os)
        {
            if (ns == null || os == null) return ns;
            try
            {
                float ow = os.rect.width / os.pixelsPerUnit, oh = os.rect.height / os.pixelsPerUnit;
                float nw = ns.rect.width / ns.pixelsPerUnit, nh = ns.rect.height / ns.pixelsPerUnit;
                if (ow <= 0f || oh <= 0f || nw <= 0f || nh <= 0f) return ns;
                float scale = Mathf.Max(nw / ow, nh / oh);
                if (scale > .9f && scale < 1.1f) return ns;
                Sprite result = Sprite.Create(ns.texture, ns.rect, new Vector2(.5f, .5f), ns.pixelsPerUnit * scale);
                result.name = ns.name + "_n"; return result;
            }
            catch { return ns; }
        }
        public AudioClip GetShuffledAudioClip(AudioClip original)
        {
            if (original == null) return null;
            string n = original.name.ToLowerInvariant();
            if (n.Contains("elv_buzz") || n.Contains("pause") || n.Contains("menu") || n.Contains("click")
                || n.Contains("hover") || n.Contains("select") || n.Contains("cursor") || !IsLevelReady || IsPaused())
                return original;
            if (audM.TryGetValue(original, out AudioClip mapped) && mapped != null) return mapped;
            if (!audP.Contains(original)) audP.Add(original);
            if (audP.Count == 0) return original;
            mapped = audP[Random.Range(0, audP.Count)]; audM[original] = mapped; return mapped;
        }


        public void ShuffleEventProperties(RandomEvent e)
        {
            if (e == null || !IsEventPropsShuffleActive) return;
            int temp = Mathf.Clamp(KnoxumsChaosModePlugin.PropShuffleTemperatureConfig.Value, 1, 15);
            float v = temp / 15f;
            LogEgg("event", e.Type.ToString(), "T=" + temp);
            try
            {
                FieldInfo min = R.Field(e, "minEventTime"), max = R.Field(e, "maxEventTime"), time = R.Field(e, "eventTime");
                if (min != null && max != null)
                {
                    float a = (float)min.GetValue(e) * Random.Range(Mathf.Max(.1f, 1f - v), 1f + v);
                    float b = (float)max.GetValue(e) * Random.Range(Mathf.Max(.2f, 1f - v), 1f + v * 2.5f);
                    if (b < a) { float t = a; a = b; b = t; }
                    min.SetValue(e, a); max.SetValue(e, b); if (time != null) time.SetValue(e, Random.Range(a, b));
                }
            }
            catch { }
        }

        public void ShuffleItemPositions()
        {
            if (!IsItemsPlaceShuffleActive) return;
            List<Pickup> list = FindObjectsOfType<Pickup>()
                .Where(p => p != null && p.gameObject.activeInHierarchy && !IsShopTransform(p.transform)).ToList();
            if (list.Count < 2) return;
            List<Vector3> positions = list.Select(p => p.transform.position).ToList();
            ShuffleList(positions);
            for (int i = 0; i < list.Count; i++) list[i].transform.position = positions[i];
            Physics.SyncTransforms();
            try { Singleton<BaseGameManager>.Instance?.Ec?.map?.UpdateIcons(); } catch { }
        }

        public void ShuffleCharPositions()
        {
            if (!IsCharPlaceShuffleActive) return;
            List<NPC> list = FindObjectsOfType<NPC>()
                .Where(n => n != null && n.Character != Character.Chalkles && n.gameObject.activeInHierarchy).ToList();
            if (list.Count < 2) return;
            List<Vector3> positions = list.Select(n => n.transform.position).ToList();
            ShuffleList(positions);
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 p = positions[i];
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas)) p = hit.position;
                NavMeshAgent agent = list[i].GetComponent<NavMeshAgent>() ?? list[i].GetComponentInChildren<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(p); else list[i].transform.position = p;
                Navigator nav = list[i].GetComponentInChildren<Navigator>();
                try { nav?.ClearDestination(); } catch { }
            }
            Physics.SyncTransforms();
        }
        private static void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            { int j = Random.Range(0, i + 1); T t = list[i]; list[i] = list[j]; list[j] = t; }
        }

        public void ApplyBuildersError(LevelBuilder b)
        {
            if (b == null || b.ld == null) return;
            LevelGenerationParameters ld = b.ld;
            if (!IsBuildersErrorActive) { RestoreBuildersError(ld); return; }
            if (beApplied && beLd != null) RestoreBuildersError(beLd);
            if (beLd != ld || beSTH == null)
            {
                beMinSz = ld.minSize; beMaxSz = ld.maxSize; beMinPl = ld.minPlots; beMaxPl = ld.maxPlots;
                beMinHR = ld.minHallsToRemove; beMaxHR = ld.maxHallsToRemove; beMinRH = ld.minReplacementHalls; beMaxRH = ld.maxReplacementHalls;
                beBTC = ld.bridgeTurnChance; beATC = ld.additionTurnChance; beDEB = ld.deadEndBuffer;
                beCW = ld.centerWeightMultiplier; beDV = ld.dijkstraWeightValueMultiplier; beDP = ld.dijkstraWeightPower;
                beEDC = ld.extraDoorChance; beHP = ld.hallPriorityDampening; beEC = ld.exitCount;
                beMinSR = ld.minSpecialRooms; beMaxSR = ld.maxSpecialRooms; beMaxIV = ld.maxItemValue;
                beMinEv = ld.minEvents; beMaxEv = ld.maxEvents; beIEG = ld.initialEventGap;
                beMinEG = ld.minEventGap; beMaxEG = ld.maxEventGap; beMaxLD = ld.maxLightDistance;
                beStdLS = ld.standardLightStrength; bePC = ld.posterChance;
                beSTH = ld.roomGroup.Select(x => x.stickToHallChance).ToArray(); beLd = ld;
            }
            ld.maxEvents = beMaxEv + 2; ld.minEvents = Mathf.Min(beMinEv + 1, ld.maxEvents);
            ld.initialEventGap = Mathf.Max(15f, beIEG * .7f); ld.minEventGap = Mathf.Max(20f, beMinEG * .75f);
            ld.maxEventGap = Mathf.Max(35f, beMaxEG * .85f); ld.extraDoorChance = Mathf.Min(.35f, beEDC + .05f);
            beApplied = true;
        }
        public void RestoreBuildersError(LevelGenerationParameters ld)
        {
            if (!beApplied || ld == null || beLd != ld) return;
            ld.minSize = beMinSz; ld.maxSize = beMaxSz; ld.minPlots = beMinPl; ld.maxPlots = beMaxPl;
            ld.minHallsToRemove = beMinHR; ld.maxHallsToRemove = beMaxHR; ld.minReplacementHalls = beMinRH; ld.maxReplacementHalls = beMaxRH;
            ld.bridgeTurnChance = beBTC; ld.additionTurnChance = beATC; ld.deadEndBuffer = beDEB;
            ld.centerWeightMultiplier = beCW; ld.dijkstraWeightValueMultiplier = beDV; ld.dijkstraWeightPower = beDP;
            ld.extraDoorChance = beEDC; ld.hallPriorityDampening = beHP; ld.exitCount = beEC;
            ld.minSpecialRooms = beMinSR; ld.maxSpecialRooms = beMaxSR; ld.maxItemValue = beMaxIV;
            ld.minEvents = beMinEv; ld.maxEvents = beMaxEv; ld.initialEventGap = beIEG; ld.minEventGap = beMinEG; ld.maxEventGap = beMaxEG;
            ld.maxLightDistance = beMaxLD; ld.standardLightStrength = beStdLS; ld.posterChance = bePC;
            if (beSTH != null) for (int i = 0; i < Mathf.Min(beSTH.Length, ld.roomGroup.Count); i++) ld.roomGroup[i].stickToHallChance = beSTH[i];
            beApplied = false;
        }

        private byte[] LoadEmbeddedBytes(string fileName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string name = asm.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using (Stream s = asm.GetManifestResourceStream(name))
                { if (s == null) return null; byte[] data = new byte[s.Length]; s.Read(data, 0, data.Length); return data; }
            }
            catch { return null; }
        }
        private Sprite LoadEmbeddedSprite(string fileName)
        {
            try
            {
                byte[] data = LoadEmbeddedBytes(fileName); if (data == null) return null;
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                if (!tex.LoadImage(data)) return null;
                Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f); s.name = fileName; return s;
            }
            catch { return null; }
        }
        private static void StretchOverlay(GameObject obj)
        {
            RectTransform r = obj.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
        private void EnsureWhiteFlashOverlay()
        {
            try
            {
                Canvas canvas = Singleton<CoreGameManager>.Instance?.GetHud(0)?.Canvas(); if (canvas == null) return;
                if (lapsBlackObj == null)
                {
                    lapsBlackObj = new GameObject("LapsBlack", typeof(RectTransform), typeof(Image));
                    lapsBlackObj.transform.SetParent(canvas.transform, false); StretchOverlay(lapsBlackObj);
                    lapsBlackImage = lapsBlackObj.GetComponent<Image>(); lapsBlackImage.raycastTarget = false;
                }
                if (lapsFlashObj == null)
                {
                    lapsFlashObj = new GameObject("LapsFlash", typeof(RectTransform), typeof(Image));
                    lapsFlashObj.transform.SetParent(canvas.transform, false); StretchOverlay(lapsFlashObj);
                    lapsFlashImage = lapsFlashObj.GetComponent<Image>(); lapsFlashImage.raycastTarget = false;
                }
                lapsBlackImage.color = new Color(0, 0, 0, 0); lapsFlashImage.color = new Color(1, 1, 1, 0);
                lapsBlackObj.transform.SetAsLastSibling(); lapsFlashObj.transform.SetAsLastSibling();
            }
            catch { }
        }
        public void PrepareCowardCatchFlash()
        {
            EnsureWhiteFlashOverlay();
            if (lapsBlackImage != null) lapsBlackImage.color = Color.black;
            if (lapsFlashImage != null) lapsFlashImage.color = new Color(1, 1, 1, 0);
        }

        public void ShowPitstopChaosReminder()
        {
            if (!IsAnyChaosOptionActive()) return;
            if (pitstopReminderRoutine != null)
            {
                try { StopCoroutine(pitstopReminderRoutine); } catch { }
            }
            if (pitstopReminderObject != null) Destroy(pitstopReminderObject);
            pitstopReminderRoutine = StartCoroutine(PitstopChaosReminderRoutine());
        }

        private IEnumerator PitstopChaosReminderRoutine()
        {
            Canvas canvas = null;
            float findTime = 3f;
            while (findTime > 0f && canvas == null)
            {
                try { canvas = Singleton<CoreGameManager>.Instance?.GetHud(0)?.Canvas(); }
                catch { }
                if (canvas != null) break;
                findTime -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (canvas == null) { pitstopReminderRoutine = null; yield break; }

            pitstopReminderObject = new GameObject("PitstopChaosReminder",
                typeof(RectTransform), typeof(CanvasGroup));
            pitstopReminderObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = pitstopReminderObject.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(.06f, .28f);
            rect.anchorMax = new Vector2(.94f, .50f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            HudManager reminderHud = null;
            try { reminderHud = Singleton<CoreGameManager>.Instance?.GetHud(0); } catch { }

            TMP_FontAsset font = GetComicSansFont(reminderHud);
            string reminder = "<b>JUST A REMINDER!</b> All applied chaos features are intended to disable in the pitstop, they are only working in the school!";
            Color[] layerColors = { Color.black, Color.yellow };
            Vector2[] layerOffsets = { new Vector2(2f, -2f), Vector2.zero };
            string[] layerNames = { "ReminderShadow", "ReminderText" };
            for (int i = 0; i < layerColors.Length; i++)
            {
                GameObject layer = new GameObject(layerNames[i], typeof(RectTransform));
                layer.transform.SetParent(pitstopReminderObject.transform, false);
                RectTransform layerRect = layer.GetComponent<RectTransform>();
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.offsetMin = layerOffsets[i];
                layerRect.offsetMax = layerOffsets[i];
                TextMeshProUGUI text = layer.AddComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.text = reminder;
                text.fontSize = 12f;
                text.color = layerColors[i];
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
            }
            CanvasGroup group = pitstopReminderObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            pitstopReminderObject.transform.SetAsLastSibling();

            float hold = 3f;
            while (hold > 0f && pitstopReminderObject != null)
            { hold -= Time.unscaledDeltaTime; yield return null; }

            float fade = 2f;
            while (fade > 0f && pitstopReminderObject != null)
            {
                fade -= Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(fade / 2f);
                yield return null;
            }
            if (pitstopReminderObject != null) Destroy(pitstopReminderObject);
            pitstopReminderObject = null;
            pitstopReminderRoutine = null;
        }

        private void StopPitstopChaosReminder()
        {
            if (pitstopReminderRoutine != null)
            {
                try { StopCoroutine(pitstopReminderRoutine); } catch { }
                pitstopReminderRoutine = null;
            }
            if (pitstopReminderObject != null) Destroy(pitstopReminderObject);
            pitstopReminderObject = null;
        }

        public void CreateLapsHud()
        {
            if (!IsLapsActive || !IsLevelReady || lapsHudObj != null || IsPitstopActive()) return;
            try
            {
                HudManager hud = Singleton<CoreGameManager>.Instance?.GetHud(0); Canvas canvas = hud?.Canvas(); if (canvas == null) return;
                lapsHudObj = new GameObject("LapsHUD", typeof(RectTransform)); lapsHudObj.transform.SetParent(canvas.transform, false);
                RectTransform r = lapsHudObj.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(10, -55); r.sizeDelta = new Vector2(100, 26);
                if (arrowsSprite == null) arrowsSprite = LoadEmbeddedSprite("ArrowsSprite.png");
                if (arrowsSprite != null)
                {
                    GameObject icon = new GameObject("LapsIcon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(lapsHudObj.transform, false);
                    lapsHudIcon = icon.GetComponent<Image>(); lapsHudIcon.sprite = arrowsSprite; lapsHudIcon.raycastTarget = false; lapsHudIcon.preserveAspect = true;
                    RectTransform ir = icon.GetComponent<RectTransform>(); ir.anchorMin = ir.anchorMax = new Vector2(0, .5f); ir.pivot = new Vector2(0, .5f); ir.sizeDelta = new Vector2(22, 22);
                }
                GameObject text = new GameObject("LapsText", typeof(RectTransform)); text.transform.SetParent(lapsHudObj.transform, false);
                lapsHudText = text.AddComponent<TextMeshProUGUI>(); lapsHudText.fontSize = 18; lapsHudText.color = Color.black;
                lapsHudText.alignment = TextAlignmentOptions.Left; lapsHudText.enableWordWrapping = false; lapsHudText.raycastTarget = false;
                TMP_FontAsset font = GetComicSansFont(hud); if (font != null) lapsHudText.font = font;
                RectTransform tr = text.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(26, 0); tr.offsetMax = Vector2.zero;
                EnsureWhiteFlashOverlay(); UpdateLapsHud(); EnsureLapsHudBehindTV(hud, canvas);
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("CreateLapsHud: " + ex.Message); }
        }
        private static TMP_FontAsset cachedComicSansFont;
        internal TMP_FontAsset GetComicSansFont(HudManager hud = null)
        {
            if (cachedComicSansFont != null) return cachedComicSansFont;
            try
            {
                if (hud != null) cachedComicSansFont = hud.GetComponentsInChildren<TMP_Text>(true).Select(x => x.font).FirstOrDefault(x => x != null);
                if (cachedComicSansFont == null) cachedComicSansFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x != null && x.name.ToLowerInvariant().Contains("comic"));
            }
            catch { }
            return cachedComicSansFont;
        }
        private static TMP_Text MkWatermarkLayer(Transform parent, TMP_FontAsset font, string body, Color color, Vector2 offset)
        {
            GameObject go = new GameObject(color.r > .5f ? "WatermarkText" : "WatermarkOutline", typeof(RectTransform)); go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>(); t.fontSize = 9.5f; t.lineSpacing = -8; t.alignment = TextAlignmentOptions.BottomRight;
            t.enableWordWrapping = false; t.raycastTarget = false; t.color = color; t.text = body; if (font != null) t.font = font;
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = offset; return t;
        }
        public void CreateBetaWatermarkHud()
        {
            if (betaWatermarkObj != null || IsPitstopActive()) return;
            try
            {
                HudManager hud = Singleton<CoreGameManager>.Instance?.GetHud(0); Canvas canvas = hud?.Canvas(); if (canvas == null) return;
                betaWatermarkObj = new GameObject("BetaWatermarkHUD", typeof(RectTransform)); betaWatermarkObj.transform.SetParent(canvas.transform, false);
                RectTransform r = betaWatermarkObj.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(1, 0);
                r.anchoredPosition = new Vector2(-4, 4); r.sizeDelta = new Vector2(400, 50);
                string body = "<line-height=100%>Knoxum's Chaos Mode\nPUBLIC BETA</line-height>"; TMP_FontAsset font = GetComicSansFont(hud);
                Vector2[] o = { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
                foreach (Vector2 x in o) MkWatermarkLayer(betaWatermarkObj.transform, font, body, Color.black, x);
                betaWatermarkText = MkWatermarkLayer(betaWatermarkObj.transform, font, body, Color.white, Vector2.zero);
                betaWatermarkObj.SetActive(IsAnyChaosOptionActive()); EnsureWatermarkBehindTV(hud, canvas);
            }
            catch { }
        }
        public void InjectBetaWatermarkIntoHud(HudManager hud)
        { if (betaWatermarkObj == null) CreateBetaWatermarkHud(); else { betaWatermarkObj.transform.SetParent(hud.Canvas().transform, false); EnsureWatermarkBehindTV(hud, hud.Canvas()); } }
        public void InjectLapsIntoHud(HudManager hud)
        { if (lapsHudObj == null) CreateLapsHud(); else { lapsHudObj.transform.SetParent(hud.Canvas().transform, false); EnsureLapsHudBehindTV(hud, hud.Canvas()); } }
        private void EnsureWatermarkBehindTV(HudManager hud, Canvas canvas) { if (betaWatermarkObj != null) betaWatermarkObj.transform.SetAsFirstSibling(); }
        private void EnsureLapsHudBehindTV(HudManager hud, Canvas canvas) { if (lapsHudObj != null) lapsHudObj.transform.SetAsFirstSibling(); }
        public void SyncBetaWatermarkWithHud()
        {
            if (!IsLevelReady || !IsInGame()) return;
            if (betaWatermarkObj == null && (watermarkRetryTimer -= Time.deltaTime) <= 0f) { watermarkRetryTimer = 1f; CreateBetaWatermarkHud(); }
            if (betaWatermarkObj != null) betaWatermarkObj.SetActive(!IsPitstopActive() && IsAnyChaosOptionActive());
        }
        public void DestroyBetaWatermarkHud() { if (betaWatermarkObj != null) Destroy(betaWatermarkObj); betaWatermarkObj = null; betaWatermarkText = null; }
        public void SyncLapsHudWithNotebooks()
        { if (!IsLapsActive || !IsLevelReady) return; if (lapsHudObj == null) CreateLapsHud(); else lapsHudObj.SetActive(!IsPitstopActive()); }
        private int pitstopCheckFrame = -1; private bool pitstopCheckValue;
        private bool IsPitstopActive()
        {
            if (pitstopCheckFrame == Time.frameCount) return pitstopCheckValue;
            pitstopCheckFrame = Time.frameCount; pitstopCheckValue = ElevatorUnlockService.IsPitstopManager(Singleton<BaseGameManager>.Instance); return pitstopCheckValue;
        }
        public void UpdateLapsHud()
        {
            if (lapsHudText == null) return;

            lapsHudText.text = InfiniteLaps
                ? CurrentLap.ToString()
                : CurrentLap + "/" + LapsCount;
        }
        public void DestroyLapsHud()
        {
            if (lapsHudObj != null) Destroy(lapsHudObj); if (lapsFlashObj != null) Destroy(lapsFlashObj); if (lapsBlackObj != null) Destroy(lapsBlackObj);
            lapsHudObj = null; lapsHudIcon = null; lapsHudText = null; lapsFlashObj = null; lapsFlashImage = null; lapsBlackObj = null; lapsBlackImage = null;
        }
        public void TriggerLapsFadeOut(float duration)
        {
            if (lapsFlashImage != null) StartCoroutine(LapsFadeOutRoutine(duration));
        }
        private IEnumerator LapsFadeOutRoutine(float duration)
        {
            duration = Mathf.Max(.01f, duration);
            Color color = Color.white;
            color.a = 1f;
            if (lapsFlashImage != null) lapsFlashImage.color = color;
            if (lapsFlashObj != null) lapsFlashObj.transform.SetAsLastSibling();
            float remaining = duration;
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(remaining / duration);
                if (lapsFlashImage != null) lapsFlashImage.color = color;
                yield return null;
            }
            color.a = 0f;
            if (lapsFlashImage != null) lapsFlashImage.color = color;
        }

        private void CaptureBasePlayerSpeed()
        {
            if (playerBaseSpeedCaptured) return; PlayerManager p = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
            if (p == null || p.plm == null) return; playerBaseWalkSpeed = p.plm.walkSpeed; playerBaseRunSpeed = p.plm.runSpeed; playerBaseSpeedCaptured = true;
        }
        private float LapMultiplier => IsLapsActive ? 1f + (CurrentLap - 1) * .15f : 1f;
        public void ApplyCurrentLapSpeedBoost() { ApplyCurrentLapSpeedBoost(null); }
        public void ApplyCurrentLapSpeedBoost(NPC only)
        {

            if (!IsLapsActive || CurrentLap <= 1) return;

            CaptureBasePlayerSpeed();
            if (only == null)
            {
                PlayerManager p = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (p != null && p.plm != null && playerBaseSpeedCaptured)
                {
                    p.plm.walkSpeed = playerBaseWalkSpeed * LapMultiplier;
                    p.plm.runSpeed = playerBaseRunSpeed * LapMultiplier;
                }
                foreach (NPC n in FindObjectsOfType<NPC>())
                    ApplyNpcSpeed(n, NotebooksCollectedCount, true);
            }
            else
            {
                ApplyNpcSpeed(only, NotebooksCollectedCount, true);
            }
        }


        public void StartInstantNewLap(BaseGameManager bgm, bool nativeFinishStarted = false)
        {
            if (bgm == null || bgm.Ec == null) return;
            if (floorExitToPitstopCommitted)
            {
                KnoxumsChaosModePlugin.Log.LogWarning(
                    "StartInstantNewLap blocked: floor already committed to pitstop.");
                return;
            }
            if (!ShouldStartNewLap())
            {
                KnoxumsChaosModePlugin.Log.LogWarning(
                    "StartInstantNewLap blocked: lap " + CurrentLap + "/" + LapsCount + ".");
                return;
            }
            if (lapTransitionInProgress) return;

            lapTransitionInProgress = true;
            skipElevatorOnLap = true;
            try { bgm.StopAllCoroutines(); } catch { }
            activeLapCoroutine = StartCoroutine(
                InstantLapTransitionCoroutine(bgm, nativeFinishStarted));
        }

        private IEnumerator InstantLapTransitionCoroutine(
            BaseGameManager bgm, bool nativeFinishStarted)
        {
            if (bgm == null || bgm.Ec == null)
            {
                activeLapCoroutine = null;
                lapTransitionInProgress = false;
                skipElevatorOnLap = false;
                yield break;
            }

            EnsureWhiteFlashOverlay();
            if (lapsFlashObj != null) lapsFlashObj.transform.SetAsLastSibling();
            if (lapsFlashImage != null)
            {
                float fade = 0f;
                while (fade < 1f)
                {
                    fade += Time.unscaledDeltaTime;
                    lapsFlashImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(fade));
                    yield return null;
                }
                lapsFlashImage.color = Color.white;
            }

            CurrentLap++;
            pendingLap = CurrentLap;
            MarkLapsUsedThisFloor();
            KnoxumsChaosModePlugin.Log.LogInfo(
                "Instant Lap Start: " + CurrentLap
                + (InfiniteLaps ? " (Endless)" : "/" + LapsCount));

            NotebooksCollectedCount = 0;
            chaosInitialSpawnDone = false;
            floorExitToPitstopCommitted = false;
            UpdateLapsHud();
            ApplyCurrentLapSpeedBoost();

            try
            {
                R.Set(bgm, "foundNotebooks", 0);
                R.Set(bgm, "allNotebooksFound", false);
                HudManager hud = Singleton<CoreGameManager>.Instance?.GetHud(0);
                if (hud != null)
                    hud.UpdateNotebookText(0, "0/" + bgm.Ec.notebookTotal, false);
            }
            catch { }

            foreach (Notebook notebook in UnityEngine.Object.FindObjectsOfType<Notebook>(true))
            {
                if (notebook == null) continue;
                try { notebook.gameObject.SetActive(true); } catch { }
                try { notebook.Hide(false); } catch { }
                try { if (notebook.activity != null) notebook.activity.InstantReset(); } catch { }
                R.Set(notebook, "collected", false);
                R.Set(notebook, "hidden", false);
            }

            try
            {
                RegisterOriginalPickups();
                RespawnAllItemsOnFloor(bgm.Ec);
                UpdateMiniMapIcons(bgm.Ec);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Lap item reset error: " + ex);
            }


            StopAllTapes();
            try
            {
                StopAllActiveEvents(bgm.Ec);
                ClearTransientLapObjects();
                ClearAllCharactersFromFloor(bgm);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Lap cleanup error: " + ex);
            }
            StopAllTapes();

            ResetElevatorsToFloorStart(bgm);

            PlayerManager pm = null;
            try
            {
                pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (pm != null)
                {
                    pm.Teleport(bgm.Ec.spawnPoint);
                    pm.transform.rotation = bgm.Ec.spawnRotation;
                    if (pm.plm != null) pm.plm.AddStamina(pm.plm.staminaMax, true);


                    if (nativeFinishStarted) UnlockPlayerMovement(pm, null);
                    Physics.SyncTransforms();
                    BaldiRampagePatches.SnapCameraToPlayer(
                        Singleton<CoreGameManager>.Instance?.GetCamera(0), pm);
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Lap player teleport error: " + ex);
            }


            yield return null;
            StopAllTapes();
            yield return new WaitForFixedUpdate();

            Elevator spawnElevator = FindSpawnElevatorForLap(bgm.Ec, pm);
            if (spawnElevator != null)
                RestartNativeWaitToExitSpawn(bgm, spawnElevator);
            else
                KnoxumsChaosModePlugin.Log.LogWarning(
                    "Lap start: spawn elevator not found for WaitToExitSpawn.");


            StartFloorIntro(bgm);

            if (lapsFlashImage != null)
                yield return StartCoroutine(LapsFadeOutRoutine(1f));

            activeLapCoroutine = null;
            lapTransitionInProgress = false;
            skipElevatorOnLap = false;
        }
        public void StartCowardCaughtRestart(BaseGameManager bgm)
        { if (bgm == null || floorExitToPitstopCommitted) return; CommitFloorExitToPitstop(); BaldiRampagePatches.ResetCowardRoundFlags(); bgm.LoadNextLevel(); }

        private void ResetElevatorsToFloorStart(BaseGameManager bgm)
        {
            ElevatorUnlockService.ResetForNewFloorOrLap();
            if (bgm == null || bgm.Ec == null) return;

            R.Set(bgm, "foundNotebooks", 0);
            R.Set(bgm, "allNotebooksFound", false);
            R.Set(bgm, "elevatorsToClose", 0);
            R.Set(bgm, "elevatorsClosed", 0);
            R.Set(bgm, "waitToExitSpawn", null);


            List<Elevator> elevators = ElevatorUnlockService.GetElevators(bgm.Ec);
            if (elevators.Count == 0)
            {
                KnoxumsChaosModePlugin.Log.LogWarning("Lap reset: no elevators found.");
                return;
            }
            RepairElevatorLists(bgm.Ec, elevators);

            Elevator spawn = FindSpawnElevatorForLap(bgm.Ec);
            foreach (Elevator elevator in elevators)
            {
                if (elevator == null) continue;
                bool isSpawn = elevator == spawn;
                SetElevatorIsSpawnSafe(elevator, isSpawn);
                R.Set(elevator, "open", false);
                R.Set(elevator, "doorIsOpen", isSpawn);
                try { elevator.OpenDoor(isSpawn); } catch { }
                try
                {
                    MeshCollider gate = R.Get<MeshCollider>(elevator, "gateCollider", null);
                    if (gate != null) gate.enabled = !isSpawn;
                }
                catch { }
                try
                {
                    if (elevator.ColliderGroup != null)
                        elevator.ColliderGroup.Enable(isSpawn);
                }
                catch { }
                try
                {
                    ColliderGroup inside = ElevatorUnlockService.GetInsideCollider(elevator);
                    if (inside != null) inside.Enable(isSpawn);
                }
                catch { }
                if (isSpawn) ElevatorUnlockService.UnlockSpawnElevatorButton(elevator);
            }
            try { Physics.SyncTransforms(); } catch { }
        }

        private void RepairElevatorLists(EnvironmentController ec, List<Elevator> snapshot)
        {
            if (ec == null || snapshot == null || snapshot.Count == 0) return;
            try
            {
                List<Elevator> ecList = ec.Elevators;
                if (ecList != null)
                {
                    ecList.Clear();
                    for (int i = 0; i < snapshot.Count; i++)
                        if (snapshot[i] != null) ecList.Add(snapshot[i]);
                }
            }
            catch { }

            HashSet<object> repairedManagers = new HashSet<object>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                Elevator elevator = snapshot[i];
                if (elevator == null) continue;
                object manager = R.Get<object>(elevator, "manager", null);
                if (manager == null || !repairedManagers.Add(manager)) continue;
                try
                {
                    FieldInfo field = R.Field(manager, "elevators");
                    System.Collections.IList managerList =
                        field != null ? field.GetValue(manager) as System.Collections.IList : null;
                    if (managerList == null) continue;
                    managerList.Clear();
                    for (int j = 0; j < snapshot.Count; j++)
                        if (snapshot[j] != null) managerList.Add(snapshot[j]);
                }
                catch (Exception ex)
                {
                    KnoxumsChaosModePlugin.Log.LogWarning(
                        "Lap reset: elevator manager list repair failed: " + ex.Message);
                }
            }
        }
        public void RegisterOriginalPickups()
        {
            foreach (Pickup p in UnityEngine.Object.FindObjectsOfType<Pickup>(true))
            {
                if (p == null || IsShopTransform(p.transform)) continue; ItemObject io = R.Get<ItemObject>(p, "item", null);
                if (io != null && io.itemType != Items.None && (io.itemSpriteLarge != null || io.itemSpriteSmall != null)) originalPickupItems[p.GetInstanceID()] = io;
            }
        }
        private bool GetElevatorIsSpawnSafe(Elevator e)
        { try { PropertyInfo p = e.GetType().GetProperty("IsSpawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (p != null) return (bool)p.GetValue(e, null); } catch { } return R.Get<bool>(e, "isSpawn", false); }
        private void SetElevatorIsSpawnSafe(Elevator e, bool v)
        { try { PropertyInfo p = e.GetType().GetProperty("IsSpawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (p != null && p.CanWrite) { p.SetValue(e, v, null); return; } } catch { } R.Set(e, "isSpawn", v); }
        private Elevator FindSpawnElevatorForLap(EnvironmentController ec, PlayerManager pm = null)
        {
            List<Elevator> list = ElevatorUnlockService.GetElevators(ec); if (list.Count == 0) return null;
            Elevator found = list.FirstOrDefault(GetElevatorIsSpawnSafe);
            if (found == null) found = list.OrderBy(e => e == null ? float.MaxValue : (e.transform.position - ec.spawnPoint).sqrMagnitude).FirstOrDefault();
            foreach (Elevator e in list) if (e != null) SetElevatorIsSpawnSafe(e, e == found); return found;
        }
        public void RespawnAllItemsOnFloor(EnvironmentController ec)
        {
            RegisterOriginalPickups(); List<ItemObject> pool = originalPickupItems.Values.Where(x => x != null && x.itemType != Items.None).Distinct().ToList();
            Dictionary<int, ItemObject> next = new Dictionary<int, ItemObject>();
            foreach (Pickup p in UnityEngine.Object.FindObjectsOfType<Pickup>(true))
            {
                if (p == null || IsShopTransform(p.transform) || !originalPickupItems.ContainsKey(p.GetInstanceID()) || pool.Count < 2) continue;
                ItemObject previous = previousLapPickupItems.TryGetValue(p.GetInstanceID(), out ItemObject x) ? x : originalPickupItems[p.GetInstanceID()];
                ItemObject item = pool.Where(y => y != previous).OrderBy(y => Random.value).FirstOrDefault();
                if (item != null) try { p.AssignItem(item); p.gameObject.SetActive(true); p.Hide(false); next[p.GetInstanceID()] = item; } catch { }
            }
            previousLapPickupItems = next;
        }
        private void RestartNativeWaitToExitSpawn(BaseGameManager bgm, Elevator spawn)
        {
            if (bgm == null) return;
            try
            {
                IEnumerator wait = WaitForPlayerExitThenExitedSpawn(bgm, spawn);
                R.Set(bgm, "waitToExitSpawn", wait);
                bgm.StartCoroutine(wait);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError(
                    "Lap reset: failed to start WaitToExitSpawn watcher: " + ex);
            }
        }
        private IEnumerator WaitForPlayerExitThenExitedSpawn(BaseGameManager bgm, Elevator spawn)
        {
            yield return new WaitForSecondsRealtime(.8f);
            if (bgm == null || bgm.Ec == null) yield break;

            Vector3 origin = bgm.Ec.spawnPoint;
            origin.y = 0f;
            float timeout = 25f;
            while (timeout > 0f)
            {
                PlayerManager player = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                Vector3 position = player != null ? player.transform.position : origin;
                position.y = 0f;
                if ((position - origin).magnitude > 12f) break;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            CloseElevatorProperly(spawn);
            InvokeExitedSpawnDirect(bgm);
        }
        private void InvokeExitedSpawnDirect(BaseGameManager bgm)
        {
            if (bgm == null) return;
            try
            {
                MethodInfo exitedSpawn = typeof(BaseGameManager).GetMethod("ExitedSpawn",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (exitedSpawn != null) exitedSpawn.Invoke(bgm, null);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError(
                    "Lap reset: ExitedSpawn invoke failed: " + ex);
            }
        }
        private void CloseElevatorProperly(Elevator elevator)
        {
            if (elevator == null) return;
            try { elevator.OpenDoor(false); } catch { }
            try
            {
                MethodInfo close = elevator.GetType().GetMethod("Close",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (close != null) close.Invoke(elevator, null);
            }
            catch { }
            R.Set(elevator, "open", false);
            R.Set(elevator, "doorIsOpen", false);
            try
            {
                MeshCollider gate = R.Get<MeshCollider>(elevator, "gateCollider", null);
                if (gate != null) gate.enabled = true;
            }
            catch { }
        }
        private static void StopTapeAudioManager(AudioManager audioManager)
        {
            if (audioManager == null) return;
            try { audioManager.FlushQueue(true); } catch { }
            try
            {
                MethodInfo setLoop = audioManager.GetType().GetMethod("SetLoop",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(bool) }, null);
                if (setLoop != null) setLoop.Invoke(audioManager, new object[] { false });
            }
            catch { }
            try
            {
                AudioSource device = R.Get<AudioSource>(audioManager, "audioDevice", null);
                if (device != null)
                {
                    device.Stop();
                    device.loop = false;
                }
            }
            catch { }
        }

        private void StopAllTapes()
        {
            try
            {
                foreach (TapePlayer tape in UnityEngine.Object.FindObjectsOfType<TapePlayer>(true))
                {
                    if (tape == null) continue;


                    string[] endNames = { "End", "Stop", "Reset", "ReInit" };
                    MethodInfo[] methods = tape.GetType().GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (int n = 0; n < endNames.Length; n++)
                    {
                        for (int m = 0; m < methods.Length; m++)
                        {
                            MethodInfo method = methods[m];
                            if (method == null || method.Name != endNames[n]
                                || method.GetParameters().Length != 0) continue;
                            try { method.Invoke(tape, null); } catch { }
                            break;
                        }
                    }

                    try { tape.StopAllCoroutines(); } catch { }
                    R.SetPossibleBoolFields(tape, false,
                        "active", "playing", "isPlaying", "on", "inserted", "looping");

                    HashSet<AudioManager> managers = new HashSet<AudioManager>();
                    try
                    {
                        AudioManager fieldManager = R.Get<AudioManager>(tape, "audMan", null);
                        if (fieldManager != null) managers.Add(fieldManager);
                        foreach (AudioManager manager in tape.GetComponentsInChildren<AudioManager>(true))
                            if (manager != null) managers.Add(manager);
                    }
                    catch { }
                    foreach (AudioManager manager in managers) StopTapeAudioManager(manager);

                    try
                    {
                        foreach (AudioSource source in tape.GetComponentsInChildren<AudioSource>(true))
                        {
                            if (source == null) continue;
                            source.Stop();
                            source.loop = false;
                        }
                    }
                    catch { }
                }

                foreach (Baldi baldi in UnityEngine.Object.FindObjectsOfType<Baldi>(true))
                {
                    BaldiRampageController controller =
                        baldi != null ? baldi.GetComponent<BaldiRampageController>() : null;
                    if (controller != null) controller.SetTapePlaying(false);
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("StopAllTapes: " + ex);
            }
        }

        private void ClearTransientLapObjects()
        {
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue; string n = mb.GetType().Name;
                if (n == "Gum" || n == "ITM_BSODA" || n == "ITM_GrapplingHook" || n == "BaldiGrappleRuntime" || n == "BaldiAppleProjectile")
                {
                    try { mb.StopAllCoroutines(); } catch { }
                    try { mb.gameObject.SetActive(false); } catch { }
                    Destroy(mb.gameObject);
                }
            }
        }
        public void ClearAllCharactersFromFloor(BaseGameManager bgm)
        {
            foreach (NPC n in UnityEngine.Object.FindObjectsOfType<NPC>(true))
            {
                if (n == null) continue; try { n.StopAllCoroutines(); n.gameObject.SetActive(false); n.Despawn(); }
                catch { try { Destroy(n.gameObject); } catch { } }
            }
            try { bgm.Ec.Npcs.Clear(); R.Set(bgm, "exitedSpawn", false); R.Set(bgm, "spawned", false); R.Set(bgm, "npcsSpawned", false); } catch { }
        }


        public void UnlockPlayerMovement(PlayerManager pm, CoreGameManager cm = null)
        {
            if (pm == null) pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
            if (cm == null) cm = Singleton<CoreGameManager>.Instance;
            try
            {
                if (pm != null)
                {
                    if (pm.plm != null)
                    {
                        pm.plm.enabled = true;
                        if (pm.plm.Entity != null)
                        {
                            R.Set(pm.plm.Entity, "freezes", 0);
                            R.Set(pm.plm.Entity, "frozen", false);
                            pm.plm.Entity.SetActive(true);
                            pm.plm.Entity.Enable(true);
                            try { pm.plm.Entity.SetFrozen(false); } catch { }
                            try { pm.plm.Entity.SetInteractionState(true); } catch { }
                            try { pm.plm.Entity.SetVisible(true); } catch { }
                        }
                    }
                    R.SetPossibleBoolFields(pm, false,
                        "lockInput", "inputLocked", "locked", "frozen",
                        "disableInput", "disableMovement", "movementLocked",
                        "inElevator", "elevatored", "playerInElevator");
                    R.SetPossibleBoolFields(pm.plm, false,
                        "lockInput", "inputLocked", "locked", "frozen",
                        "disableInput", "disableMovement", "movementLocked",
                        "inElevator", "elevatored", "playerInElevator");
                }

                R.SetPossibleBoolFields(cm, false,
                    "lockInput", "inputLocked", "locked", "gameOver", "ending",
                    "disablePause", "disableInput");
                BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
                R.SetPossibleBoolFields(bgm, false,
                    "lockInput", "inputLocked", "locked", "gameOver", "ending",
                    "disableInput", "playerInElevator");

                InputManager inputManager = FindObjectOfType<InputManager>();
                if (inputManager != null)
                {
                    R.SetPossibleBoolFields(inputManager, false,
                        "lockInput", "inputLocked", "locked", "disableInput");
                    inputManager.ActivateActionSet("InGame");
                }
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("UnlockPlayerMovement: " + ex.Message);
            }
        }
        public void StopAllActiveEvents(EnvironmentController ec)
        {
            try
            {
                if (ec != null)
                {
                    try
                    {
                        MethodInfo stopEvents = ec.GetType().GetMethod("StopEvents",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, Type.EmptyTypes, null);
                        if (stopEvents != null) stopEvents.Invoke(ec, null);
                    }
                    catch { }
                    R.Set(ec, "eventsStarted", false);
                    R.Set(ec, "surpassedGameTime", 0f);
                    R.Set(ec, "surpassedRealTime", 0f);
                    R.Set(ec, "lastTimeWarning", 0);
                }

                foreach (RandomEvent randomEvent in
                    UnityEngine.Object.FindObjectsOfType<RandomEvent>(true))
                {
                    if (randomEvent == null) continue;
                    try { randomEvent.StopAllCoroutines(); } catch { }
                    try { if (randomEvent.Active) randomEvent.End(); } catch { }
                    R.Set(randomEvent, "active", false);
                }

                if (ec != null)
                {
                    try
                    {
                        (R.Field(ec, "currentEvents")?.GetValue(ec)
                            as System.Collections.IList)?.Clear();
                        (R.Field(ec, "currentEventTypes")?.GetValue(ec)
                            as System.Collections.IList)?.Clear();
                    }
                    catch { }
                    try
                    {
                        MethodInfo resetEvents = ec.GetType().GetMethod("ResetEvents",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, Type.EmptyTypes, null);
                        if (resetEvents != null) resetEvents.Invoke(ec, null);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("StopAllActiveEvents: " + ex);
            }
        }
        public void UpdateMiniMapIcons(EnvironmentController ec) { try { ec?.map?.UpdateIcons(); } catch { } }
        public void RestoreLapAfterRestart()
        {
            if (!lapRestartPending) return;

            CurrentLap = Mathf.Max(1, pendingLap);
            lapRestartPending = false;
            lapTransitionInProgress = false;
            skipElevatorOnLap = false;
            floorExitToPitstopCommitted = false;
            skipRemainingLaps = false;
            NotebooksCollectedCount = 0;
            chaosInitialSpawnDone = false;

            KnoxumsChaosModePlugin.Log.LogInfo(
                "Lap restored after restart: " + CurrentLap
                + (InfiniteLaps ? " (Endless)" : "/" + LapsCount));
        }

        private void LoadEgg()
        { try { Directory.CreateDirectory(Path.GetDirectoryName(EggPath)); IsEggActive = File.Exists(EggPath) && File.ReadAllText(EggPath).Trim().Equals("true", StringComparison.OrdinalIgnoreCase); if (KnoxumsChaosModePlugin.EggConfig != null) KnoxumsChaosModePlugin.EggConfig.Value = IsEggActive; } catch { } }
        private void SaveEgg()
        { try { Directory.CreateDirectory(Path.GetDirectoryName(EggPath)); File.WriteAllText(EggPath, IsEggActive ? "true" : "false"); if (KnoxumsChaosModePlugin.EggConfig != null) KnoxumsChaosModePlugin.EggConfig.Value = IsEggActive; } catch { } }
        public void LogEgg(string cat, string from, string to) { if (!IsEggActive) return; List<string> l = cat == "event" ? dbgE : cat == "item" ? dbgI : dbgC; l.Add("  " + from + " -> " + to); }
        public void ClearEggLog() { dbgE.Clear(); dbgC.Clear(); dbgI.Clear(); }
        private void ShowEgg() { KnoxumsChaosModePlugin.Log.LogInfo("=== EGG ==="); }

        public void ShuffleNpcProperties(NPC n)
        { if (n == null || !IsCharPropShuffleActive) return; float v = Mathf.Clamp(KnoxumsChaosModePlugin.PropShuffleTemperatureConfig.Value, 1, 15) / 15f; RandF(n, n.GetType(), v, Mathf.Lerp(.3f, 2.5f, v)); }
        public void ShuffleItemProperties(Item item)
        { if (item == null || !IsItemPropShuffleActive) return; float v = Mathf.Clamp(KnoxumsChaosModePlugin.PropShuffleTemperatureConfig.Value, 1, 15) / 15f; RandF(item, item.GetType(), v, Mathf.Lerp(2.5f, .3f, v)); }
        private static readonly HashSet<string> EF = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {"m_CachedPtr","m_InstanceID","m_ObjectHideFlags","aggroed","endUp","currentSoundVal","detentionLevel","level","currentYtps","jumps","totalPoints","currentDisplayTime","anger","extraAnger","slapTotal","slapDistance","nextSlapDistance","initSetTime","baseSpeed","speed","maxSpeed"};
        private void RandF(object o, Type t, float v, float mul = 1f)
        {
            if (o == null || t == null) return;
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (EF.Contains(f.Name) || f.IsInitOnly) continue;
                try
                {
                    if (f.FieldType == typeof(float)) { float x = (float)f.GetValue(o); if (x != 0 && !float.IsNaN(x) && !float.IsInfinity(x)) f.SetValue(o, x * mul * Random.Range(Mathf.Max(.1f, 1 - v * .5f), 1 + v * .5f)); }
                    else if (f.FieldType == typeof(int)) { int x = (int)f.GetValue(o); if (x <= 0) continue; int y = Mathf.RoundToInt(x * mul * Random.Range(Mathf.Max(.1f, 1 - v * .5f), 1 + v * .5f)); f.SetValue(o, f.Name.ToLowerInvariant().Contains("noise") ? Mathf.Clamp(y, 0, 127) : Mathf.Max(1, y)); }
                }
                catch { }
            }
            Type b = t.BaseType; if (b != null && b != typeof(MonoBehaviour) && b != typeof(NPC) && b != typeof(Item) && b != typeof(UnityEngine.Object) && b != typeof(object)) RandF(o, b, v, mul);
        }

        public void HandleNotebookCollection(int nb)
        {
            if (!IsChaosModeActive) return;
            NotebooksCollectedCount = Mathf.Max(0, nb);
            BaseGameManager bgm = FindObjectOfType<BaseGameManager>();


            if (nb == 1)
            {
                origSpeeds.Clear();
                if (!IsLapsActive && bgm?.Ec != null && !chaosInitialSpawnDone)
                {
                    chaosInitialSpawnDone = true;
                    bgm.Ec.SpawnNPCs();
                    bgm.Ec.StartEventTimers();
                    RegLiveNpcs();
                    FSwapNpc();
                }
                else if (IsLapsActive)
                {

                    chaosInitialSpawnDone = true;
                }
                return;
            }


            NPC[] liveNpcs = FindObjectsOfType<NPC>();
            for (int i = 0; i < liveNpcs.Length; i++)
                if (liveNpcs[i] != null)
                    CaptureBaseSpeed(liveNpcs[i]);

            int target = 1;
            if (CurrentChaosMode == ChaosModeType.Chaos)
                target = nb;
            else if (CurrentChaosMode == ChaosModeType.ChaosPlus1)
                target = nb * (nb + 1) / 2;
            else if (CurrentChaosMode == ChaosModeType.DoubleChaos)
                target = (int)Mathf.Pow(2, Mathf.Min(nb - 1, 10));

            target = Mathf.Clamp(target, 1, MaxClonesPerCharacter);
            Dictionary<string, List<NPC>> groups = liveNpcs
                .Where(n => n != null)
                .GroupBy(n => n.Character.ToString())
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (List<NPC> group in groups.Values)
                for (int i = group.Count; i < target; i++)
                    CloneNPC(group[0]);

            UpdateSpeeds(nb);
        }
        private void CloneNPC(NPC orig)
        {
            try
            {

                CaptureBaseSpeed(orig);
                NPC tmpl = GetTmpl(orig); if (tmpl == null) return; Vector3 pos = GetClonePos(orig);
                NPC cl = Instantiate(tmpl, pos, orig.transform.rotation); cl.name = tmpl.name + "_Clone"; cl.ec = orig.ec;
                if (cl.ec != null && cl.ec.Npcs != null && !cl.ec.Npcs.Contains(cl)) cl.ec.Npcs.Add(cl);
                cl.Initialize();
                if (BaldiRampageConfig.IsActive && cl is Baldi)
                {
                    BaldiRampageController ctl = BaldiRampagePatches.Ctl(cl);
                    if (ctl != null)
                    {


                        ctl.SetTapePlaying(BaldiRampagePatches.IsAnyTapePlaying());
                        ctl.SetNotebooks(NotebooksCollectedCount);
                    }
                }
                NavMeshAgent a = cl.GetComponent<NavMeshAgent>() ?? cl.GetComponentInChildren<NavMeshAgent>(); if (a != null && a.enabled && a.isOnNavMesh) a.Warp(pos);
                ResetRuntimeTimers(cl); RegNpcCh(cl); SyncEvts(cl); cl.gameObject.SetActive(true);


            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Clone " + orig.Character + ": " + ex.Message); }
        }
        private Vector3 GetClonePos(NPC o)
        {
            Vector3 p = o.transform.position;
            if (CurrentCloneSpawnPoint == CloneSpawnPoint.CharPosition)
            { NPC last = FindObjectsOfType<NPC>().LastOrDefault(n => n != null && n.Character == o.Character); if (last != null) p = last.transform.position; }
            else if (o.ec != null)
            {
                FieldInfo f = R.Field(o.ec, "spawnPositions"); object v = f?.GetValue(o.ec);
                if (v is IntVector2[] a && a.Length > 0) { IntVector2 q = a[Random.Range(0, a.Length)]; p = new Vector3(q.x * 10f + 5f, o.transform.position.y, q.z * 10f + 5f); }
            }
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 8f, NavMesh.AllAreas)) p = hit.position; return p;
        }
        private NPC GetTmpl(NPC o)
        {
            if (o?.ec?.npcsToSpawn != null) { NPC p = o.ec.npcsToSpawn.FirstOrDefault(x => x != null && x.Character == o.Character); if (p != null) return p; }
            return Resources.FindObjectsOfTypeAll<NPC>().FirstOrDefault(x => x != null && !x.gameObject.scene.IsValid() && x.Character == o.Character);
        }
        private void ResetRuntimeTimers(NPC c)
        {
            string[] exact = { "timer", "currentTimer", "cooldownTimer", "attackTimer", "waitTimer", "currentCooldown" };
            foreach (FieldInfo f in c.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (f.FieldType == typeof(float) && exact.Contains(f.Name, StringComparer.OrdinalIgnoreCase)) try { f.SetValue(c, Random.Range(0f, 1f)); } catch { }
        }
        private void SyncEvts(NPC cl)
        { foreach (RandomEvent e in FindObjectsOfType<RandomEvent>()) if (e != null && e.Active) try { MethodInfo m = e.GetType().GetMethod("AffectNpc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(NPC) }, null); m?.Invoke(e, new object[] { cl }); } catch { } }

        private static bool CowardOwnsSpeed(NPC npc)
        {
            return npc is Baldi && BaldiRampageConfig.IsActive;
        }

        private void CaptureBaseSpeed(NPC n)
        {
            if (n == null || CowardOwnsSpeed(n)) return;
            string key = n.Character.ToString();
            if (origSpeeds.ContainsKey(key)) return;

            Navigator nav = n.GetComponentInChildren<Navigator>();
            float speed = 0f;
            if (nav != null)
            {
                FieldInfo sf = R.Field(nav, "speed");
                FieldInfo mf = R.Field(nav, "maxSpeed");
                try
                {

                    if (sf != null) speed = (float)sf.GetValue(nav);
                    if (speed <= .01f && mf != null) speed = (float)mf.GetValue(nav);
                }
                catch { }
            }
            if (speed <= .01f && n is Baldi baldi) speed = baldi.baseSpeed;
            if (speed <= .01f) speed = 16f;
            origSpeeds[key] = speed;
            KnoxumsChaosModePlugin.Log.LogInfo(
                "Chaos speed base: " + key + " = " + speed.ToString("F2"));
        }

        private float GetBaseSpd(NPC n)
        {
            CaptureBaseSpeed(n);
            return n != null && origSpeeds.TryGetValue(n.Character.ToString(), out float speed)
                ? speed : 16f;
        }

        private void ApplyNpcSpeed(NPC npc, int notebooks, bool exact)
        {
            if (npc == null) return;


            if (CowardOwnsSpeed(npc)) return;

            Navigator navigator = npc.GetComponentInChildren<Navigator>();
            if (navigator == null) return;

            FieldInfo speedField = R.Field(navigator, "speed");
            if (speedField == null || speedField.FieldType != typeof(float)) return;


            float target = GetBaseSpd(npc) * LapMultiplier;
            try
            {
                float current = (float)speedField.GetValue(navigator);
                if (exact || current > target)
                    speedField.SetValue(navigator, target);
            }
            catch { }


        }

        public void UpdateSpeeds(int notebooks)
        {


            if (!IsLapsActive || CurrentLap <= 1) return;
            foreach (NPC npc in FindObjectsOfType<NPC>())
                ApplyNpcSpeed(npc, notebooks, true);
        }

        private void RestoreOrigSpeeds()
        {


            origSpeeds.Clear();
        }

        public void ApplyChaosItemSpawns() { if (IsChaosModeActive) foreach (Pickup p in FindObjectsOfType<Pickup>()) ReplacePickupItem(p); }
        public void ReplacePickupItem(Pickup p)
        {
            if (p == null || p.name.Contains("ChaosModified") || IsShopTransform(p.transform)) return; ItemObject old = R.Get<ItemObject>(p, "item", null); if (old == null) return;
            float chance = CurrentChaosMode == ChaosModeType.DoubleChaos ? .7f : CurrentChaosMode == ChaosModeType.ChaosPlus1 ? .45f : .25f; if (Random.value > chance) return;
            ItemObject item = FindItm("chalk") ?? FindItm("eraser") ?? FindItm("invis"); if (item != null && item != old) try { p.AssignItem(item); p.name += "_ChaosModified"; } catch { }
        }
        private ItemObject FindItm(string key) { key = key.ToLowerInvariant(); return Resources.FindObjectsOfTypeAll<ItemObject>().FirstOrDefault(i => i != null && (i.name.ToLowerInvariant().Contains(key) || i.itemType.ToString().ToLowerInvariant().Contains(key))); }
        public void ClearMapDiscovery(BaseGameManager bgm)
        { try { Map m = bgm?.Ec?.map; if (m?.tiles == null || m.foundTiles == null) return; FieldInfo f = typeof(MapTile).GetField("found", BindingFlags.NonPublic | BindingFlags.Instance); for (int x = 0; x < m.size.x; x++) for (int z = 0; z < m.size.z; z++) { m.foundTiles[x, z] = false; MapTile t = m.tiles[x, z]; if (t != null) { f?.SetValue(t, false); t.gameObject.SetActive(false); } } } catch { } }
        public void ShuffleControls() { if (!IsCtrlMapShuffleActive || ctrlShuffled) return; List<string> list = new List<string>(shuffleActions); ShuffleList(list); for (int i = 0; i < list.Count; i++) ctrlMap[shuffleActions[i]] = list[i]; ctrlShuffled = true; }
        public string GetRemappedAction(string a) { return IsCtrlMapShuffleActive && ctrlShuffled && ctrlMap.TryGetValue(a, out string x) ? x : a; }
        public void ResetCtrlShuffle() { ctrlMap.Clear(); ctrlShuffled = false; }
        public RandomEvent FindPairEventFor(EnvironmentController ec, RandomEvent exclude)
        { try { List<RandomEvent> all = R.Field(ec, "events")?.GetValue(ec) as List<RandomEvent>; List<RandomEvent> c = all?.Where(e => e != null && e != exclude && !e.Active && e.Type != RandomEventType.TimeOut && !pairedEvts.Contains(e.GetInstanceID())).ToList(); return c != null && c.Count > 0 ? c[Random.Range(0, c.Count)] : null; } catch { return null; } }
        public void MarkEventAsPaired(RandomEvent e) { if (e != null) pairedEvts.Add(e.GetInstanceID()); }
        private static MethodInfo discoUpdateLight;
        private void UpdateDisco()
        { if ((discoT2 += Time.deltaTime) < 1f) return; discoT2 = 0; try { EnvironmentController ec = cBGM?.Ec; if (ec == null) return; if (discoUpdateLight == null) discoUpdateLight = typeof(EnvironmentController).GetMethod("UpdateLightingAtCell", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); foreach (Cell c in ec.hallLights) if (c != null) ApplyDisco(c, ec); foreach (RoomController r in ec.rooms) if (r?.lights != null) foreach (Cell c in r.lights) if (c != null) ApplyDisco(c, ec); } catch { } }
        private void ApplyDisco(Cell c, EnvironmentController ec) { Color color = new Color(Random.value, Random.value, Random.value); c.lightColor = color; try { discoUpdateLight?.Invoke(ec, new object[] { c }); } catch { } if (c.TileTransform != null) foreach (Light l in c.TileTransform.GetComponentsInChildren<Light>(true)) if (l != null) l.color = color; }
    }


    public static class BaldiRampageConfig
    {
        public static bool IsActive => (KnoxumsChaosModePlugin.IsBaldiCowardEnabledConfig?.Value ?? false)
            && !(KnoxumsChaosModePlugin.IsLapsEnabledConfig?.Value ?? false);
        public static bool CatchingPlayer;
        public static ConfigEntry<float> DietBsodaChance, BsodaChance, GrappleChance;
        public static ConfigEntry<float> ItemFarChance, ItemMidChance, ItemCloseChance;
        public static ConfigEntry<float> CloseDistance, MidDistance, MaxViewDistance;
        public static ConfigEntry<float> NotebookSlowPer, MinSpeedMult, BaseFleeSpeed;
        public static ConfigEntry<float> AppleStunMin, AppleStunMax, GrappleRange, AttackCooldown, MaxAnger;

        public static void Init(ConfigFile c)
        {
            DietBsodaChance = c.Bind("BaldiRampage", "DietBsodaChance", .75f, "Chance weight for Diet BSODA.");
            BsodaChance = c.Bind("BaldiRampage", "BsodaChance", .15f, "Chance weight for normal BSODA.");
            GrappleChance = c.Bind("BaldiRampage", "GrappleChance", .10f, "Chance weight for grappling hook.");
            ItemFarChance = c.Bind("BaldiRampage", "ItemFarChance", .25f, "Item-use chance when far.");
            ItemMidChance = c.Bind("BaldiRampage", "ItemMidChance", .67f, "Item-use chance at mid distance.");
            ItemCloseChance = c.Bind("BaldiRampage", "ItemCloseChance", 1f, "Item-use chance when close.");
            CloseDistance = c.Bind("BaldiRampage", "CloseDistance", 8f, "Close distance.");
            MidDistance = c.Bind("BaldiRampage", "MidDistance", 25f, "Mid distance.");
            MaxViewDistance = c.Bind("BaldiRampage", "MaxViewDistance", 40f, "Maximum sight distance.");
            NotebookSlowPer = c.Bind("BaldiRampage", "NotebookSlowPer", .08f, "Speed multiplier loss per notebook (prime mode caps this at 0.08). ");
            MinSpeedMult = c.Bind("BaldiRampage", "MinSpeedMult", .35f, "Minimum flee speed multiplier (prime mode keeps at least 0.35). ");
            BaseFleeSpeed = c.Bind("BaldiRampage", "BaseFleeSpeed", 50f, "Flee speed at zero notebooks (prime mode keeps at least 50). ");
            AppleStunMin = c.Bind("BaldiRampage", "AppleStunMin", 3f, "Minimum apple stun.");
            AppleStunMax = c.Bind("BaldiRampage", "AppleStunMax", 5f, "Maximum apple stun.");
            GrappleRange = c.Bind("BaldiRampage", "GrappleRange", 60f, "Maximum grapple flight range.");
            AttackCooldown = c.Bind("BaldiRampage", "AttackCooldown", 2f, "Attack cooldown.");
            MaxAnger = c.Bind("BaldiRampage", "MaxAnger", 100f, "Anger used at full flee speed.");
        }

        public static float EffectiveBaseFleeSpeed =>
            Mathf.Max(50f, BaseFleeSpeed?.Value ?? 50f);

        public static float SpeedMultiplier(int count)
        {
            float slowPerNotebook = Mathf.Clamp(NotebookSlowPer?.Value ?? .08f, 0f, .08f);
            float minimum = Mathf.Clamp(Mathf.Max(.35f,
                MinSpeedMult?.Value ?? .35f), .35f, 1f);
            float loss = slowPerNotebook * Mathf.Max(0, count);
            return Mathf.Clamp(1f - loss, minimum, 1f);
        }
        public static float NotebookSpeed(int count)
        { return EffectiveBaseFleeSpeed * SpeedMultiplier(count); }
    }

    public class BaldiRampageController : MonoBehaviour
    {
        private Baldi baldi;
        private NPC npc;
        private Navigator nav;
        private NavMeshAgent agent;
        private float origBaseSpeed, origAnger, origExtraAnger, origNavSpeed, origNavMax, origAgentSpeed;
        private bool captured, initialized, restored, spawned, hookPullActive, tapePlaying;
        private int notebooks;
        private float nextAttackTime, stunnedTimer, repathTimer, fleeOverrideTimer;
        private float lastAppliedSpeed = float.NaN, lastAppliedAnger = float.NaN;
        private Vector3? fleeOverride;
        private readonly Dictionary<Renderer, bool> rendererStates = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Collider, bool> colliderStates = new Dictionary<Collider, bool>();

        public void SetTapePlaying(bool playing)
        {
            if (tapePlaying == playing) return;
            tapePlaying = playing;
            if (BaldiRampageConfig.IsActive && captured)
                EnforceSpeedAfterSlap();
        }
        private void Awake()
        {
            CacheRefs(); CaptureOriginal(); repathTimer = Random.Range(0f, .35f);
            if (BaldiRampageConfig.IsActive) HideBaldi(true);
        }
        private void Start()
        {
            CacheRefs(); CaptureOriginal();
            if (BaldiRampageConfig.IsActive) StartCoroutine(SpawnFarCoroutine());
        }
        private void CacheRefs()
        {
            if (baldi == null) baldi = GetComponent<Baldi>();
            if (npc == null) npc = GetComponent<NPC>();
            if (nav == null) nav = GetComponentInChildren<Navigator>();
            if (agent == null) agent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>();
        }
        private void CaptureOriginal()
        {
            if (captured || baldi == null) return;
            CacheRefs(); origBaseSpeed = baldi.baseSpeed; origAnger = R.Get<float>(baldi, "anger", .1f);
            origExtraAnger = R.Get<float>(baldi, "extraAnger", 0f);
            try { origNavSpeed = R.Get<float>(nav, "speed", origBaseSpeed); origNavMax = R.Get<float>(nav, "maxSpeed", origNavSpeed); } catch { }
            try { origAgentSpeed = agent != null ? agent.speed : origNavSpeed; } catch { origAgentSpeed = origNavSpeed; }
            captured = true; restored = false;
        }
        private void HideBaldi(bool hide)
        {
            try
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue; if (!rendererStates.ContainsKey(r)) rendererStates[r] = r.enabled;
                    r.enabled = hide ? false : rendererStates[r];
                }
                foreach (Collider c in GetComponentsInChildren<Collider>(true))
                {
                    if (c == null) continue; if (!colliderStates.ContainsKey(c)) colliderStates[c] = c.enabled;
                    c.enabled = hide ? false : colliderStates[c];
                }
            }
            catch { }
        }
        private IEnumerator SpawnFarCoroutine() { yield return null; yield return null; TeleportFarOnce(); }
        public void TeleportFarOnce()
        {
            if (spawned || baldi == null || !BaldiRampageConfig.IsActive) return;
            try
            {
                EnvironmentController ec = baldi.ec; PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0);
                if (ec == null || pm == null) { HideBaldi(false); return; }
                Cell best = null; float score = -1f;
                for (int i = 0; i < 24; i++)
                {
                    Cell c = ec.RandomCell(true, false, true); if (c?.room == null || c.room.type != RoomType.Hall) continue;
                    float d = Vector3.Distance(c.FloorWorldPosition, pm.transform.position); if (d > score) { score = d; best = c; }
                }
                Vector3 p = best != null ? best.FloorWorldPosition : baldi.transform.position;
                if (NavMesh.SamplePosition(p, out NavMeshHit h, 6f, NavMesh.AllAreas)) p = h.position;
                if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.Warp(p); agent.ResetPath(); } else baldi.transform.position = p;
                nav?.ClearDestination();
            }
            catch { }
            HideBaldi(false); spawned = true;
        }
        private void EnsureInitialized()
        {
            if (initialized || baldi == null) return; CaptureOriginal(); initialized = true; restored = false; EnforceSpeedAfterSlap();
        }
        private float CurrentSpeed()
        {


            float slow = BaldiRampageConfig.SpeedMultiplier(notebooks)
                * (tapePlaying ? .60f : 1f);
            return Mathf.Max(8f, BaldiRampageConfig.EffectiveBaseFleeSpeed * slow);
        }
        public void EnforceSpeedAfterSlap()
        {
            if (!BaldiRampageConfig.IsActive || baldi == null) return;
            CaptureOriginal(); float speed = CurrentSpeed(); float slow = speed /
                Mathf.Max(.1f, BaldiRampageConfig.EffectiveBaseFleeSpeed);
            float anger = Mathf.Max(origAnger, BaldiRampageConfig.MaxAnger.Value * slow);
            baldi.baseSpeed = speed; ApplySpeed(speed);
            if (float.IsNaN(lastAppliedAnger) || Mathf.Abs(lastAppliedAnger - anger) > .01f)
            { baldi.SetAnger(anger); lastAppliedAnger = anger; }
            R.Set(baldi, "extraAnger", 0f); lastAppliedSpeed = speed; restored = false;
        }
        private void RestoreOriginal()
        {
            if (!captured || restored || baldi == null) return;
            restored = true; initialized = false; baldi.baseSpeed = origBaseSpeed; baldi.SetAnger(origAnger); R.Set(baldi, "extraAnger", origExtraAnger);
            try { if (nav != null) { nav.SetSpeed(origNavSpeed); R.Set(nav, "speed", origNavSpeed); R.Set(nav, "maxSpeed", origNavMax); } } catch { }
            try { if (agent != null) agent.speed = origAgentSpeed; } catch { }
            lastAppliedSpeed = lastAppliedAnger = float.NaN; HideBaldi(false);
        }
        private void OnDisable() { if (!BaldiRampageConfig.IsActive) RestoreOriginal(); }
        private void OnDestroy() { RestoreOriginal(); }
        public void SetNotebooks(int count)
        { notebooks = Mathf.Max(0, count); if (BaldiRampageConfig.IsActive) EnforceSpeedAfterSlap(); }
        public void SetHookPullActive(bool active) { hookPullActive = active; if (active) StopMovement(); else if (BaldiRampageConfig.IsActive) EnforceSpeedAfterSlap(); }
        public void Stun(float seconds) { stunnedTimer = Mathf.Max(stunnedTimer, seconds); StopMovement(); }
        public void OnNoise(Vector3 source)
        {
            if (!BaldiRampageConfig.IsActive || baldi == null) return;
            Vector3 away = baldi.transform.position - source; away.y = 0; if (away.sqrMagnitude < .1f) return;
            fleeOverride = away.normalized; fleeOverrideTimer = 3f; repathTimer = 0f;
        }
        private void Update()
        {
            CacheRefs(); if (baldi == null || npc == null || !npc.gameObject.activeInHierarchy) return;
            if (!BaldiRampageConfig.IsActive) { RestoreOriginal(); return; }
            EnsureInitialized(); HandleAttack();
        }
        private void LateUpdate()
        {
            CacheRefs(); if (baldi == null || npc == null || !npc.gameObject.activeInHierarchy || !BaldiRampageConfig.IsActive) return;
            ForceFlee();
        }
        private void StopMovement()
        {
            try { if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } } catch { }
            try { nav?.ClearDestination(); } catch { }
        }
        private void ApplySpeed(float speed)
        {
            try { if (nav != null) { nav.SetSpeed(speed); R.Set(nav, "speed", speed); R.Set(nav, "maxSpeed", speed); } } catch { }
            try { if (agent != null && agent.enabled) { agent.speed = speed; agent.acceleration = Mathf.Max(agent.acceleration, 80f); agent.angularSpeed = 720f; agent.autoBraking = false; } } catch { }
        }
        private void ForceFlee()
        {
            if (BaldiRampageConfig.CatchingPlayer || hookPullActive) { StopMovement(); return; }
            if (stunnedTimer > 0f) { stunnedTimer -= Time.deltaTime; StopMovement(); return; }
            if (fleeOverrideTimer > 0f) fleeOverrideTimer -= Time.deltaTime; else fleeOverride = null;
            PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0); if (pm == null) return;
            float targetSpeed = CurrentSpeed();
            if (float.IsNaN(lastAppliedSpeed) || Mathf.Abs(lastAppliedSpeed - targetSpeed) > .01f) EnforceSpeedAfterSlap();
            else ApplySpeed(targetSpeed);
            repathTimer -= Time.deltaTime; if (repathTimer > 0f) return; repathTimer = Random.Range(.30f, .48f);
            if (!TryGetFleeTarget(pm, out Vector3 target)) return;
            try
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = false; agent.SetDestination(target); }
                else nav?.FindPath(target);
            }
            catch { }
        }
        private bool TryGetFleeTarget(PlayerManager pm, out Vector3 target)
        {
            target = baldi.transform.position; EnvironmentController ec = baldi.ec ?? pm.ec; if (ec == null) return false;
            Vector3 away = fleeOverride ?? (baldi.transform.position - pm.transform.position); away.y = 0;
            if (away.sqrMagnitude < .1f) away = -baldi.transform.forward; away.Normalize();
            Cell best = null; float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Cell c = ec.RandomCell(true, false, true); if (c?.room == null || c.room.type != RoomType.Hall) continue;
                Vector3 d = c.FloorWorldPosition - baldi.transform.position; d.y = 0; if (d.magnitude < 10f) continue;
                float score = Vector3.Dot(d.normalized, away) * 1000f + d.magnitude; if (score > bestScore) { bestScore = score; best = c; }
            }
            Vector3 rough = best != null ? best.FloorWorldPosition : baldi.transform.position + away * 20f;
            if (NavMesh.SamplePosition(rough, out NavMeshHit h, 10f, NavMesh.AllAreas)) { target = h.position; return true; }
            return false;
        }
        private void HandleAttack()
        {
            if (hookPullActive || Time.time < nextAttackTime) return;
            PlayerManager pm = Singleton<CoreGameManager>.Instance?.GetPlayer(0); if (pm?.plm == null) return;
            float dist = Vector3.Distance(baldi.transform.position, pm.transform.position); if (!CanSeePlayer(pm, dist)) return;
            float use = dist <= BaldiRampageConfig.CloseDistance.Value ? BaldiRampageConfig.ItemCloseChance.Value
                : dist <= BaldiRampageConfig.MidDistance.Value ? BaldiRampageConfig.ItemMidChance.Value : BaldiRampageConfig.ItemFarChance.Value;
            if (Random.value > Mathf.Clamp01(use)) { nextAttackTime = Time.time + 1.2f; return; }
            float a = Mathf.Max(0, BaldiRampageConfig.DietBsodaChance.Value), b = Mathf.Max(0, BaldiRampageConfig.BsodaChance.Value),
                g = Mathf.Max(0, BaldiRampageConfig.GrappleChance.Value), total = a + b + g;
            float roll = Random.value * Mathf.Max(.001f, total);
            if (roll < a) BaldiRampage.DoBsoda(baldi, pm, true); else if (roll < a + b) BaldiRampage.DoBsoda(baldi, pm, false); else BaldiRampage.DoGrapple(baldi, pm);
            nextAttackTime = Time.time + Mathf.Max(.1f, BaldiRampageConfig.AttackCooldown.Value);
        }
        private bool CanSeePlayer(PlayerManager pm, float dist)
        {
            if (dist > BaldiRampageConfig.MaxViewDistance.Value || BaldiRampage.IsPlayerInvisible(pm)) return false;
            Vector3 origin = baldi.transform.position + Vector3.up * 1.2f, dir = pm.transform.position - origin;
            if (dir.sqrMagnitude < .001f) return true;
            return !Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider == null || hit.collider.GetComponentInParent<PlayerManager>() != null;
        }
    }

    public static class BaldiRampage
    {
        public static bool IsHitByThrownApple;
        public static void DoBsoda(Baldi baldi, PlayerManager pm, bool diet)
        {
            try
            {
                ItemObject io = FindBsodaItem(diet) ?? FindBsodaItem(false); Item item = GetItem(io); if (item == null) return;
                ITM_BSODA bs = UnityEngine.Object.Instantiate(item, baldi.transform.position, Quaternion.identity) as ITM_BSODA; if (bs == null) return;
                Vector3 dir = pm.transform.position - baldi.transform.position; dir.y = 0; if (dir.sqrMagnitude < .01f) dir = -baldi.transform.forward; dir.Normalize();
                bs.transform.position = baldi.transform.position + dir * 2f; bs.transform.forward = dir; R.Set(bs, "ec", pm.ec); R.Set(bs, "launching", false);
                Entity ent = R.Get<Entity>(bs, "entity", null); ent?.Initialize(pm.ec, bs.transform.position);
                if (diet) R.Set(bs, "time", Mathf.Max(.3f, R.Get<float>(bs, "time", 1f) * .55f));
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("BaldiRampage DoBsoda: " + ex); }
        }
        public static void DoGrapple(Baldi baldi, PlayerManager pm)
        {
            try
            {
                ITM_GrapplingHook prefab = FindGrapplePrefab(); if (baldi == null || pm == null || prefab == null) return;
                Vector3 away = baldi.transform.position - pm.transform.position; away.y = 0; if (away.sqrMagnitude < .001f) away = -baldi.transform.forward; away.Normalize();
                ITM_GrapplingHook hook = UnityEngine.Object.Instantiate(prefab, baldi.transform.position + Vector3.up * 1.35f + away * 1.1f, Quaternion.LookRotation(away));
                hook.enabled = false; hook.gameObject.AddComponent<BaldiGrappleRuntime>().Initialize(baldi, pm, hook, away);
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("BaldiRampage DoGrapple: " + ex); }
        }
        private static ITM_GrapplingHook FindGrapplePrefab()
        {
            foreach (ItemObject io in Resources.FindObjectsOfTypeAll<ItemObject>())
            {
                if (io == null) continue; string n = (io.name + io.itemType).ToLowerInvariant();
                if (n.Contains("grap") || n.Contains("hook")) { Item item = GetItem(io); if (item is ITM_GrapplingHook h) return h; }
            }
            return Resources.FindObjectsOfTypeAll<ITM_GrapplingHook>().FirstOrDefault(h => h != null && !h.gameObject.scene.IsValid());
        }
        public static bool IsPlayerInvisible(PlayerManager pm)
        {
            try
            {
                foreach (Component c in new Component[] { pm, pm?.plm })
                    if (c != null) foreach (string n in new[] { "invisible", "invisibility", "invisTime", "invis", "hidden" })
                    { object v = R.Field(c, n)?.GetValue(c); if ((v is bool b && b) || (v is float f && f > 0) || (v is int i && i > 0)) return true; }
            }
            catch { }
            return false;
        }
        private static ItemObject FindBsodaItem(bool diet)
        { return Resources.FindObjectsOfTypeAll<ItemObject>().FirstOrDefault(i => i != null && (i.name + i.itemType).ToLowerInvariant().Contains("bsoda") && (i.name.ToLowerInvariant().Contains("diet") == diet)); }
        private static Item GetItem(ItemObject io)
        { if (io == null) return null; Item item = R.Get<Item>(io, "item", null); return item ?? Resources.FindObjectsOfTypeAll<Item>().FirstOrDefault(x => x != null && x.name.IndexOf(io.name, StringComparison.OrdinalIgnoreCase) >= 0); }
    }

    public class BaldiGrappleRuntime : MonoBehaviour
    {
        private Baldi baldi; private BaldiRampageController controller; private EnvironmentController ec;
        private Navigator baldiNav; private NavMeshAgent baldiAgent; private LineRenderer lineRenderer; private Entity entity;
        private AudioManager audMan; private AudioSource motorAudio; private SoundObject audLaunch, audClang, audSnap;
        private Transform cracks; private LayerMaskObject layerMask; private readonly Vector3[] positions = new Vector3[2];
        private Vector3 navAnchor, launchPosition; private float speed = 100f, maxPressure = 100f, initialForce = 20f,
            forceIncrease = 5f, stopDistance = 5f, force, initialDistance, hookTime; private bool locked, snapped, ending, ended;
        public void Initialize(Baldi owner, PlayerManager target, ITM_GrapplingHook hook, Vector3 dir)
        {
            baldi = owner; controller = baldi?.GetComponent<BaldiRampageController>(); ec = baldi?.ec;
            baldiNav = baldi?.GetComponentInChildren<Navigator>(); baldiAgent = baldi?.GetComponent<NavMeshAgent>() ?? baldi?.GetComponentInChildren<NavMeshAgent>();
            transform.forward = dir; launchPosition = transform.position;
            lineRenderer = R.Get<LineRenderer>(hook, "lineRenderer", null); entity = R.Get<Entity>(hook, "entity", null);
            audMan = R.Get<AudioManager>(hook, "audMan", null); motorAudio = R.Get<AudioSource>(hook, "motorAudio", null);
            audLaunch = R.Get<SoundObject>(hook, "audLaunch", null); audClang = R.Get<SoundObject>(hook, "audClang", null); audSnap = R.Get<SoundObject>(hook, "audSnap", null);
            cracks = R.Get<Transform>(hook, "cracks", null); layerMask = R.Get<LayerMaskObject>(hook, "layerMask", null);
            speed = R.Get<float>(hook, "speed", 100f); maxPressure = R.Get<float>(hook, "maxPressure", 100f);
            initialForce = R.Get<float>(hook, "initialForce", 20f); forceIncrease = R.Get<float>(hook, "forceIncrease", 5f); stopDistance = R.Get<float>(hook, "stopDistance", 5f);
            try
            {
                if (lineRenderer != null) lineRenderer.enabled = true; if (cracks != null) cracks.gameObject.SetActive(false); motorAudio?.Stop();
                if (entity != null) { entity.Initialize(ec, transform.position); entity.OnEntityMoveInitialCollision += OnCollision; }
                if (audLaunch != null) audMan?.PlaySingle(audLaunch);
            }
            catch { }
            controller?.SetHookPullActive(true);
        }
        private void Update()
        {
            if (ended || baldi == null || ec == null) { EndNow(); return; }
            if (!locked)
            {
                float ts = ec.EnvironmentTimeScale; if (entity != null) entity.UpdateInternalMovement(transform.forward * speed * ts); else transform.position += transform.forward * speed * ts * Time.deltaTime;
                hookTime += Time.deltaTime * ts; if (hookTime > 6f || Vector3.Distance(launchPosition, transform.position) > BaldiRampageConfig.GrappleRange.Value) EndNow();
            }
            else UpdatePulling();
        }
        private void LateUpdate() { if (ended || lineRenderer == null || baldi == null) return; positions[0] = transform.position; positions[1] = baldi.transform.position - Vector3.up; lineRenderer.SetPositions(positions); }
        private void UpdatePulling()
        {
            float dist = Vector3.Distance(transform.position, baldi.transform.position); if (!snapped) PullBaldi();
            if (dist <= stopDistance && !ending) { ending = true; StartCoroutine(EndDelay()); }
            force += forceIncrease * Time.deltaTime;
            if (dist - (initialDistance - force) > maxPressure && !snapped) { snapped = true; motorAudio?.Stop(); if (lineRenderer != null) lineRenderer.enabled = false; if (audSnap != null) audMan?.PlaySingle(audSnap); StartCoroutine(WaitAudio()); }
        }
        private void PullBaldi() { float s = Mathf.Max(BaldiRampageConfig.EffectiveBaseFleeSpeed, force * 1.5f); try { baldiNav?.SetSpeed(s); R.Set(baldiNav, "speed", s); R.Set(baldiNav, "maxSpeed", s); if (baldiAgent != null && baldiAgent.isOnNavMesh) { baldiAgent.isStopped = false; baldiAgent.speed = s; baldiAgent.SetDestination(navAnchor); } } catch { } }
        private void OnCollision(RaycastHit hit)
        { if (locked || hit.collider == null || (layerMask != null && !layerMask.Contains(hit.collider.gameObject.layer))) return; locked = true; force = initialForce; initialDistance = Vector3.Distance(transform.position, baldi.transform.position); navAnchor = NavMesh.SamplePosition(hit.point, out NavMeshHit h, 3f, NavMesh.AllAreas) ? h.position : hit.point; entity?.SetFrozen(true); if (audClang != null) audMan?.PlaySingle(audClang); motorAudio?.Play(); if (cracks != null) cracks.gameObject.SetActive(true); }
        private IEnumerator EndDelay() { yield return new WaitForSeconds(.25f); EndNow(); }
        private IEnumerator WaitAudio()
        {
            float timeout = 4f;
            while (timeout > 0f)
            {
                bool playing = audMan?.audioDevice != null && audMan.audioDevice.isPlaying;
                if (SoundShuffleNoAudioWaitPatch.Active
                    && SoundShuffleDetachedPlaybackPatch.TryGetVirtualPlaying(
                        audMan, out bool virtualPlaying))
                    playing = virtualPlaying;
                if (!playing) break;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            EndNow();
        }
        private void EndNow() { if (ended) return; ended = true; try { if (entity != null) entity.OnEntityMoveInitialCollision -= OnCollision; } catch { } controller?.SetHookPullActive(false); Destroy(gameObject); }
        private void OnDestroy() { try { if (entity != null) entity.OnEntityMoveInitialCollision -= OnCollision; } catch { } controller?.SetHookPullActive(false); }
    }


    public class AppleChargeHandler : MonoBehaviour
    {
        private PlayerManager pm; private InputManager input; private ItemManager itm; private int slot;
        private float charge; private const float MaxCharge = 5f; private bool done;
        private AudioSource audioSource; private static AudioClip cachedChargeClip;
        private float walk, run; private bool captured;
        public static AppleChargeHandler ActiveCharge { get; private set; }
        public void Initialize(PlayerManager player, ItemManager manager, int itemSlot)
        {
            pm = player; itm = manager; slot = itemSlot; input = FindObjectOfType<InputManager>();
            if (pm?.plm != null) { walk = pm.plm.walkSpeed; run = pm.plm.runSpeed; captured = true; }
            if (cachedChargeClip == null) cachedChargeClip = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c != null && c.name.ToLowerInvariant().Contains("grapple") && c.name.ToLowerInvariant().Contains("loop"));
            if (cachedChargeClip != null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.clip = cachedChargeClip; audioSource.loop = true; audioSource.pitch = .5f; audioSource.volume = .7f; audioSource.Play(); }
            ActiveCharge = this;
        }
        private void Update()
        {
            if (done) return;
            if (itm == null || itm.items == null || slot < 0 || slot >= itm.items.Length || itm.items[slot] == null || itm.items[slot].itemType != Items.Apple || itm.selectedItem != slot) { Finish(false); return; }
            charge = Mathf.Min(MaxCharge, charge + Time.deltaTime); float t = charge / MaxCharge;
            if (audioSource != null) audioSource.pitch = Mathf.Lerp(.5f, 2f, t);
            if (captured && pm?.plm != null) { float m = Mathf.Lerp(1f, .15f, t); pm.plm.walkSpeed = walk * m; pm.plm.runSpeed = run * m; }
            try { if (input == null || !input.GetDigitalInput("UseItem", false)) Fire(); } catch { if (charge >= .3f) Fire(); }
        }
        private void Fire()
        {
            if (done) return;
            try
            {
                float speed = Mathf.Lerp(15f, 55f, charge / MaxCharge); Transform cam = null; try { cam = Singleton<CoreGameManager>.Instance?.GetCamera(pm.playerNumber)?.transform; } catch { }
                Vector3 dir = cam != null ? cam.forward : pm.transform.forward; dir.y = 0; if (dir.sqrMagnitude < .001f) dir = Vector3.forward; dir.Normalize();
                GameObject go = new GameObject("BaldiRampageApple"); go.transform.position = pm.transform.position + dir * 1.35f + Vector3.up * 1.4f;
                go.AddComponent<BaldiAppleProjectile>().Initialize(pm, dir, speed); itm.RemoveItem(slot); Finish(true);
            }
            catch (Exception ex) { KnoxumsChaosModePlugin.Log.LogError("Apple fire: " + ex); Finish(false); }
        }
        private void Finish(bool fired) { if (done) return; done = true; Restore(); if (audioSource != null) audioSource.Stop(); if (ActiveCharge == this) ActiveCharge = null; Destroy(gameObject); }
        private void Restore() { if (captured && pm?.plm != null) { pm.plm.walkSpeed = walk; pm.plm.runSpeed = run; } }
        private void OnDestroy() { Restore(); if (ActiveCharge == this) ActiveCharge = null; }
    }

    public class BaldiAppleProjectile : MonoBehaviour
    {
        private PlayerManager owner; private Vector3 dir; private float speed = 28f, life = 10f; private bool destroyed;
        private SphereCollider sphere; private Transform visual, cam; private AudioSource fly;
        private static Sprite cachedSprite; private static AudioClip cachedFlyClip;
        public void Initialize(PlayerManager p, Vector3 d, float customSpeed) { speed = customSpeed; Initialize(p, d); }
        public void Initialize(PlayerManager p, Vector3 d)
        {
            owner = p; dir = d; dir.y = 0; if (dir.sqrMagnitude < .001f) dir = Vector3.forward; dir.Normalize(); transform.forward = dir;
            try { cam = Singleton<CoreGameManager>.Instance?.GetCamera(p != null ? p.playerNumber : 0)?.transform; } catch { }
            sphere = gameObject.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.radius = .35f; Rigidbody rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            if (owner != null) foreach (Collider c in owner.GetComponentsInChildren<Collider>(true)) if (c != null) Physics.IgnoreCollision(sphere, c, true);
            AttachVisual(); PlaySound();
        }
        private void AttachVisual()
        {
            if (cachedSprite == null) { Sprite s = Resources.FindObjectsOfTypeAll<ItemObject>().Where(i => i != null && i.itemType == Items.Apple).Select(i => i.itemSpriteLarge ?? i.itemSpriteSmall).FirstOrDefault(x => x != null); if (s != null) cachedSprite = Sprite.Create(s.texture, s.rect, new Vector2(.5f, .5f), s.pixelsPerUnit); }
            if (cachedSprite == null) return; GameObject go = new GameObject("AppleVisual"); go.transform.SetParent(transform, false); visual = go.transform; SpriteRenderer r = go.AddComponent<SpriteRenderer>(); r.sprite = cachedSprite; r.sortingOrder = 80;
        }
        private void PlaySound()
        {
            if (cachedFlyClip == null) cachedFlyClip = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c != null && (c.name.ToLowerInvariant().Contains("gum") || c.name.ToLowerInvariant().Contains("bubble")));
            if (cachedFlyClip == null) return; fly = gameObject.AddComponent<AudioSource>(); fly.clip = cachedFlyClip; fly.loop = cachedFlyClip.length > .45f; fly.spatialBlend = .85f; fly.maxDistance = 40f; fly.Play();
        }
        private void Update()
        {
            if (destroyed) return; if ((life -= Time.deltaTime) <= 0f) { DestroyApple(); return; }
            float step = speed * Time.deltaTime;
            if (Physics.SphereCast(transform.position, sphere.radius * .9f, dir, out RaycastHit hit, step + .15f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != null)
            {
                if (hit.collider.GetComponentInParent<PlayerManager>() == owner) { }
                else if (hit.collider.GetComponentInParent<Baldi>() is Baldi b) { transform.position = hit.point; HitBaldi(b); return; }
                else if (hit.collider.GetComponentInParent<NPC>() == null) { transform.position = hit.point; DestroyApple(); return; }
            }
            transform.position += dir * step;
        }
        private void LateUpdate() { if (visual == null || destroyed) return; if (cam == null) try { cam = Singleton<CoreGameManager>.Instance?.GetCamera(0)?.transform; } catch { } if (cam != null) { visual.rotation = cam.rotation; visual.position = transform.position - cam.forward * .12f; } }
        private void OnTriggerEnter(Collider other) { if (destroyed || other == null || other.GetComponentInParent<PlayerManager>() == owner) return; Baldi b = other.GetComponentInParent<Baldi>(); if (b != null) HitBaldi(b); else if (other.GetComponentInParent<NPC>() == null) DestroyApple(); }
        private void HitBaldi(Baldi b)
        {
            if (destroyed || b == null) return; try
            {
                BaldiRampage.IsHitByThrownApple = true; try { R.Set(b, "appleTime", 3f); b.TakeApple(); } finally { BaldiRampage.IsHitByThrownApple = false; }
                float min = BaldiRampageConfig.AppleStunMin.Value, max = BaldiRampageConfig.AppleStunMax.Value; if (max < min) { float t = min; min = max; max = t; }
                b.GetComponent<BaldiRampageController>()?.Stun(Random.Range(min, max));
            }
            catch { }
            DestroyApple();
        }
        private void DestroyApple() { if (destroyed) return; destroyed = true; fly?.Stop(); Destroy(gameObject); }
    }

    internal static class R
    {
        public static FieldInfo Field(object o, string name)
        { if (o == null) return null; for (Type t = o.GetType(); t != null && t != typeof(object); t = t.BaseType) { FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) return f; } return null; }
        public static T Get<T>(object o, string name, T def = default)
        { try { object v = Field(o, name)?.GetValue(o); if (v == null) return def; if (v is T x) return x; return (T)Convert.ChangeType(v, typeof(T)); } catch { return def; } }
        public static void Set(object o, string name, object value) { try { Field(o, name)?.SetValue(o, value); } catch { } }
        public static void SetPossibleBoolFields(object o, bool value, params string[] names)
        { if (o == null) return; for (Type t = o.GetType(); t != null && t != typeof(object); t = t.BaseType) foreach (string n in names) try { FieldInfo f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (f != null && f.FieldType == typeof(bool)) f.SetValue(o, value); } catch { } }
    }


    [HarmonyPatch(typeof(Elevator), "ButtonPressed", new Type[] { })]
    public static class ElevatorButtonPressedPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Elevator __instance, out bool __state)
        {
            __state = ElevatorUnlockService.IsPitstopManager(
                Singleton<BaseGameManager>.Instance);
            try { return ElevatorUnlockService.OnElevatorButtonPressed(__instance); }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogError("Elevator.ButtonPressed lap hook: " + ex);
                return true;
            }
        }

        [HarmonyPostfix]
        static void Postfix(Elevator __instance, bool __state)
        {
            if (!__state || __instance == null) return;


            ElevatorUnlockService.CloseElevatorDoors(__instance);
            try { Physics.SyncTransforms(); } catch { }
        }
    }

    [HarmonyPatch]
    public static class PitstopElevatorStayOpenPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        { MethodInfo a = typeof(Elevator).GetMethod("OpenDoor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null); if (a != null) yield return a; MethodInfo b = typeof(Elevator).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null); if (b != null) yield return b; }
        static bool Prefix(MethodBase __originalMethod, object[] __args)
        { if (!ElevatorUnlockService.IsPitstopManager(Singleton<BaseGameManager>.Instance) || ElevatorUnlockService.PitstopExitArmed) return true; if (__originalMethod?.Name == "Close") return false; return !(__args?.Length == 1 && __args[0] is bool x && !x); }
    }

    public static class ElevatorUnlockService
    {
        private static bool applied, loadNextStarted, pitstopExitArmed;
        private static readonly List<Collider> disabled = new List<Collider>();
        public static bool PitstopExitArmed => pitstopExitArmed;
        public static bool ElevatorsUnlockedThisFloor => applied;
        public static void MarkLoadNextStarted() { loadNextStarted = true; }
        public static bool AllNotebooksReallyDone(BaseGameManager b) { if (b == null) return false; return R.Get<bool>(b, "allNotebooksFound", false) || (b.NotebookTotal > 0 && b.FoundNotebooks >= b.NotebookTotal) || applied; }
        public static void ResetForNewFloorOrLap()
        {
            applied = false;
            loadNextStarted = false;
            pitstopExitArmed = false;
            foreach (Collider c in disabled)
                if (c != null) c.enabled = true;
            disabled.Clear();
            BaldiRampagePatches.ResetCowardRoundFlags();
        }
        public static bool IsPitstopManager(BaseGameManager b)
        { if (b == null) return false; try { return b.GetType().Name.ToLowerInvariant().Contains("pitstop") || b.name.ToLowerInvariant().Contains("pitstop") || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLowerInvariant().Contains("pitstop"); } catch { return false; } }
        public static void KeepPitstopElevatorsOpen(BaseGameManager b)
        {
            if (b == null || pitstopExitArmed) return;
            foreach (Elevator e in GetElevators(b.Ec))
            {
                if (e == null) continue;
                bool open = R.Get<bool>(e, "doorIsOpen", false);
                R.Set(e, "open", true);
                R.Set(e, "doorIsOpen", true);
                if (!open) try { e.OpenDoor(true); } catch { }
                DisableGate(e);
                ClearPocketOnly(e);


                try { if (e.ColliderGroup != null) e.ColliderGroup.Enable(true); } catch { }
                try
                {
                    ColliderGroup inside = GetInsideCollider(e);
                    if (inside != null) inside.Enable(true);
                }
                catch { }
                UnlockElevatorButton(e);
            }
            try { Physics.SyncTransforms(); } catch { }
        }
        public static void ClearClosedElevatorFrontBarriers(BaseGameManager b)
        { if (b == null) return; foreach (Elevator e in GetElevators(b.Ec)) if (e != null) ClearPocketOnly(e); }
        private static void ClearPocketOnly(Elevator e)
        {

            string[] names = { "pocketCollider", "coverCollider", "frontCollider", "hallCollider", "blockerCollider" };
            foreach (string n in names) { object v = null; try { v = R.Field(e, n)?.GetValue(e); } catch { } if (v is Collider c) Disable(c, e); }
            foreach (Collider c in e.GetComponentsInChildren<Collider>(true))
            { if (c == null || !c.enabled || c.isTrigger) continue; string n = c.name.ToLowerInvariant(); if (n.Contains("gate") || n.Contains("door") || n.Contains("button") || n.Contains("inside")) continue; if (n.Contains("pocket") || n.Contains("cover") || n.Contains("frontbarrier") || n.Contains("blocker")) Disable(c, e); }
        }
        private static void Disable(Collider c, Elevator e) { if (c == null || !c.enabled || c.isTrigger || c == R.Get<Collider>(e, "gateCollider", null)) return; c.enabled = false; if (!disabled.Contains(c)) disabled.Add(c); }
        private static void DisableGate(Elevator e) { Collider c = R.Get<Collider>(e, "gateCollider", null); if (c != null) c.enabled = false; }
        public static void OnAllNotebooks(BaseGameManager b, string reason) { if (b == null || b is EndlessGameManager || IsPitstopManager(b) || BaldiRampageConfig.IsActive || applied) return; EnsureElevatorsOpen(b); applied = true; }
        public static void EnsureElevatorsOpen(BaseGameManager b)
        {
            if (b == null) return; R.Set(b, "allNotebooksFound", true); try { MethodInfo m = typeof(BaseGameManager).GetMethod("SetElevators", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null); m?.Invoke(b, new object[] { true }); } catch { }
            foreach (Elevator e in GetElevators(b.Ec))
            {
                if (e == null) continue;
                R.Set(e, "open", true);
                R.Set(e, "doorIsOpen", true);
                try { e.OpenDoor(true); } catch { }
                DisableGate(e);
                UnlockElevatorButton(e);
                try { e.SetState(ElevatorState.OpenForExit); } catch { }


                ElevatorExitHelper fallback = e.GetComponent<ElevatorExitHelper>();
                if (fallback != null)
                    UnityEngine.Object.Destroy(fallback);
            }
        }
        private static void UnlockElevatorButton(Elevator e)
        { foreach (MonoBehaviour m in e.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null && (m.GetType().Name.Contains("Button") || m.name.ToLowerInvariant().Contains("button"))) { m.enabled = true; m.gameObject.SetActive(true); R.SetPossibleBoolFields(m, false, "locked", "disabled", "inactive"); R.SetPossibleBoolFields(m, true, "unlocked", "interactable", "clickable"); } }
        public static void UnlockSpawnElevatorButton(Elevator e) { if (e != null) UnlockElevatorButton(e); }


        public static bool OnElevatorButtonPressed(Elevator e)
        {
            BaseGameManager b = Singleton<BaseGameManager>.Instance;
            if (e == null || b == null) return true;


            if (IsPitstopManager(b))
            {
                BeginPitstopDeparture(b, e);
                return false;
            }

            ChaosManager cm = ChaosManager.Instance;
            if (cm == null || !cm.IsLapsActive) return true;
            if (cm.IsLapTransitionInProgress) return false;


            RepairManagerElevatorList(e, b.Ec);

            bool notebooksDone = AllNotebooksReallyDone(b);
            if (cm.CurrentLap > 1 && !cm.FloorExitToPitstopCommitted && !notebooksDone)
            {
                KnoxumsChaosModePlugin.Log.LogInfo(
                    "Laps: voluntary elevator exit from lap " + cm.CurrentLap + ".");
                cm.LeaveToPitstopNow("Elevator.ButtonPressed");
                return false;
            }

            if (!notebooksDone) return true;

            if (cm.ShouldStartNewLap())
            {


                KnoxumsChaosModePlugin.Log.LogInfo(
                    "Laps: elevator button -> current floor lap "
                    + (cm.CurrentLap + 1) + ".");


                CloseElevatorDoors(e);
                cm.StartInstantNewLap(b);
                return false;
            }


            if (cm.IsLastLap())
            {
                cm.CommitFloorExitToPitstop();
                cm.StartCoroutine(ConfirmExit(b, 1.5f));


            }
            return true;
        }

        private static void RepairManagerElevatorList(Elevator source, EnvironmentController ec)
        {
            if (source == null) return;
            try
            {


                List<Elevator> snapshot = GetElevators(ec);
                object manager = R.Get<object>(source, "manager", null);
                FieldInfo field = R.Field(manager, "elevators");
                System.Collections.IList managerList =
                    field != null ? field.GetValue(manager) as System.Collections.IList : null;
                if (managerList == null) return;
                managerList.Clear();
                for (int i = 0; i < snapshot.Count; i++)
                    if (snapshot[i] != null) managerList.Add(snapshot[i]);
            }
            catch (Exception ex)
            {
                KnoxumsChaosModePlugin.Log.LogWarning(
                    "Elevator.ButtonPressed manager repair: " + ex.Message);
            }
        }

        public static void BeginPitstopDeparture(BaseGameManager b, Elevator e)
        {
            if (b == null || pitstopExitArmed || loadNextStarted) return;
            pitstopExitArmed = true;
            CloseElevatorDoors(e);


            if (ChaosManager.Instance != null)
                ChaosManager.Instance.StartCoroutine(ConfirmExit(b, .6f));
            else
                b.StartCoroutine(ConfirmExit(b, .6f));
        }
        public static void HandleElevatorExitButton(BaseGameManager b, Elevator e) { if (b == null) return; CloseElevatorDoors(e); if (ChaosManager.Instance?.IsLapsActive == true && ChaosManager.Instance.IsLastLap()) ChaosManager.Instance.CommitFloorExitToPitstop(); ChaosManager.Instance?.StartCoroutine(ConfirmExit(b, .6f)); }
        private static IEnumerator ConfirmExit(BaseGameManager b, float wait) { while (wait > 0 && !loadNextStarted) { wait -= Time.unscaledDeltaTime; yield return null; } if (!loadNextStarted) ForceLoadNext(b); }
        public static void CloseElevatorDoors(Elevator e) { if (e == null) return; try { e.OpenDoor(false); } catch { } R.Set(e, "open", false); R.Set(e, "doorIsOpen", false); Collider c = R.Get<Collider>(e, "gateCollider", null); if (c != null) c.enabled = true; }
        public static bool IsLastOpenElevator(BaseGameManager b, Elevator candidate) { List<Elevator> open = GetElevators(b?.Ec).Where(e => e != null && R.Get<bool>(e, "open", false)).ToList(); return open.Count <= 1 || open[0] == candidate; }
        public static bool PlayerInside(Elevator e, PlayerManager p) { if (e == null || p == null) return false; Vector3 a = e.transform.position, b = p.transform.position; a.y = b.y = 0; return Vector3.Distance(a, b) <= 14f; }
        public static bool IsAlreadyLeaving(BaseGameManager b) { return b == null || loadNextStarted || (ChaosManager.Instance?.IsLapTransitionInProgress ?? false) || R.Get<bool>(b, "ending", false) || R.Get<bool>(b, "exiting", false); }
        public static void ForceLoadNext(BaseGameManager b) { if (b == null || loadNextStarted) return; loadNextStarted = true; try { b.LoadNextLevel(); } catch (Exception ex) { loadNextStarted = false; KnoxumsChaosModePlugin.Log.LogError("ForceLoadNext: " + ex); } }
        public static ColliderGroup GetInsideCollider(Elevator e) { if (e == null) return null; try { PropertyInfo p = e.GetType().GetProperty("InsideCollider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); return p?.GetValue(e, null) as ColliderGroup ?? R.Get<ColliderGroup>(e, "insideCollider", null); } catch { return null; } }
        public static List<Elevator> GetElevators(EnvironmentController ec)
        { List<Elevator> r = new List<Elevator>(); HashSet<int> s = new HashSet<int>(); try { if (ec?.Elevators != null) foreach (Elevator e in ec.Elevators) if (e != null && s.Add(e.GetInstanceID())) r.Add(e); } catch { } foreach (Elevator e in UnityEngine.Object.FindObjectsOfType<Elevator>(true)) if (e != null && s.Add(e.GetInstanceID())) r.Add(e); return r; }
    }


    public class ElevatorExitHelper : MonoBehaviour
    {
        private void Awake() { enabled = false; }
        public void Bind(BaseGameManager manager, Elevator owner) { enabled = false; }
    }
}
