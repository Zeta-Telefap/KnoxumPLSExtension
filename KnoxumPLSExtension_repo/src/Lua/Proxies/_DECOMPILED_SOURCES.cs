// ============================================================================
// Decompiled proxy sources from PlusLevelStudio.Lua
// These are reference copies — do NOT compile directly.
// Original code belongs to Plus Level Studio / BB+.
// ============================================================================

// --- Vector3Proxy ---
// MoonSharp-safe wrapper for UnityEngine.Vector3
// Properties: x, y, z (float, private set)
// Operators: +, -, ==, !=
// Methods: ToString(), DistanceFrom(other), ToVector() [Hidden], ToVector2YAsZ() [Hidden]

// --- ColorProxy ---
// MoonSharp-safe wrapper for UnityEngine.Color
// Properties: r, g, b (int 0-255, private set)
// Methods: ToColor() [Hidden]

// --- IntVector2Proxy ---
// MoonSharp-safe wrapper for IntVector2
// Properties: x, z (int, private set)
// Operators: +, -, ==, !=
// Methods: ToVector() [Hidden]

// --- CellProxy ---
// Wraps Cell
// Properties: position (IntVector2Proxy), FloorWorldPosition (Vector3Proxy), CenterWorldPosition (Vector3Proxy)
// Methods: GetRoom(), GetLight(), SetLight(color, strength), PressAllButtons()

// --- LightProxy ---
// Wraps Cell with light
// Properties: cell (CellProxy), color (ColorProxy), strength (int)
// Methods: SetPower(bool)

// --- RoomProxy ---
// Wraps RoomController
// Properties: name (string), category (string), powered (bool), mapColor (ColorProxy),
//             hasActivity (bool), activityCompleted (bool)
// Methods: IsHall(), GetZone(), GetCells(), GetLights(), GetEntitySafeCells(),
//          GetRandomEntitySafeCell(), LockAllDoors(), UnlockAllDoors(), LockAllDoorsTimed(time),
//          RespawnActivity(), RespawnItem(itemId)

// --- NPCProxy ---
// Wraps NPC
// Properties: id (string), position (Vector3Proxy), direction (float),
//             objectName (string), moveSpeedMultiplier (float), squished (bool)
// Methods: GetForward(), AddArrow(r,g,b), IsHidden(), Squish(time), Unsquish()

// --- BaldiProxy : NPCProxy ---
// Wraps Baldi
// Methods: AddAnger(amount), SetAnger(amount), Slap(), Praise(time), Praise(time, rewardSticker)

// --- PlayerProxy ---
// Wraps PlayerManager
// Properties: position, direction, walkSpeed, runSpeed, stamina*, points,
//             slotCount, moveSpeedMultiplier, baseWalkSpeed, baseRunSpeed,
//             baseStaminaDrop, baseStaminaMax, baseStaminaRise, squished
// Methods: GetForward(), IsHidden(), Squish(), Unsquish(),
//          MakeGuilty(rule, time), GetGuilt(),
//          SetItem(itemId, slot), AddItem(itemId), GetItem(slot), HasItem(itemId),
//          RemoveItemOfID(itemId), RemoveItem(slot), RemoveItemSlot(slot),
//          SetSlotCount(count), LockItemSlot(slot), UnlockItemSlot(slot), UseItem(itemId),
//          AddStickerToInventory(id, anim), RemoveStickerFromInventory(id),
//          GetInventoryStickers(), GetInventorySticker(slot),
//          GetActiveStickers(), GetActiveSticker(slot), GetStickerValue(id), ApplySticker(slot, id)

// --- ElevatorManagerProxy ---
// Wraps ElevatorManager
// Methods: SetIntendedElevatorState(elevator, state), GetIntendedElevatorState(elevator),
//          SetTotalOutOfOrderElevators(total), SetAllElevators(state), GetElevators()

// --- ElevatorProxy ---
// Wraps Elevator
// Properties: cell (CellProxy), state (string), powered (bool), gateIsOpen (bool)
// Methods: SetState(state)

// --- EditorLuaGameProxy ("self" in Lua) ---
// Properties: notebookAngerVal (float), notebookCount (int), totalNotebooks (int),
//             escapeSequenceActive (bool),
//             npcTimeScaleMod (float), environmentTimeScaleMod (float), playerTimeScaleMod (float)
// Methods: OpenExits(doEscape), ActivateBonusProblems(includeLast),
//          GetAllLights(), GetAllRooms(),
//          ForceLose(), ForceWin(text),
//          GiveRandomSticker(packType, total),
//          GetRandomEntitySafeCell(),
//          GetNPCTimeScale(), GetEnvironmentTimeScale(), GetPlayerTimeScale(),
//          SpawnNPCs(), StartEventTimers(), StartEvent(eventId, length, doJingle),
//          GetNPC(npcId), GetNPCs(), GetBaldi(),
//          MakeNoise(position, noiseValue),
//          SpawnNPC(type, position), SpawnItemPickup(position, itemId),
//          CellFromPosition(Vector3Proxy), CellFromPosition(IntVector2Proxy),
//          PlaySoundObject(sound)

// --- CustomChallengeManager ---
// Runtime manager, extends BaseChallengeGameManager
// Fields: luaScript, script, myProxy, timeScaleModifier
// Hooks into: Initialize, Update, ExitedSpawn, CollectNotebook, BaldiSlapped,
//             AngerBaldi, NoiseMade, ActivityCompleted, LoadNextLevel, GiveRandomSticker
// Lua globals: self, player, Vector3(), IntVector2(), Color(), RandomDecimalNumber()
// Lua callbacks: SetupPlayerProperties, Initialize, ExitedSpawn, Update, AllNotebooks,
//                NotebookCollected, OnItemUse, NoiseMade, BaldiSlapped, AngerBaldi,
//                OnActivityCompletion, OnActivityProgress, OnLevelCompleted,
//                AllNPCsSpawned, OnGiveRandomSticker

// --- CustomChallengeGameModeSettings ---
// Save/load: luaScript (string), fileName (string)
// Binary format version 2 with Deflate compression

// --- LuaHelpers ---
// Static utility: GetIDFromItemObject, GetIDFromSticker, GetIDFromNPC
