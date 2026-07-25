# Lua Proxy Sources — Decompiled from PlusLevelStudio.Lua

These are decompiled source files from the PLS (Plus Level Studio) Lua challenge system.
They define the MoonSharp proxy layer that exposes BB+ game objects to Lua scripts.

## Architecture

```
CustomChallengeManager (runtime)
  └─ Script (MoonSharp)
       ├─ Globals: self, player, Vector3(), IntVector2(), Color(), ...
       └─ Callbacks: Initialize, Update, SetupPlayerProperties, ...

EditorLuaGameProxy ("self" in Lua)
  ├─ NPC management: GetNPC, SpawnNPC, GetBaldi, GetNPCs
  ├─ Room/Light queries: GetAllRooms, GetAllLights
  ├─ Elevator control: elevatorManager
  ├─ Items: SpawnItemPickup
  ├─ Events: StartEvent, StartEventTimers
  ├─ Game flow: OpenExits, ForceWin, ForceLose
  └─ Time scales: npcTimeScaleMod, environmentTimeScaleMod, playerTimeScaleMod

PlayerProxy ("player" in Lua)
  ├─ Stats: walkSpeed, runSpeed, stamina*, points
  ├─ Inventory: SetItem, AddItem, GetItem, HasItem, RemoveItem
  ├─ Stickers: AddStickerToInventory, ApplySticker, GetActiveStickers
  ├─ Movement: position, direction, moveSpeedMultiplier
  └─ Misc: MakeGuilty, Squish, IsHidden

NPCProxy / BaldiProxy
  ├─ position, direction, moveSpeedMultiplier
  ├─ Squish, IsHidden, AddArrow
  └─ BaldiProxy: AddAnger, SetAnger, Slap, Praise

RoomProxy
  ├─ name, category, powered, mapColor
  ├─ GetCells, GetLights, GetEntitySafeCells
  ├─ LockAllDoors, UnlockAllDoors, LockAllDoorsTimed
  ├─ hasActivity, activityCompleted, RespawnActivity, RespawnItem
  └─ GetZone

CellProxy → LightProxy, RoomProxy
ElevatorManagerProxy → ElevatorProxy
```

## Existing Lua Callbacks (script.Globals)

| Callback | Args | Return | Description |
|----------|------|--------|-------------|
| `SetupPlayerProperties()` | — | table | Set walkSpeed, runSpeed, staminaDrop, staminaMax, staminaRise |
| `Initialize()` | — | — | Called after setup |
| `ExitedSpawn()` | — | — | Player left spawn room |
| `Update(dt)` | deltaTime | — | Every frame |
| `AllNotebooks()` | — | — | All notebooks collected |
| `NotebookCollected(pos)` | Vector3 | — | Single notebook picked up |
| `OnItemUse(itemId, slot)` | string, int | bool | Return false to cancel use |
| `NoiseMade(pos, value)` | Vector3, int | — | Noise at position |
| `BaldiSlapped(npcProxy)` | NPCProxy | — | Baldi was slapped |
| `AngerBaldi(val)` | float | float | Override anger value |
| `OnActivityCompletion(room, correct)` | RoomProxy, bool | — | Activity finished |
| `OnActivityProgress(room)` | RoomProxy | — | Activity progress |
| `OnLevelCompleted()` | — | string | Level done, return win text |
| `AllNPCsSpawned()` | — | — | Initial NPC spawn complete |
| `OnGiveRandomSticker(packType, total)` | string, int | bool | Return true to give sticker |

## Files

- `Proxies/` — All proxy classes (Vector3Proxy, CellProxy, NPCProxy, etc.)
- `Core/` — Manager, settings, game mode, helpers
