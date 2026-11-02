namespace BBPlusSimpleModder.Core.Models;

/// <summary>Найденная сущность чужого мода (для ModBrowserWindow: DLL → список плагинов/патчей).</summary>
public class ImportItem
{
    public string Kind { get; set; } = "";        // Plugin / HarmonyPatch / NPC / Item / Raw
    public string Description { get; set; } = "";
    public bool Selected { get; set; } = true;

    /// <summary>Внутренние данные для импорта (имя типа, тело метода и т.п.).</summary>
    public string Payload { get; set; } = "";
}
