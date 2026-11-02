using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBPlusSimpleModder.Core.Models;

/// <summary>
/// Проект мода (*.bbmod.json). Хранится в папке проекта вместе с
/// assets/, graphs/, levels/, out/.
/// </summary>
public class ModProject
{
    public string Name { get; set; } = "Новый мод";
    public string Guid { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string GamePath { get; set; } = "";

    public ModComponent Root { get; set; } = new() { Kind = ComponentKind.Root, Name = "Мод" };

    public ObservableCollection<AssetEntry> Assets { get; } = new();

    public SettingsModel Settings { get; set; } = new();

    // ---------- сериализация ----------

    public const string ProjectFileName = "project.bbmod.json";

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts.Default);

    public static ModProject FromJson(string json)
    {
        var p = JsonSerializer.Deserialize<ModProject>(json, JsonOpts.Default)!;
        Rebind(p.Root, null);
        return p;
    }

    private static void Rebind(ModComponent c, ModComponent? parent)
    {
        c.Parent = parent;
        foreach (var ch in c.Children) Rebind(ch, c);
    }

    public void Save(string projectPath)
    {
        var file = Path.Combine(projectPath, ProjectFileName);
        File.WriteAllText(file, ToJson());
    }

    public static ModProject Load(string projectPath)
    {
        var file = Path.Combine(projectPath, ProjectFileName);
        var p = FromJson(File.ReadAllText(file));
        p.ProjectDir = projectPath;
        return p;
    }

    [JsonIgnore]
    public string ProjectDir { get; set; } = "";

    /// <summary>Все компоненты дерева плоским списком (обход в глубину).</summary>
    public IEnumerable<ModComponent> AllComponents()
    {
        var stack = new Stack<ModComponent>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var c = stack.Pop();
            yield return c;
            foreach (var ch in c.Children.Reverse()) stack.Push(ch);
        }
    }
}
