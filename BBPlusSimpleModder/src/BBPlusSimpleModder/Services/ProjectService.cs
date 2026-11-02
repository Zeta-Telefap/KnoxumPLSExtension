using BBPlusSimpleModder.Core.Models;
using System.IO;

namespace BBPlusSimpleModder.Services;

/// <summary>Создание/загрузка/сохранение проекта мода (*.bbmod.json + папки).</summary>
public class ProjectService
{
    private readonly SettingsService _settings;
    private readonly Logger _logger;

    public ProjectService(SettingsService settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Создать новый проект мода. Возвращает путь к project.bbmod.json.
    /// </summary>
    public string CreateNew(string dir, string name, string guid, string version, string author, string description, string gamePath)
    {
        Directory.CreateDirectory(dir);
        foreach (var sub in new[] { "assets/sprites", "assets/textures", "assets/models", "assets/audio", "assets/data", "assets/other", "graphs", "levels", "out" })
            Directory.CreateDirectory(Path.Combine(dir, sub));

        var project = new ModProject
        {
            Name = name,
            Guid = guid,
            Version = version,
            Author = author,
            Description = description,
            GamePath = gamePath,
            ProjectDir = dir
        };
        project.Root.Name = name;
        project.Save(dir);

        _settings.Model.AddRecent(Path.Combine(dir, ModProject.ProjectFileName), name, guid);
        _settings.Model.LastGamePath = gamePath;
        _settings.Save();
        _logger.Info($"Создан проект {name} ({guid}) в {dir}");
        return Path.Combine(dir, ModProject.ProjectFileName);
    }

    public ModProject Load(string projectPath)
    {
        var p = ModProject.Load(projectPath);
        _settings.Model.AddRecent(projectPath, p.Name, p.Guid);
        _settings.Save();
        return p;
    }

    public void Save(string projectPath, ModProject project)
    {
        var dir = Path.GetDirectoryName(projectPath) ?? project.ProjectDir;
        project.ProjectDir = dir;
        project.Save(dir);
        _logger.Info("Проект сохранён");
    }
}
