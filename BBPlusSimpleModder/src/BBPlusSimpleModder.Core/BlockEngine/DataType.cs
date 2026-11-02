namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>Типы data-портов кирпичиков (цвет провода зависит от типа).</summary>
public enum DataType
{
    Any,
    Bool,
    Int,
    Float,
    String,
    Vector3,
    Color,
    GameObject,
    Component,          // любой объект Unity/MonoBehaviour
    NPC,
    Entity,
    PlayerManager,
    Item,
    RoomController,
    Cell,
    EnvironmentController,
    SoundObject,
    Texture2D,
    AudioClip
}
