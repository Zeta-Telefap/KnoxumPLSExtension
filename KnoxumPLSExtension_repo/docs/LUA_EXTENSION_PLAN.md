# Lua Extension Plan — KnoxumPLSExtension

## Current API Coverage Analysis

### ✅ Well Covered
- Player stats, inventory, stickers, movement
- NPC position, movement, squish, arrows on map
- Baldi anger, slap, praise
- Rooms: doors, power, lights, cells, activities
- Elevators: state, get all, set all
- Game flow: win/lose, exits, events
- Time scales: NPC, environment, player

### ❌ Missing / Limited

#### 1. Cell — very thin proxy
Current: `position`, `FloorWorldPosition`, `CenterWorldPosition`, `GetRoom()`, `GetLight()`, `SetLight()`, `PressAllButtons()`
Missing:
- `GetNeighbors()` → list of adjacent CellProxy
- `GetDoors()` → list of DoorProxy
- `GetItems()` → list of items on this cell
- `IsOccupied()` → has NPC or player
- `GetAllObjects()` → ObjectBase children
- `SetWallState(direction, state)` → open/close/lock walls

#### 2. No DoorProxy at all
- `state` (open/closed/locked)
- `Open()`, `Close()`, `Lock(time)`, `Unlock()`
- `isLocked`, `isOpen`
- `SetVisiblity(bool)` — hide/show

#### 3. No Timer/HUD control
- `SetTimer(seconds)` — start countdown
- `StopTimer()`
- `GetTimerValue()`
- `ShowMessage(text, duration)` — subtitle/hud text
- `SetNotebookDisplay(bool)`

#### 4. No Map control
- `GetMapSize()` → IntVector2
- `RevealMap()`
- `HideMap()`
- `AddMapIcon(position, type, color)`
- `RemoveMapIcon(icon)`
- `SetMapTileVisible(x, z, bool)`

#### 5. No NPC behavior control
- `SetBehaviorState(npcId, state)` — force state machine
- `GetBehaviorState(npcId)` → string
- `SetNavigationTarget(npcId, position)`
- `Wander(npcId)`
- `ForceSeekPlayer(npcId)`
- `SetActivityOverride(npcId, ActivityOverride)` — custom behavior

#### 6. No Audio control beyond PlaySoundObject
- `PlayMusic(soundId, loop)`
- `StopMusic()`
- `PlayAmbient(soundId)`
- `SetMusicVolume(0-1)`

#### 7. No Visual effects
- `SpawnParticles(type, position, color, duration)`
- `FlashScreen(color, duration)`
- `ShakeScreen(intensity, duration)`
- `SetFogDensity(density)`
- `SetLightingColor(color)` — global ambient

#### 8. No Room creation/destruction at runtime
- `CreateRoom(cells, category, name)`
- `DestroyRoom(roomProxy)`
- `SetRoomCategory(room, category)`

#### 9. No Item pickup interaction control
- `GetPickupsInRoom(room)` → list
- `RespawnPickup(pickup)`
- `RemovePickup(pickup)`
- `SetPickupItem(pickup, itemId)`

#### 10. No coroutine/async support
- `Wait(seconds)` — Lua coroutine yield
- `WaitForCondition(func)` — yield until condition true
- `ScheduleRepeating(seconds, func)` — periodic callback

---

## Priority Implementation Plan

### Phase 1 — High Impact, Low Complexity
These add the most value with minimal new code:

#### 1a. DoorProxy (NEW class)
```csharp
[MoonSharpUserData]
public class DoorProxy
{
    public Door door; // [MoonSharpHidden]
    
    public string state { get; set; }  // "Open", "Closed", "Locked"
    public bool isOpen { get; }
    public bool isLocked { get; }
    
    public void Open();
    public void Close();
    public void Lock(bool shut);
    public void LockTimed(float time);
    public void Unlock();
    
    public CellProxy GetCellA();
    public CellProxy GetCellB();
}
```

#### 1b. Expand CellProxy
```csharp
// Add to existing CellProxy:
public List<CellProxy> GetNeighbors();
public List<DoorProxy> GetDoors();
public bool HasNPC();
public bool HasPlayer();
```

#### 1c. Expand RoomProxy
```csharp
// Add to existing RoomProxy:
public List<DoorProxy> GetDoors();
public int GetActivityNotebookCount();
```

#### 1d. Timer/HUD control on EditorLuaGameProxy
```csharp
// Add to EditorLuaGameProxy:
public void SetTimer(float seconds);
public void StopTimer();
public float GetTimerValue();
public void ShowMessage(string text, float duration);
public void SetNotebookDisplay(bool visible);
```

### Phase 2 — Medium Complexity

#### 2a. Map control on EditorLuaGameProxy
```csharp
public void RevealMap();
public void HideMap();
public void RevealMapArea(IntVector2Proxy center, int radius);
public void SetMapTileVisible(int x, int z, bool visible);
```

#### 2b. Audio control on EditorLuaGameProxy
```csharp
public void PlayMusic(string soundId, bool loop);
public void StopMusic();
public void PlayAmbient(string soundId);
public void SetMusicVolume(float volume);
public void StopAllSounds();
```

#### 2c. NPC behavior on NPCProxy
```csharp
// Add to NPCProxy:
public string GetBehaviorState();
public void SetNavigationTarget(Vector3Proxy target);
public void Wander();
public void ForceSeekPlayer();
```

### Phase 3 — Higher Complexity

#### 3a. Visual effects on EditorLuaGameProxy
```csharp
public void FlashScreen(int r, int g, int b, float duration);
public void ShakeScreen(float intensity, float duration);
public void SpawnParticles(string type, Vector3Proxy position, ColorProxy color, float duration);
```

#### 3b. Coroutine/async support
Register MoonSharp coroutines so Lua can do:
```lua
function Update(dt)
    -- ...
end

function SomeTimedEvent()
    wait(5.0)  -- custom function that yields
    self:ShowMessage("5 seconds passed!", 3.0)
    wait(3.0)
    self:OpenExits(false)
end
```

#### 3c. Pickup control on EditorLuaGameProxy
```csharp
public List<PickupProxy> GetPickupsInRoom(RoomProxy room);
public void RespawnAllPickupsInRoom(RoomProxy room);
```

---

## New Lua Callbacks to Add

| Callback | Args | Return | Description |
|----------|------|--------|-------------|
| `OnDoorOpened(door)` | DoorProxy | — | Door opened |
| `OnDoorClosed(door)` | DoorProxy | — | Door closed |
| `OnPlayerEnterRoom(room)` | RoomProxy | — | Player entered room |
| `OnPlayerExitRoom(room)` | RoomProxy | — | Player left room |
| `OnNPCEnterRoom(npc, room)` | NPCProxy, RoomProxy | — | NPC entered room |
| `OnTimerExpired()` | — | — | Timer reached zero |
| `OnItemPickup(itemId, slot)` | string, int | bool | Item picked up, return false to cancel |
| `OnPlayerDamaged()` | — | — | Player took damage |

---

## Implementation Notes

### Harmony Patches Needed
- Door state changes → fire Lua callbacks
- Room entry/exit detection → `PlayerManager` or `Entity` triggers
- Timer expiry → `BaseGameManager` or custom timer
- Item pickup → `Pickup` or `ItemManager`

### Existing Patterns to Follow
- All proxies use `[MoonSharpUserData]`
- Hidden Unity types marked `[MoonSharpHidden]`
- Proxy constructors take the game type, expose only safe properties
- String-based enums use `ToStringExtended<T>()` / `GetFromExtendedName<T>()`
- Game objects accessed through `Singleton<T>.Instance`

### Files to Create
```
src/Lua/Proxies/DoorProxy.cs
src/Lua/Extensions/CellProxyExtensions.cs    (methods added via Harmony or wrapper)
src/Lua/Extensions/RoomProxyExtensions.cs
src/Lua/Extensions/NPCProxyExtensions.cs
src/Lua/Extensions/EditorLuaGameProxyExtensions.cs
src/Lua/Core/KnoxumLuaCallbacks.cs           (Harmony patches for new callbacks)
src/Lua/Core/KnoxumLuaTimer.cs               (timer system)
src/Lua/Core/KnoxumLuaCoroutineHelper.cs     (wait/schedule support)
```

### Risk Areas
- `Door` internals may be sealed or use private fields → need Harmony reflection
- Room entry/exit detection depends on `Entity` trigger system
- Timer conflicts with existing `BaseChallengeGameManager` timer logic
- Coroutine support requires careful MoonSharp `Script.CoroutineAutoYielder` setup
