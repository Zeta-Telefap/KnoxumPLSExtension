using BBPlusSimpleModder.Core.Models;
using System.IO;
using System.Collections.Generic;

namespace BBPlusSimpleModder.Services;

/// <summary>
/// Импорт файлов-ассетов в проект: копирует в assets/{sprites,models,audio,…}/,
/// создаёт AssetEntry + узел в дереве компонентов.
/// При сборке ассеты уезжают в BALDI_Data/StreamingAssets/Modded/{GUID}/.
/// </summary>
public class AssetImportService
{
    private readonly Logger _logger;

    public AssetImportService(Logger logger) => _logger = logger;

    public List<AssetEntry> ImportFiles(IEnumerable<string> files, ModComponent parent, ModProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var result = new List<AssetEntry>();
        foreach (var file in files)
        {
            try
            {
                var kind = AssetKindUtil.FromExtension(file);
                var sub = AssetKindUtil.SubFolder(kind);
                var assetsRoot = Path.Combine(project.ProjectDir, "assets", sub);
                Directory.CreateDirectory(assetsRoot);

                var fileName = Path.GetFileName(file);
                var dest = Path.Combine(assetsRoot, fileName);
                if (File.Exists(dest)) dest = Path.Combine(assetsRoot, $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
                File.Copy(file, dest, overwrite: true);

                var entry = new AssetEntry
                {
                    Name = Path.GetFileName(file),
                    Kind = kind,
                    RelativePath = Path.GetRelativePath(project.ProjectDir, dest),
                    Size = new FileInfo(dest).Length
                };
                project.Assets.Add(entry);

                // узел в дереве
                var cKind = kind switch
                {
                    AssetKind.Sprite or AssetKind.Texture => ComponentKind.Sprite,
                    AssetKind.Model => ComponentKind.Model,
                    AssetKind.Audio => ComponentKind.Audio,
                    _ => ComponentKind.Folder
                };
                if (cKind != ComponentKind.Folder)
                {
                    var node = ModComponent.Create(cKind, parent);
                    node.Name = entry.Name;
                    node.AssetId = entry.Id;
                    parent.Children.Add(node);
                }

                result.Add(entry);
                _logger.Info($"Ассет импортирован: {entry.Name} → {entry.RelativePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Не удалось импортировать {file}: {ex.Message}");
            }
        }
        return result;
    }

    public void Delete(AssetEntry entry, ModProject project)
    {
        var abs = Path.Combine(project.ProjectDir, entry.RelativePath);
        try { if (File.Exists(abs)) File.Delete(abs); } catch { }
        project.Assets.Remove(entry);
    }

    /// <summary>Абсолютный путь к файлу ассета внутри папки проекта.</summary>
    public static string AbsolutePath(ModProject p, AssetEntry a) => Path.Combine(p.ProjectDir, a.RelativePath);
}
