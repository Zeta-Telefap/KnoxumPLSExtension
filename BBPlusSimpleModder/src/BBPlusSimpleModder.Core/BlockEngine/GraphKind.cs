namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>
/// Тип графа = «хук», куда вешается логика. Определяет сигнатуру
/// генерируемого метода (см. Codegen.GraphToCSharp).
/// </summary>
public enum GraphKind
{
    Function,           // обычная подпрограмма void/int…
    NpcInitialize,
    NpcUpdate,          // NPC.VirtualUpdate
    NpcHear,            // NPC.Hear(GameObject, Vector3, int)
    NpcPlayerSighted,
    NpcPlayerLost,
    NpcTriggerEnter,
    NpcTriggerStay,
    NpcTriggerExit,
    NpcDoorHit,
    ItemUse,            // Item.Use(PlayerManager) -> bool
    ItemPostUse,        // Item.PostUse(PlayerManager)
    EventRun,           // RandomEvent…
    RoomBuild,          // RoomFunction.Build(LevelBuilder, Random)
    HarmonyPrefix,      // [HarmonyPatch] prefix
    HarmonyPostfix,     // [HarmonyPatch] postfix
    OnAssetsLoaded      // LoadingEvents.RegisterOnAssetsLoaded
}
