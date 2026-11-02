using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using BBPlusSimpleModder.Core.BlockEngine;

namespace BBPlusSimpleModder.Core.Models;

/// <summary>
/// Узел дерева компонентов мода (как GameObject в иерархии Unity):
/// у каждого есть свойства, дети, а у «логических» типов — граф кирпичиков.
/// </summary>
public class ModComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Новый компонент";
    public ComponentKind Kind { get; set; } = ComponentKind.Folder;

    [JsonIgnore]
    public ModComponent? Parent { get; set; }

    public ObservableCollection<ModComponent> Children { get; } = new();

    /// <summary>Редактируемые свойства (отображаются в Inspector).</summary>
    public Dictionary<string, object?> Properties { get; } = new();

    /// <summary>Граф кирпичиков (у типов с логикой).</summary>
    public Graph? Graph { get; set; }

    /// <summary>Ссылка на ассет (для Sprite/Model/Audio-узлов).</summary>
    public string? AssetId { get; set; }

    /// <summary>Есть ли у этого типа редактор логики.</summary>
    [JsonIgnore]
    public bool HasGraph => Kind is ComponentKind.Character or ComponentKind.Item
        or ComponentKind.Event or ComponentKind.Patch or ComponentKind.FloorGen
        or ComponentKind.Object or ComponentKind.UiScreen;

    [JsonIgnore]
    public string Icon => Kind switch
    {
        ComponentKind.Root => "📦",
        ComponentKind.Folder => "📁",
        ComponentKind.Floor => "🗼",
        ComponentKind.FloorGen => "⚙️",
        ComponentKind.Object => "🚪",
        ComponentKind.Character => "🧍",
        ComponentKind.Item => "🥤",
        ComponentKind.Event => "⚡",
        ComponentKind.Sprite => "🖼️",
        ComponentKind.Model => "🧊",
        ComponentKind.Audio => "🔊",
        ComponentKind.UiScreen => "🖥️",
        ComponentKind.Patch => "🩹",
        ComponentKind.Settings => "⚙️",
        _ => "▫️"
    };

    /// <summary>Создать компонент нужного типа с дефолтными свойствами и (если надо) графом.</summary>
    public static ModComponent Create(ComponentKind kind, ModComponent parent)
    {
        var c = new ModComponent
        {
            Kind = kind,
            Parent = parent,
            Name = DefaultName(kind)
        };
        switch (kind)
        {
            case ComponentKind.Character:
                c.Properties["Скорость"] = 5f;
                c.Properties["Слух (радиус шума)"] = 15;
                c.Properties["Цвет"] = "#FFFFFFFF";
                c.Properties["Имя (локализация)"] = "";
                c.Graph = new Graph { Kind = GraphKind.NpcUpdate, Title = "Логика персонажа (Update)" };
                break;
            case ComponentKind.Item:
                c.Properties["Цена (монеты)"] = 0;
                c.Properties["Имя (локализация)"] = "";
                c.Graph = new Graph { Kind = GraphKind.ItemUse, Title = "Использовать предмет (Use)" };
                break;
            case ComponentKind.Event:
                c.Properties["Длительность (с)"] = 30f;
                c.Graph = new Graph { Kind = GraphKind.EventRun, Title = "Событие (запуск)" };
                break;
            case ComponentKind.Patch:
                c.Properties["Цель (класс)"] = "Baldi";
                c.Properties["Метод"] = "TakeApple";
                c.Properties["Тип патча"] = "Prefix";
                c.Graph = new Graph { Kind = GraphKind.HarmonyPrefix, Title = "Патч (Prefix)" };
                break;
            case ComponentKind.Object:
            case ComponentKind.FloorGen:
                c.Graph = new Graph { Kind = GraphKind.RoomBuild, Title = "Логика (Build)" };
                break;
            case ComponentKind.Floor:
                c.Properties["Номер этажа"] = 1;
                c.Properties["Название"] = "Этаж 1";
                c.Properties["Лимит времени (с)"] = 180f;
                break;
            case ComponentKind.UiScreen:
                c.Properties["Сцена"] = "MainMenu";
                c.Graph = new Graph { Kind = GraphKind.OnAssetsLoaded, Title = "Построение UI" };
                break;
        }
        return c;
    }

    public static string DefaultName(ComponentKind kind) => kind switch
    {
        ComponentKind.Floor => "Новый этаж",
        ComponentKind.FloorGen => "Новый компонент генерации",
        ComponentKind.Object => "Новый объект",
        ComponentKind.Character => "Новый персонаж",
        ComponentKind.Item => "Новый предмет",
        ComponentKind.Event => "Новое событие",
        ComponentKind.Sprite => "Новый спрайт",
        ComponentKind.Model => "Новая модель",
        ComponentKind.Audio => "Новый звук",
        ComponentKind.UiScreen => "Новый экран UI",
        ComponentKind.Patch => "Новый патч",
        _ => "Новая папка"
    };

    /// <summary>Глубокая копия (через JSON round-trip) — для «Дублировать».</summary>
    public ModComponent DeepCopy()
    {
        Parent = null;
        var json = JsonSerializer.Serialize(this, JsonOpts.Default);
        var copy = JsonSerializer.Deserialize<ModComponent>(json, JsonOpts.Default)!;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name += " (копия)";
        RebindParents(copy, null);
        return copy;
    }

    private static void RebindParents(ModComponent c, ModComponent? parent)
    {
        c.Parent = parent;
        foreach (var ch in c.Children) RebindParents(ch, c);
    }
}
