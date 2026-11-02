using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using BBPlusSimpleModder.Compiler;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Services;

/// <summary>
/// Работа с папкой игры: детект BepInEx/API/PLS, автоустановка,
/// запуск игры, чтение хвоста BepInEx/LogOutput.log.
/// </summary>
public class GameEnvironmentService
{
    private readonly SettingsModel _settings;
    private readonly Logger _logger;
    private readonly HttpClient _http = new();

    public GameEnvironmentService(SettingsModel settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    // ---------- детект ----------

    public async Task<List<EnvStatusItem>> DetectAsync(string gamePath)
    {
        await Task.Yield();
        var items = new List<EnvStatusItem>();

        bool gameOk = ReferenceResolver.IsGameFolder(gamePath);
        items.Add(new EnvStatusItem
        {
            Kind = EnvKind.Game,
            Title = "Baldi's Basics Plus (BALDI_Data)",
            Installed = gameOk,
            Status = gameOk ? "✔ найдена" : "✘ не найдена"
        });

        bool bep = File.Exists(Path.Combine(gamePath, "winhttp.dll")) &&
                   Directory.Exists(Path.Combine(gamePath, "BepInEx", "core"));
        items.Add(new EnvStatusItem
        {
            Kind = EnvKind.BepInEx,
            Title = "BepInEx 5.x (winhttp.dll + BepInEx/core)",
            Installed = bep,
            CanInstall = !bep && _settings.AutoInstallBepInEx && Url("bepinex").Length > 0,
            Status = bep ? "✔ установлен" : "✘ нет"
        });

        bool api = Directory.Exists(Path.Combine(gamePath, "BepInEx", "plugins")) &&
                   Directory.GetFiles(Path.Combine(gamePath, "BepInEx", "plugins"), "*.dll", SearchOption.AllDirectories)
                       .Any(f => Path.GetFileName(f).Equals("MTM101BaldAPI.dll", StringComparison.OrdinalIgnoreCase));
        items.Add(new EnvStatusItem
        {
            Kind = EnvKind.DevApi,
            Title = "MTM101BaldAPI (BepInEx/plugins/MTM101BaldAPI.dll)",
            Installed = api,
            CanInstall = !api && _settings.AutoInstallDevApi && Url("devapi").Length > 0,
            Status = api ? "✔ установлен" : "✘ нет"
        });

        bool loader = PluginPresent(gamePath, "*levelstudioloader*", "*LevelLoader*");
        items.Add(new EnvStatusItem
        {
            Kind = EnvKind.LevelLoader,
            Title = "Plus Level Loader",
            Installed = loader,
            CanInstall = !loader && _settings.AutoInstallPls && Url("levelloader").Length > 0,
            Status = loader ? "✔ установлен" : "— опционально"
        });

        bool studio = PluginPresent(gamePath, "*levelstudio*", "*PlusLevelStudio*");
        items.Add(new EnvStatusItem
        {
            Kind = EnvKind.LevelStudio,
            Title = "Plus Level Studio (редактор уровней)",
            Installed = studio,
            CanInstall = !studio && _settings.AutoInstallPls && Url("levelstudio").Length > 0,
            Status = studio ? "✔ установлен" : "— опционально"
        });

        return items;
    }

    private static bool PluginPresent(string gamePath, params string[] patterns)
    {
        var plugins = Path.Combine(gamePath, "BepInEx", "plugins");
        if (!Directory.Exists(plugins)) return false;
        var files = Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFileName)!;
        return patterns.Any(p => files.Any(f => f!.IndexOf(p.Trim('*'), StringComparison.OrdinalIgnoreCase) >= 0));
    }

    /// <summary>BepInEx + DevAPI стоят → можно собирать моды.</summary>
    public bool IsReady(string gamePath)
    {
        if (!ReferenceResolver.IsGameFolder(gamePath)) return false;
        bool bep = File.Exists(Path.Combine(gamePath, "winhttp.dll")) &&
                   Directory.Exists(Path.Combine(gamePath, "BepInEx", "core"));
        if (!bep) return false;
        var plugins = Path.Combine(gamePath, "BepInEx", "plugins");
        return Directory.Exists(plugins) &&
               Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories)
                   .Any(f => Path.GetFileName(f).Equals("MTM101BaldAPI.dll", StringComparison.OrdinalIgnoreCase));
    }

    private string Url(string key) =>
        _settings.Downloads.TryGetValue(key, out var u) ? u ?? "" : "";

    // ---------- установка ----------

    public async Task<bool> InstallAsync(string gamePath, EnvKind kind)
    {
        var key = kind switch
        {
            EnvKind.BepInEx => "bepinex",
            EnvKind.DevApi => "devapi",
            EnvKind.LevelLoader => "levelloader",
            EnvKind.LevelStudio => "levelstudio",
            _ => ""
        };
        var url = Url(key);
        if (url.Length == 0)
        {
            _logger.Warn($"Нет URL для {kind} — задайте downloads[{key}] в settings.json");
            return false;
        }
        try
        {
            _logger.Info($"Скачиваю {kind}: {url}");
            var bytes = await _http.GetByteArrayAsync(url);
            var tmp = Path.Combine(Path.GetTempPath(), $"bbmod_{kind}_{Guid.NewGuid():N}.zip");
            await File.WriteAllBytesAsync(tmp, bytes);

            if (kind == EnvKind.BepInEx)
            {
                // BepInEx win x64 zip распаковываем в корень игры
                ZipFile.ExtractToDirectory(tmp, gamePath, overwriteFiles: true);
            }
            else
            {
                // API/PLS/Loader: zip с dll → в BepInEx/plugins
                var plugins = Path.Combine(gamePath, "BepInEx", "plugins");
                Directory.CreateDirectory(plugins);
                using var zip = ZipFile.OpenRead(tmp);
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var dest = Path.Combine(plugins, Path.GetFileName(e.FullName));
                        e.ExtractToFile(dest, overwrite: true);
                    }
                }
            }
            File.Delete(tmp);
            _logger.Info($"{kind} установлен");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Ошибка установки {kind}: {ex.Message}");
            return false;
        }
    }

    // ---------- запуск и лог ----------

    public void LaunchGame(string gamePath)
    {
        var exe = ReferenceResolver.FindGameExe(gamePath);
        if (exe == null)
        {
            _logger.Error("Не найден exe игры рядом с BALDI_Data");
            return;
        }
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gamePath });
    }

    /// <summary>Последние N строк BepInEx/LogOutput.log.</summary>
    public string ReadLogTail(string gamePath, int maxLines = 100)
    {
        var log = Path.Combine(gamePath, "BepInEx", "LogOutput.log");
        if (!File.Exists(log)) return "";
        try
        {
            var lines = File.ReadAllLines(log);
            return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - maxLines)));
        }
        catch { return ""; }
    }
}
