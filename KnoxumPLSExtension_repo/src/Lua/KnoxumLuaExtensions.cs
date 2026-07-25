// ============================================================================
// KnoxumPLSExtension — Lua Extensions for Custom Challenges
//
// Extends the PlusLevelStudio.Lua system with:
//   - TalkProxy: NPC dialogue (Say, queue, subtitles)
//   - DoorProxy: door control (open/close/lock)
//   - CellProxy extensions: neighbors, doors, occupancy
//   - RoomProxy extensions: doors list
//   - NPCProxy extensions: Say, behavior state
//   - EditorLuaGameProxy extensions: CreateTalk, timer, HUD, map, audio, FX
//   - New Lua callbacks: OnNPCTalk, OnDoorOpened, OnPlayerEnterRoom, etc.
//   - wait() coroutine helper
//
// Namespace: KnoxumPLSExtension.Features
// Requires: HarmonyLib, MoonSharp.Interpreter, PlusLevelStudio.Lua
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MTM101BaldAPI;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Lua;
using PlusStudioLevelLoader;
using UnityEngine;
using UnityEngine.UI;
using MoonSharpType = MoonSharp.Interpreter.DataType;
using MoonSharpUserDataUtil = MoonSharp.Interpreter.UserData;

namespace KnoxumPLSExtension.Features
{
    // ========================================================================
    // 1. TALK PROXY — NPC dialogue queue
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public class TalkLine
    {
        public string soundId;
        public string subtitleText;  // null = use default from Subtitles_En.json
        public float pauseAfter;     // seconds of silence after this line
    }

    [MoonSharp.Interpreter.MoonSharpUserData]
    public class TalkProxy
    {
        [MoonSharp.Interpreter.MoonSharpHidden] public NPC npc;
        [MoonSharp.Interpreter.MoonSharpHidden] public MonoBehaviour host; // coroutine host

        private readonly Queue<TalkLine> queue = new Queue<TalkLine>();
        private UnityEngine.Coroutine activeCoroutine;
        private bool playing;

        /// <summary>Add a dialogue line (plays via NPC's AudioManager).</summary>
        public TalkProxy AddLine(string soundId)
        {
            queue.Enqueue(new TalkLine { soundId = soundId });
            return this;
        }

        /// <summary>Add a dialogue line with custom subtitle override.</summary>
        public TalkProxy AddLine(string soundId, string subtitleText)
        {
            queue.Enqueue(new TalkLine { soundId = soundId, subtitleText = subtitleText });
            return this;
        }

        /// <summary>Add a silent pause between lines.</summary>
        public TalkProxy AddPause(float seconds)
        {
            queue.Enqueue(new TalkLine { pauseAfter = seconds });
            return this;
        }

        /// <summary>Start playing the queued sequence.</summary>
        public void Play()
        {
            if (host == null || npc == null) return;
            if (playing) Stop();
            activeCoroutine = host.StartCoroutine(PlaySequence());
        }

        /// <summary>Stop playback and clear queue.</summary>
        public void Stop()
        {
            if (activeCoroutine != null && host != null)
                host.StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            playing = false;
            queue.Clear();
            KnoxumLuaAudioHelper.StopNPCAudio(npc);
        }

        /// <summary>True while the sequence is playing.</summary>
        public bool isPlaying => playing;

        /// <summary>Lines remaining in queue.</summary>
        public int remainingLines => queue.Count;

        private IEnumerator PlaySequence()
        {
            playing = true;

            while (queue.Count > 0)
            {
                TalkLine line = queue.Dequeue();

                if (line.pauseAfter > 0f)
                {
                    yield return new WaitForSeconds(line.pauseAfter);
                    continue;
                }

                KnoxumLuaAudioHelper.PlayNPCSound(npc, line.soundId, line.subtitleText);

                // Wait for the audio clip to finish
                AudioManager audMan = KnoxumLuaAudioHelper.GetNPCAudioManager(npc);
                if (audMan != null && audMan.audioDevice != null)
                {
                    yield return new WaitWhile(() => audMan.audioDevice != null && audMan.audioDevice.isPlaying);
                }
                else
                {
                    // Fallback: wait a fixed duration
                    yield return new WaitForSeconds(3f);
                }
            }

            playing = false;
        }
    }

    // ========================================================================
    // 2. DOOR PROXY — uses reflection for state (GetOpen/GetLocked don't exist)
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public class DoorProxy
    {
        [MoonSharp.Interpreter.MoonSharpHidden] public Door door;

        // Cached reflection fields
        private static FieldInfo _lockedField;
        private static FieldInfo _openField;

        public DoorProxy(Door door)
        {
            this.door = door;
        }

        private static FieldInfo GetLockedField()
        {
            if (_lockedField == null)
            {
                _lockedField = AccessTools.Field(typeof(Door), "locked");
                if (_lockedField == null)
                    _lockedField = AccessTools.Field(typeof(Door), "isLocked");
            }
            return _lockedField;
        }

        private static FieldInfo GetOpenField()
        {
            if (_openField == null)
            {
                _openField = AccessTools.Field(typeof(Door), "open");
                if (_openField == null)
                    _openField = AccessTools.Field(typeof(Door), "isOpen");
            }
            return _openField;
        }

        /// <summary>Check if door is locked (via reflection).</summary>
        public bool isLocked
        {
            get
            {
                if (door == null) return false;
                var field = GetLockedField();
                if (field != null)
                    return (bool)field.GetValue(door);
                return false;
            }
        }

        /// <summary>Check if door is open (via reflection).</summary>
        public bool isOpen
        {
            get
            {
                if (door == null) return false;
                var field = GetOpenField();
                if (field != null)
                    return (bool)field.GetValue(door);
                return false;
            }
        }

        /// <summary>"Open", "Closed", or "Locked".</summary>
        public string state
        {
            get
            {
                if (door == null) return "Unknown";
                if (isLocked) return "Locked";
                if (isOpen) return "Open";
                return "Closed";
            }
        }

        /// <summary>Open the door. cancelTimer=true stops any timed lock.</summary>
        public void Open(bool cancelTimer = false, bool openAll = false)
        {
            if (door == null) return;
            door.Open(cancelTimer, openAll);
        }

        /// <summary>Close the door.</summary>
        public void Close()
        {
            if (door == null) return;
            door.Shut();
        }

        /// <summary>Lock the door. If shut=true, close it first.</summary>
        public void Lock(bool shut)
        {
            if (door == null) return;
            if (shut) door.Shut();
            door.Lock(false);
        }

        /// <summary>Lock the door for a duration, then close it first.</summary>
        public void LockTimed(float time)
        {
            if (door == null) return;
            door.Shut();
            door.LockTimed(time);
        }

        /// <summary>Unlock the door.</summary>
        public void Unlock()
        {
            if (door == null) return;
            door.Unlock();
        }

        /// <summary>Cell on side A of the door.</summary>
        public CellProxy GetCellA()
        {
            if (door == null || door.ec == null) return null;
            Cell cell = door.ec.CellFromPosition(door.position);
            return cell != null ? new CellProxy(cell) : null;
        }

        /// <summary>Cell on side B of the door (opposite side).</summary>
        public CellProxy GetCellB()
        {
            if (door == null || door.ec == null) return null;
            IntVector2 opposite = door.position;
            switch (door.direction)
            {
                case Direction.North: opposite.z += 1; break;
                case Direction.South: opposite.z -= 1; break;
                case Direction.East:  opposite.x += 1; break;
                case Direction.West:  opposite.x -= 1; break;
            }
            Cell cell = door.ec.CellFromPosition(opposite);
            return cell != null ? new CellProxy(cell) : null;
        }

        public override string ToString()
        {
            return "Door@" + (door != null ? door.position.ToString() : "null");
        }
    }

    // ========================================================================
    // 3. AUDIO HELPER — shared by TalkProxy, NPCProxy, EditorLuaGameProxy
    // ========================================================================

    public static class KnoxumLuaAudioHelper
    {
        private static FieldInfo _audManField;

        /// <summary>Get the AudioManager from an NPC (tries component, then field).</summary>
        public static AudioManager GetNPCAudioManager(NPC npc)
        {
            if (npc == null) return null;

            // Try component first
            AudioManager audMan = npc.GetComponent<AudioManager>();
            if (audMan != null) return audMan;

            // Try common field names via reflection
            if (_audManField == null)
            {
                _audManField = AccessTools.Field(typeof(NPC), "audMan");
                if (_audManField == null)
                    _audManField = AccessTools.Field(typeof(NPC), "audioManager");
            }

            if (_audManField != null)
                return _audManField.GetValue(npc) as AudioManager;

            return null;
        }

        /// <summary>Play a registered sound through an NPC's AudioManager.</summary>
        public static void PlayNPCSound(NPC npc, string soundId, string subtitleOverride = null)
        {
            if (npc == null) return;

            SoundObject soundObj = ResolveSound(soundId);
            if (soundObj == null) return;

            AudioManager audMan = GetNPCAudioManager(npc);
            if (audMan == null) return;

            audMan.PlaySingle(soundObj);
        }

        /// <summary>Stop NPC audio and clear queue.</summary>
        public static void StopNPCAudio(NPC npc)
        {
            if (npc == null) return;
            AudioManager audMan = GetNPCAudioManager(npc);
            if (audMan == null) return;

            try { audMan.FlushQueue(true); } catch { }
            try
            {
                if (audMan.audioDevice != null && audMan.audioDevice.isPlaying)
                    audMan.audioDevice.Stop();
            }
            catch { }
        }

        /// <summary>Check if NPC is currently playing audio.</summary>
        public static bool IsNPCTalking(NPC npc)
        {
            if (npc == null) return false;
            AudioManager audMan = GetNPCAudioManager(npc);
            if (audMan == null) return false;
            return audMan.audioDevice != null && audMan.audioDevice.isPlaying;
        }

        /// <summary>Resolve a sound ID to SoundObject.</summary>
        public static SoundObject ResolveSound(string soundId)
        {
            if (string.IsNullOrEmpty(soundId)) return null;

            try
            {
                var plugin = LevelStudioPlugin.Instance;
                if (plugin != null)
                {
                    var soundsField = AccessTools.Field(plugin.GetType(), "sounds");
                    if (soundsField != null)
                    {
                        var dict = soundsField.GetValue(plugin) as Dictionary<string, SoundObject>;
                        if (dict != null && dict.ContainsKey(soundId))
                            return dict[soundId];
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>Play a sound through the global player AudioManager.</summary>
        public static void PlayGlobalSound(string soundId)
        {
            SoundObject soundObj = ResolveSound(soundId);
            if (soundObj == null) return;

            Singleton<CoreGameManager>.Instance.audMan.PlaySingle(soundObj);
        }
    }

    // ========================================================================
    // 4. EXTENDED NPC PROXY — Say, behavior
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public static class NPCProxyExtensions
    {
        /// <summary>Make this NPC say a registered sound.</summary>
        public static void Say(this NPCProxy proxy, string soundId)
        {
            if (proxy == null || proxy.npc == null) return;
            KnoxumLuaAudioHelper.PlayNPCSound(proxy.npc, soundId);
            KnoxumLuaCallbacks.FireOnNPCTalk(proxy, soundId);
        }

        /// <summary>Make this NPC say a sound with custom subtitle text.</summary>
        public static void SayWithSubtitle(this NPCProxy proxy, string soundId, string subtitleText)
        {
            if (proxy == null || proxy.npc == null) return;
            KnoxumLuaAudioHelper.PlayNPCSound(proxy.npc, soundId, subtitleText);
            KnoxumLuaCallbacks.FireOnNPCTalk(proxy, soundId);
        }

        /// <summary>Stop all speech from this NPC.</summary>
        public static void StopTalking(this NPCProxy proxy)
        {
            if (proxy == null || proxy.npc == null) return;
            KnoxumLuaAudioHelper.StopNPCAudio(proxy.npc);
        }

        /// <summary>True if this NPC is currently playing a sound.</summary>
        public static bool IsTalking(this NPCProxy proxy)
        {
            if (proxy == null || proxy.npc == null) return false;
            return KnoxumLuaAudioHelper.IsNPCTalking(proxy.npc);
        }

        /// <summary>Create a queued talk sequence for this NPC.</summary>
        public static TalkProxy CreateTalk(this NPCProxy proxy)
        {
            if (proxy == null || proxy.npc == null) return null;
            return new TalkProxy
            {
                npc = proxy.npc,
                host = proxy.npc
            };
        }

        /// <summary>Get the current behavior state name (via reflection).</summary>
        public static string GetBehaviorState(this NPCProxy proxy)
        {
            if (proxy == null || proxy.npc == null) return "Unknown";

            try
            {
                var bsm = AccessTools.Field(typeof(NPC), "behaviorStateMachine");
                if (bsm != null)
                {
                    var machine = bsm.GetValue(proxy.npc);
                    if (machine != null)
                    {
                        var currentStateProp = machine.GetType()
                            .GetProperty("CurrentState",
                                BindingFlags.Public | BindingFlags.Instance);
                        if (currentStateProp != null)
                        {
                            var state = currentStateProp.GetValue(machine);
                            if (state != null) return state.GetType().Name;
                        }
                    }
                }
            }
            catch { }

            return "Unknown";
        }
    }

    // ========================================================================
    // 5. EXTENDED CELL PROXY — neighbors, doors, occupancy
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public static class CellProxyExtensions
    {
        /// <summary>Get the 4 neighboring cells (N, E, S, W).</summary>
        public static List<CellProxy> GetNeighbors(this CellProxy proxy)
        {
            if (proxy == null) return new List<CellProxy>();

            var pos = proxy.position;
            var neighbors = new List<CellProxy>();

            EnvironmentController ec = GetEC();
            if (ec == null) return neighbors;

            int[][] offsets = new int[][] {
                new int[] { 0, 1 },   // North
                new int[] { 1, 0 },   // East
                new int[] { 0, -1 },  // South
                new int[] { -1, 0 }   // West
            };

            foreach (var off in offsets)
            {
                Cell neighbor = SafeGetCell(ec, pos.x + off[0], pos.z + off[1]);
                if (neighbor != null)
                    neighbors.Add(new CellProxy(neighbor));
            }

            return neighbors;
        }

        /// <summary>Get doors connected to this cell.</summary>
        public static List<DoorProxy> GetDoors(this CellProxy proxy)
        {
            var doors = new List<DoorProxy>();
            if (proxy == null) return doors;

            EnvironmentController ec = GetEC();
            if (ec == null) return doors;

            Cell cell = ec.CellFromPosition(proxy.position.ToVector());
            if (cell == null) return doors;

            if (cell.ObjectBase != null)
            {
                Door[] doorComps = cell.ObjectBase.GetComponentsInChildren<Door>();
                foreach (var d in doorComps)
                {
                    if (d != null) doors.Add(new DoorProxy(d));
                }
            }

            if (cell.room != null)
            {
                foreach (var d in cell.room.doors)
                {
                    if (d != null && !doors.Exists(x => x.door == d))
                        doors.Add(new DoorProxy(d));
                }
            }

            return doors;
        }

        /// <summary>True if any NPC is on this cell.</summary>
        public static bool HasNPC(this CellProxy proxy)
        {
            if (proxy == null) return false;
            EnvironmentController ec = GetEC();
            if (ec == null) return false;

            foreach (var npc in ec.Npcs)
            {
                if (npc == null) continue;
                Cell npcCell = ec.CellFromPosition(npc.transform.position);
                if (npcCell != null && npcCell.position == proxy.position.ToVector())
                    return true;
            }
            return false;
        }

        /// <summary>True if any player is on this cell.</summary>
        public static bool HasPlayer(this CellProxy proxy)
        {
            if (proxy == null) return false;

            try
            {
                // Single-player: just check player 0
                var pm = Singleton<CoreGameManager>.Instance.GetPlayer(0);
                if (pm == null) return false;

                EnvironmentController ec = GetEC();
                if (ec == null) return false;

                Cell playerCell = ec.CellFromPosition(pm.transform.position);
                if (playerCell != null && playerCell.position == proxy.position.ToVector())
                    return true;
            }
            catch { }

            return false;
        }

        private static EnvironmentController GetEC()
        {
            if (Singleton<BaseGameManager>.Instance != null)
                return Singleton<BaseGameManager>.Instance.Ec;
            return null;
        }

        private static Cell SafeGetCell(EnvironmentController ec, int x, int z)
        {
            if (ec == null || ec.cells == null) return null;
            if (x < 0 || z < 0 || x >= ec.cells.GetLength(0) || z >= ec.cells.GetLength(1))
                return null;
            return ec.cells[x, z];
        }
    }

    // ========================================================================
    // 6. EXTENDED ROOM PROXY
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public static class RoomProxyExtensions
    {
        /// <summary>Get all doors in this room.</summary>
        public static List<DoorProxy> GetDoors(this RoomProxy proxy)
        {
            var doors = new List<DoorProxy>();
            var rc = GetRoomController(proxy);
            if (rc == null) return doors;

            foreach (var d in rc.doors)
            {
                if (d != null) doors.Add(new DoorProxy(d));
            }
            return doors;
        }

        /// <summary>Lock all doors in this room for a duration.</summary>
        public static void LockAllDoorsTimed(this RoomProxy proxy, float time)
        {
            var rc = GetRoomController(proxy);
            if (rc == null) return;

            foreach (var d in rc.doors)
            {
                if (d == null) continue;
                d.Shut();
                d.LockTimed(time);
            }
        }

        private static RoomController GetRoomController(RoomProxy proxy)
        {
            if (proxy == null) return null;
            var field = AccessTools.Field(typeof(RoomProxy), "roomController");
            return field?.GetValue(proxy) as RoomController;
        }
    }

    // ========================================================================
    // 7. EXTENDED EDITOR LUA GAME PROXY — timer, HUD, map, audio, FX, talk
    // ========================================================================

    [MoonSharp.Interpreter.MoonSharpUserData]
    public static class EditorLuaGameProxyExtensions
    {
        // --- Talk ---

        /// <summary>Create a queued talk sequence for an NPC by ID.</summary>
        public static TalkProxy CreateTalk(this EditorLuaGameProxy proxy, string npcId)
        {
            if (proxy == null) return null;

            NPCProxy npcProxy = proxy.GetNPC(npcId);
            if (npcProxy == null) return null;

            return new TalkProxy
            {
                npc = npcProxy.npc,
                host = npcProxy.npc
            };
        }

        // --- Timer ---

        private static float timerSeconds;
        private static bool timerRunning;
        private static float timerElapsed;

        public static void SetTimer(this EditorLuaGameProxy proxy, float seconds)
        {
            timerSeconds = seconds;
            timerElapsed = 0f;
            timerRunning = true;
        }

        public static void StopTimer(this EditorLuaGameProxy proxy)
        {
            timerRunning = false;
        }

        public static float GetTimerValue(this EditorLuaGameProxy proxy)
        {
            if (!timerRunning) return 0f;
            return Mathf.Max(0f, timerSeconds - timerElapsed);
        }

        public static bool IsTimerRunning(this EditorLuaGameProxy proxy)
        {
            return timerRunning;
        }

        internal static void TickTimer(float dt)
        {
            if (!timerRunning) return;
            timerElapsed += dt;
            if (timerElapsed >= timerSeconds)
            {
                timerRunning = false;
                KnoxumLuaCallbacks.FireOnTimerExpired();
            }
        }

        // --- HUD ---

        private static GameObject hudMessageObject;
        private static Text hudMessageText;
        private static float hudMessageTimer;

        public static void ShowMessage(this EditorLuaGameProxy proxy, string text, float duration)
        {
            EnsureHUDMessageObject();
            if (hudMessageText != null)
            {
                hudMessageText.text = text;
                hudMessageObject.SetActive(true);
                hudMessageTimer = duration;
            }
        }

        public static void HideMessage(this EditorLuaGameProxy proxy)
        {
            if (hudMessageObject != null)
                hudMessageObject.SetActive(false);
            hudMessageTimer = 0f;
        }

        public static void SetNotebookDisplay(this EditorLuaGameProxy proxy, bool visible)
        {
            try
            {
                Singleton<CoreGameManager>.Instance.GetHud(0).SetNotebookDisplay(visible);
            }
            catch { }
        }

        private static void EnsureHUDMessageObject()
        {
            if (hudMessageObject != null) return;

            Canvas canvas = null;
            try
            {
                canvas = Singleton<CoreGameManager>.Instance.GetHud(0)
                    .GetComponentInChildren<Canvas>();
            }
            catch { }

            if (canvas == null) return;

            hudMessageObject = new GameObject("KnoxumLuaMessage");
            hudMessageObject.transform.SetParent(canvas.transform, false);

            RectTransform rt = hudMessageObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.85f);
            rt.anchorMax = new Vector2(0.5f, 0.85f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 60f);

            hudMessageText = hudMessageObject.AddComponent<Text>();
            hudMessageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hudMessageText.fontSize = 24;
            hudMessageText.alignment = TextAnchor.MiddleCenter;
            hudMessageText.color = Color.white;
            hudMessageText.text = "";

            hudMessageObject.SetActive(false);
        }

        internal static void TickHUDMessage(float dt)
        {
            if (hudMessageTimer <= 0f) return;
            hudMessageTimer -= dt;
            if (hudMessageTimer <= 0f)
            {
                if (hudMessageObject != null)
                    hudMessageObject.SetActive(false);
            }
        }

        // --- Map ---

        public static void RevealMap(this EditorLuaGameProxy proxy)
        {
            try { Singleton<BaseGameManager>.Instance.Ec.map.CompleteMap(); } catch { }
        }

        // --- Audio ---

        public static void PlaySound(this EditorLuaGameProxy proxy, string soundId)
        {
            KnoxumLuaAudioHelper.PlayGlobalSound(soundId);
        }

        // --- Visual Effects ---

        private static Camera mainCam;
        private static float shakeTimer;
        private static float shakeIntensity;
        private static Vector3 shakeOriginalPos;

        public static void ShakeScreen(this EditorLuaGameProxy proxy, float intensity, float duration)
        {
            if (mainCam == null)
                mainCam = Camera.main;
            if (mainCam == null) return;

            shakeIntensity = intensity;
            shakeTimer = duration;
            shakeOriginalPos = mainCam.transform.localPosition;
        }

        public static void FlashScreen(this EditorLuaGameProxy proxy, int r, int g, int b, float duration)
        {
            try
            {
                Canvas canvas = Singleton<CoreGameManager>.Instance.GetHud(0)
                    .GetComponentInChildren<Canvas>();
                if (canvas == null) return;

                GameObject flash = new GameObject("KnoxumLuaFlash");
                flash.transform.SetParent(canvas.transform, false);

                RectTransform rt = flash.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                Image img = flash.AddComponent<Image>();
                img.color = new Color(r / 255f, g / 255f, b / 255f, 0.7f);
                img.raycastTarget = false;

                var runner = flash.AddComponent<KnoxumLuaCoroutineRunner>();
                runner.StartCoroutine(FadeAndDestroy(flash, img, duration));
            }
            catch { }
        }

        private static IEnumerator FadeAndDestroy(GameObject obj, Image img, float duration)
        {
            float elapsed = 0f;
            Color startColor = img.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                img.color = new Color(startColor.r, startColor.g, startColor.b,
                    Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            UnityEngine.Object.Destroy(obj);
        }

        internal static void TickScreenShake(float dt)
        {
            if (shakeTimer <= 0f) return;
            if (mainCam == null) return;

            shakeTimer -= dt;
            if (shakeTimer > 0f)
            {
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * shakeIntensity;
                randomOffset.z = 0f;
                mainCam.transform.localPosition = shakeOriginalPos + randomOffset;
            }
            else
            {
                mainCam.transform.localPosition = shakeOriginalPos;
            }
        }
    }

    // Internal MonoBehaviour for running coroutines from static context
    internal class KnoxumLuaCoroutineRunner : MonoBehaviour { }

    // ========================================================================
    // 8. NEW LUA CALLBACKS — fired from Harmony patches
    // ========================================================================

    public static class KnoxumLuaCallbacks
    {
        public static CustomChallengeManager GetCCM()
        {
            if (Singleton<BaseGameManager>.Instance is CustomChallengeManager ccm)
                return ccm;
            return null;
        }

        private static MoonSharp.Interpreter.Script GetScript()
        {
            var ccm = GetCCM();
            return ccm?.script;
        }

        private static void FireCallback(string name, params object[] args)
        {
            var script = GetScript();
            if (script == null) return;

            MoonSharp.Interpreter.DynValue func = script.Globals.Get(name);
            if (func.Type != MoonSharpType.Function) return;

            try
            {
                if (args != null && args.Length > 0)
                    script.Call(func, args);
                else
                    script.Call(func);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KnoxumLua] Callback '" + name + "' error: " + e.Message);
            }
        }

        public static void FireOnNPCTalk(NPCProxy npc, string soundId)
        {
            FireCallback("OnNPCTalk", npc, soundId);
        }

        public static void FireOnDoorOpened(DoorProxy door)
        {
            FireCallback("OnDoorOpened", door);
        }

        public static void FireOnDoorClosed(DoorProxy door)
        {
            FireCallback("OnDoorClosed", door);
        }

        public static void FireOnTimerExpired()
        {
            FireCallback("OnTimerExpired");
        }

        public static void FireOnPlayerEnterRoom(RoomProxy room)
        {
            FireCallback("OnPlayerEnterRoom", room);
        }

        public static void FireOnPlayerExitRoom(RoomProxy room)
        {
            FireCallback("OnPlayerExitRoom", room);
        }

        public static void FireOnNPCEnterRoom(NPCProxy npc, RoomProxy room)
        {
            FireCallback("OnNPCEnterRoom", npc, room);
        }
    }

    // ========================================================================
    // 9. HARMONY PATCHES
    // ========================================================================

    // --- Update tick for timer, HUD, screen shake ---

    [HarmonyPatch(typeof(CustomChallengeManager), "Update")]
    internal static class Patch_CustomChallengeManager_Update
    {
        private static void Postfix()
        {
            float dt = Time.deltaTime;
            EditorLuaGameProxyExtensions.TickTimer(dt);
            EditorLuaGameProxyExtensions.TickHUDMessage(dt);
            EditorLuaGameProxyExtensions.TickScreenShake(dt);
        }
    }

    // --- NPC Talk callback ---

    [HarmonyPatch(typeof(AudioManager), "PlaySingle")]
    internal static class Patch_AudioManager_PlaySingle
    {
        private static void Postfix(AudioManager __instance, SoundObject sound)
        {
            NPC npc = __instance.GetComponent<NPC>();
            if (npc == null) return;

            var ccm = KnoxumLuaCallbacks.GetCCM();
            if (ccm == null) return;

            string soundId = "unknown";
            try
            {
                var plugin = LevelStudioPlugin.Instance;
                if (plugin != null)
                {
                    var soundsField = AccessTools.Field(plugin.GetType(), "sounds");
                    if (soundsField != null)
                    {
                        var dict = soundsField.GetValue(plugin) as Dictionary<string, SoundObject>;
                        if (dict != null)
                        {
                            foreach (var kv in dict)
                            {
                                if (kv.Value == sound)
                                {
                                    soundId = kv.Key;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            KnoxumLuaCallbacks.FireOnNPCTalk(ccm.myProxy.GetProxyForNPC(npc), soundId);
        }
    }

    // --- Door state change callbacks ---

    [HarmonyPatch(typeof(Door), "Open")]
    internal static class Patch_Door_Open
    {
        private static void Postfix(Door __instance)
        {
            KnoxumLuaCallbacks.FireOnDoorOpened(new DoorProxy(__instance));
        }
    }

    [HarmonyPatch(typeof(Door), "Shut")]
    internal static class Patch_Door_Shut
    {
        private static void Postfix(Door __instance)
        {
            KnoxumLuaCallbacks.FireOnDoorClosed(new DoorProxy(__instance));
        }
    }

    // --- Player room entry/exit tracking ---

    [HarmonyPatch(typeof(PlayerManager), "EnterRoom")]
    internal static class Patch_PlayerManager_EnterRoom
    {
        private static void Postfix(PlayerManager __instance, RoomController room)
        {
            if (room == null) return;
            KnoxumLuaCallbacks.FireOnPlayerEnterRoom(new RoomProxy(room));
        }
    }

    [HarmonyPatch(typeof(PlayerManager), "ExitRoom")]
    internal static class Patch_PlayerManager_ExitRoom
    {
        private static void Postfix(PlayerManager __instance, RoomController room)
        {
            if (room == null) return;
            KnoxumLuaCallbacks.FireOnPlayerExitRoom(new RoomProxy(room));
        }
    }

    // --- Register extension types with MoonSharp on startup ---

    [HarmonyPatch(typeof(CustomChallengeManager), "InitializeScriptGlobals")]
    internal static class Patch_CustomChallengeManager_InitGlobals
    {
        private static void Postfix(CustomChallengeManager __instance)
        {
            // Register extension types
            MoonSharpUserDataUtil.RegisterExtensionType(typeof(NPCProxyExtensions));
            MoonSharpUserDataUtil.RegisterExtensionType(typeof(CellProxyExtensions));
            MoonSharpUserDataUtil.RegisterExtensionType(typeof(RoomProxyExtensions));
            MoonSharpUserDataUtil.RegisterExtensionType(typeof(EditorLuaGameProxyExtensions));

            // Register new types
            MoonSharpUserDataUtil.RegisterType<TalkProxy>();
            MoonSharpUserDataUtil.RegisterType<TalkLine>();
            MoonSharpUserDataUtil.RegisterType<DoorProxy>();

            // Inject globals
            __instance.script.Globals["wait"] = (Func<float, IEnumerator>)(seconds =>
                WaitCoroutine(seconds));

            __instance.script.Globals["GetAllDoors"] = (Func<List<DoorProxy>>)(() =>
                GetAllDoorsInLevel());

            __instance.script.Globals["SetTimer"] = (Action<float>)(sec =>
                EditorLuaGameProxyExtensions.SetTimer(__instance.myProxy, sec));
            __instance.script.Globals["StopTimer"] = (Action)(() =>
                EditorLuaGameProxyExtensions.StopTimer(__instance.myProxy));
            __instance.script.Globals["GetTimerValue"] = (Func<float>)(() =>
                EditorLuaGameProxyExtensions.GetTimerValue(__instance.myProxy));

            __instance.script.Globals["ShowMessage"] = (Action<string, float>)((text, dur) =>
                EditorLuaGameProxyExtensions.ShowMessage(__instance.myProxy, text, dur));
            __instance.script.Globals["HideMessage"] = (Action)(() =>
                EditorLuaGameProxyExtensions.HideMessage(__instance.myProxy));

            __instance.script.Globals["ShakeScreen"] = (Action<float, float>)((intensity, dur) =>
                EditorLuaGameProxyExtensions.ShakeScreen(__instance.myProxy, intensity, dur));
            __instance.script.Globals["FlashScreen"] = (Action<int, int, int, float>)((r, g, b, dur) =>
                EditorLuaGameProxyExtensions.FlashScreen(__instance.myProxy, r, g, b, dur));

            Debug.Log("[KnoxumLua] Extension globals registered");
        }

        private static IEnumerator WaitCoroutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private static List<DoorProxy> GetAllDoorsInLevel()
        {
            var doors = new List<DoorProxy>();
            var bgm = Singleton<BaseGameManager>.Instance;
            if (bgm == null) return doors;
            var ec = bgm.Ec;
            if (ec == null) return doors;

            foreach (var room in ec.rooms)
            {
                if (room == null) continue;
                foreach (var door in room.doors)
                {
                    if (door != null) doors.Add(new DoorProxy(door));
                }
            }
            return doors;
        }
    }
}
