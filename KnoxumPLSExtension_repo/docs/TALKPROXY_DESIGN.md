# TalkProxy Design — NPC Dialogue from Lua

## Problem
Lua scripts for custom challenges cannot make NPCs speak.
`self:PlaySoundObject(sound)` plays through the player, not NPC.

## BB+ Audio System (confirmed from decompiled code)

```csharp
// Player audio (existing Lua API):
Singleton<CoreGameManager>.Instance.audMan.PlaySingle(
    LevelStudioPlugin.Instance.sounds[soundId]
);

// NPC audio (what we need):
// AudioManager is a MonoBehaviour on the NPC GameObject
AudioManager audMan = npc.GetComponent<AudioManager>();
audMan.PlaySingle(soundObject);
// OR if audMan is a field on NPC:
// npc.audMan.PlaySingle(soundObject);

// Sound objects are registered in:
LevelStudioPlugin.Instance.sounds   // PLS sound aliases
LevelLoaderPlugin.Instance...       // level loader aliases
```

## What to Add

### 1. NPCProxy — add Say/StopTalking

```csharp
[MoonSharpUserData]
public class NPCProxy
{
    // ... existing fields ...
    
    // NEW:
    public void Say(string soundId)
    {
        // Get AudioManager from NPC
        AudioManager audMan = this.npc.GetComponent<AudioManager>();
        if (audMan == null) return;
        
        if (!LevelStudioPlugin.Instance.sounds.ContainsKey(soundId))
            return;
        
        audMan.PlaySingle(LevelStudioPlugin.Instance.sounds[soundId]);
    }
    
    public void StopTalking()
    {
        AudioManager audMan = this.npc.GetComponent<AudioManager>();
        if (audMan == null) return;
        audMan.FlushQueue(true); // or StopAudio equivalent
    }
    
    public bool isTalking
    {
        get
        {
            AudioManager audMan = this.npc.GetComponent<AudioManager>();
            return audMan != null && audMan.audioDevice.isPlaying;
        }
    }
}
```

### 2. TalkProxy — queued dialogue (NEW class)

```csharp
[MoonSharpUserData]
public class TalkProxy
{
    [MoonSharpHidden] public NPC npc;
    private Queue<TalkLine> lines = new Queue<TalkLine>();
    private Coroutine playCoroutine;
    private bool playing;
    
    private struct TalkLine
    {
        public string soundId;
        public string subtitleOverride;
        public float pauseAfter;
    }
    
    public TalkProxy AddLine(string soundId)
    {
        lines.Enqueue(new TalkLine { soundId = soundId });
        return this; // fluent
    }
    
    public TalkProxy AddLine(string soundId, string subtitle)
    {
        lines.Enqueue(new TalkLine { soundId = soundId, subtitleOverride = subtitle });
        return this;
    }
    
    public TalkProxy AddPause(float seconds)
    {
        lines.Enqueue(new TalkLine { pauseAfter = seconds });
        return this;
    }
    
    public void Play()
    {
        if (playing) Stop();
        playCoroutine = npc.StartCoroutine(PlaySequence());
    }
    
    public void Stop()
    {
        if (playCoroutine != null)
            npc.StopCoroutine(playCoroutine);
        playing = false;
        // Flush AudioManager queue
        AudioManager audMan = npc.GetComponent<AudioManager>();
        if (audMan != null) audMan.FlushQueue(true);
    }
    
    public bool isPlaying => playing;
    public int remainingLines => lines.Count;
    
    private IEnumerator PlaySequence()
    {
        playing = true;
        AudioManager audMan = npc.GetComponent<AudioManager>();
        
        while (lines.Count > 0)
        {
            var line = lines.Dequeue();
            
            if (line.pauseAfter > 0)
            {
                yield return new WaitForSeconds(line.pauseAfter);
                continue;
            }
            
            if (audMan != null && LevelStudioPlugin.Instance.sounds.ContainsKey(line.soundId))
            {
                audMan.PlaySingle(LevelStudioPlugin.Instance.sounds[line.soundId]);
                
                // Wait for audio to finish
                yield return new WaitWhile(() => audMan.audioDevice.isPlaying);
            }
        }
        
        playing = false;
    }
}
```

### 3. EditorLuaGameProxy — add CreateTalk

```csharp
public TalkProxy CreateTalk(string npcId)
{
    NPCProxy npcProxy = this.GetNPC(npcId);
    if (npcProxy == null) return null;
    
    return new TalkProxy { npc = npcProxy.npc };
}
```

### 4. New Lua callback

```csharp
// In Harmony patch for AudioManager.PlaySingle:
[HarmonyPatch(typeof(AudioManager), "PlaySingle")]
class Patch_AudioManager_PlaySingle
{
    static void Postfix(AudioManager __instance, SoundObject sound)
    {
        // Check if this AudioManager belongs to an NPC
        NPC npc = __instance.GetComponent<NPC>();
        if (npc == null) return;
        
        // Fire Lua callback
        if (CustomChallengeManager.Instance?.script != null)
        {
            var ccm = CustomChallengeManager.Instance;
            if (ccm.script.Globals.Get("OnNPCTalk").Type == DataType.Function)
            {
                ccm.script.Call(ccm.script.Globals["OnNPCTalk"],
                    ccm.myProxy.GetProxyForNPC(npc),
                    LuaHelpers.GetIDFromSoundObject(sound)
                );
            }
        }
    }
}
```

## Lua Usage Examples

```lua
-- Simple NPC speech
function Initialize()
    self:GetBaldi():Say("BAL_Hello")
end

-- Queued dialogue with pauses
function Initialize()
    local talk = self:GetBaldi():CreateTalk()
    talk:AddLine("BAL_Hello")
           :AddPause(2.0)
           :AddLine("BAL_Countdown")
           :AddPause(1.0)
           :AddLine("BAL_Angry")
           :Play()
end

-- NPC reacts when player enters room
function OnPlayerEnterRoom(room)
    local principal = self:GetNPC("principal")
    if principal and room.name == "Faculty" then
        principal:Say("PRI_NoRunning")
    end
end

-- Check if NPC is talking
function Update(dt)
    local baldi = self:GetBaldi()
    if baldi and baldi.isTalking then
        -- do something while Baldi talks
    end
end
```

## Implementation Steps
1. Add `Say()`, `StopTalking()`, `isTalking` to NPCProxy
2. Create TalkProxy class
3. Add `CreateTalk()` to EditorLuaGameProxy
4. Add Harmony patch for AudioManager.PlaySingle → OnNPCTalk callback
5. Test with existing SoundObject aliases
