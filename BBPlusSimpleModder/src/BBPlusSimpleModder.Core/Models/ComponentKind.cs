namespace BBPlusSimpleModder.Core.Models;

/// <summary>Тип узла в дереве компонентов мода.</summary>
public enum ComponentKind
{
    Root,
    Folder,
    Floor,        // этаж
    FloorGen,     // компонент генерации этажа (комната/коридор/структура)
    Object,       // объект: шкафчик, стол, дверь…
    Character,    // персонаж (NPC)
    Item,         // предмет
    Event,        // событие (RandomEvent)
    Sprite,       // спрайт/текстура
    Model,        // 3D-модель (OBJ)
    Audio,        // звук
    UiScreen,     // экран UI (меню/HUD/пауза…)
    Patch,        // Harmony-патч
    Settings
}
