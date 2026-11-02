namespace BBPlusSimpleModder.Core.Models;

/// <summary>Настройки проекта/приложения (settings.json рядом с exe + настройки проекта).</summary>
public class SettingsModel
{
    /// <summary>Папка игры BB+.</summary>
    public string GamePath { get; set; } = "";

    /// <summary>Класть ли плагин папкой BepInEx/plugins/{GUID}/ вместо flat {GUID}.dll.</summary>
    public bool PluginInFolder { get; set; } = false;

    public bool AutoInstallBepInEx { get; set; } = true;
    public bool AutoInstallDevApi { get; set; } = true;
    public bool AutoInstallPls { get; set; } = false;

    /// <summary>Последняя выбранная папка игры.</summary>
    public string LastGamePath { get; set; } = "";

    /// <summary>Недавние проекты.</summary>
    public List<RecentProject> RecentProjects { get; set; } = new();

    /// <summary>URL релизов для автоустановки (downloads.json рядом с exe; дефолты — здесь).</summary>
    public Dictionary<string, string> Downloads { get; set; } = new()
    {
        ["bepinex"] = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x64_5.4.23.6.zip",
        ["devapi"] = "https://github.com/MTM101/BaldiDevAPI/releases/latest",   // подменяется пользователем при необходимости
        ["levelloader"] = "",
        ["levelstudio"] = ""
    };

    public void AddRecent(string path, string name, string guid)
    {
        RecentProjects.RemoveAll(r => r.Path == path);
        RecentProjects.Insert(0, new RecentProject { Path = path, Name = name, Guid = guid });
        if (RecentProjects.Count > 10) RecentProjects.RemoveAt(RecentProjects.Count - 1);
    }
}

public class RecentProject
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Guid { get; set; } = "";
    public string Display => $"{Name}  ({Guid})\n{Path}";
}
