using BBPlusSimpleModder.Compiler;
using BBPlusSimpleModder.Core.BlockEngine;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Services;

/// <summary>
/// Импорт чужих модов: DLL → список сущностей (Cecil) → декомпиляция (ICSharpCode.Decompiler)
/// → графы кирпичиков (узнаваемые паттерны) или Raw C# блоки. .lvl → в LevelEditorView.
/// </summary>
public class ModImportService
{
    private readonly Logger _logger;

    public ModImportService(Logger logger) => _logger = logger;

    public List<ImportItem> ScanDll(string dllPath)
    {
        var items = ModDecompiler.ScanDll(dllPath);
        _logger.Info($"Просканировано {Path.GetFileName(dllPath)}: найдено {items.Count} сущностей");
        return items;
    }

    public async Task ImportSelectedAsync(IEnumerable<ImportItem> items, ModProject project, string dllPath)
    {
        await Task.Yield();
        foreach (var item in items)
        {
            try
            {
                if (item.Kind == "HarmonyPatch")
                {
                    var parts = item.Payload.Split('|');
                    var code = ModDecompiler.DecompileMethod(dllPath, parts[0], parts[1]);
                    var patch = ModComponent.Create(ComponentKind.Patch, project.Root);
                    patch.Name = $"{parts[0]}.{parts[1]}";
                    patch.Graph ??= new Graph { Kind = GraphKind.HarmonyPrefix };
                    // декомпилированный код — Raw C# блоком в графе
                    var start = new BlockNode { BlockId = "flow.start", X = 40, Y = 40 };
                    var raw = new BlockNode { BlockId = "stmt.raw", X = 260, Y = 40 };
                    raw.Values["code"] = code;
                    patch.Graph.Nodes.Add(start);
                    patch.Graph.Nodes.Add(raw);
                    patch.Graph.Wires.Add(new Wire { From = new PortRef(start.Id, "out"), To = new PortRef(raw.Id, "in") });
                    project.Root.Children.Add(patch);
                }
                else
                {
                    var folder = ModComponent.Create(ComponentKind.Folder, project.Root);
                    folder.Name = $"Импорт: {item.Description}";
                    project.Root.Children.Add(folder);
                }
                _logger.Info($"Импортировано: {item.Description}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Ошибка импорта {item.Description}: {ex.Message}");
            }
        }
    }

    public BaldiLevelModel ImportLvl(string path) => Core.LvlFormat.BaldiLevelModel.Load(path);

    public void ImportLvlIntoProject(string path, ModProject project)
    {
        var lvl = ImportLvl(path);
        var floor = ModComponent.Create(ComponentKind.Floor, project.Root);
        floor.Name = lvl.LevelTitle;
        floor.Properties["Файл уровня"] = Path.GetFileName(path);
        project.Root.Children.Add(floor);
        _logger.Info($"Уровень {lvl.LevelTitle} привязан к проекту");
    }
}
