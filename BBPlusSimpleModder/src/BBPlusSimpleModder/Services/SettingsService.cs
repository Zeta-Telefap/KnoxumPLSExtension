using System.Text.Json;
using BBPlusSimpleModder.Core.Models;
using System.IO;

namespace BBPlusSimpleModder.Services;

/// <summary>Хранит настройки приложения в settings.json рядом с exe.</summary>
public class SettingsService
{
    public SettingsModel Model { get; private set; } = new();

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public SettingsService() => Load();

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Model = JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(FilePath), JsonOpts.Default) ?? new SettingsModel();
        }
        catch
        {
            Model = new SettingsModel();
        }
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Model, JsonOpts.Default)); }
        catch { /* не критично */ }
    }
}
