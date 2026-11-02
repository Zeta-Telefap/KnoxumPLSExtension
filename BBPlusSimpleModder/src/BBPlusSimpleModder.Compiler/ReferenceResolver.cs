namespace BBPlusSimpleModder.Compiler;

/// <summary>Набор путей к DLL для компиляции мода.</summary>
public class ReferenceSet
{
    /// <summary>BALDI_Data/Managed/*.dll (Assembly-CSharp, UnityEngine…).</summary>
    public List<string> GameManaged { get; } = new();
    /// <summary>BepInEx/core/*.dll (BepInEx.dll, 0Harmony.dll…).</summary>
    public List<string> BepInExCore { get; } = new();
    /// <summary>Плагины из BepInEx/plugins (MTM101BaldAPI.dll, PixelInternalAPI.dll, Plus Level Loader…).</summary>
    public List<string> Plugins { get; } = new();

    public IEnumerable<string> All => GameManaged.Concat(BepInExCore).Concat(Plugins);

    public string? FindPlugin(string fileName) =>
        Plugins.FirstOrDefault(p => Path.GetFileName(p).Equals(fileName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Находит референсы в папке игры пользователя:
/// BALDI_Data/Managed, BepInEx/core, BepInEx/plugins (рекурсивно).
/// </summary>
public static class ReferenceResolver
{
    public static ReferenceSet Resolve(string gamePath)
    {
        var set = new ReferenceSet();
        var managed = Path.Combine(gamePath, "BALDI_Data", "Managed");
        if (Directory.Exists(managed))
            set.GameManaged.AddRange(Directory.GetFiles(managed, "*.dll"));

        var core = Path.Combine(gamePath, "BepInEx", "core");
        if (Directory.Exists(core))
            set.BepInExCore.AddRange(Directory.GetFiles(core, "*.dll"));

        var plugins = Path.Combine(gamePath, "BepInEx", "plugins");
        if (Directory.Exists(plugins))
            set.Plugins.AddRange(Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories));

        return set;
    }

    /// <summary>Папка игры валидна (есть BALDI_Data).</summary>
    public static bool IsGameFolder(string path) =>
        Directory.Exists(path) && Directory.Exists(Path.Combine(path, "BALDI_Data"));

    /// <summary>Найти exe игры рядом с BALDI_Data.</summary>
    public static string? FindGameExe(string gamePath)
    {
        if (!Directory.Exists(gamePath)) return null;
        return Directory.GetFiles(gamePath, "*.exe")
            .FirstOrDefault(f => !Path.GetFileName(f).Equals("UnityCrashHandler64.exe", StringComparison.OrdinalIgnoreCase));
    }
}
