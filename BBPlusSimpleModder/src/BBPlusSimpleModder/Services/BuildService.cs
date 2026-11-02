using System.Diagnostics;
using System.Text.Json;
using BBPlusSimpleModder.Compiler;
using BBPlusSimpleModder.Core.Codegen;
using BBPlusSimpleModder.Core.Models;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BBPlusSimpleModder.Services;

/// <summary>Результат сборки мода.</summary>
public class BuildResult
{
    public bool Success { get; set; }
    public string PluginDllPath { get; set; } = "";
    public string ModFolderInGame { get; set; } = "";
    public List<string> Errors { get; } = new();
    public string? AssemblyName { get; set; }
}

/// <summary>
/// «Собрать»: графы → C# → Roslyn → {GUID}.dll + .pdb + .xml;
/// ресурсы → BALDI_Data/StreamingAssets/Modded/{GUID}/; плагин → BepInEx/plugins/
/// (плоско или папкой — по настройке).
/// </summary>
public class BuildService
{
    private readonly Logger _logger;

    public BuildService(Logger logger) => _logger = logger;

    /// <summary>Последний сгенерированный C# (для вкладки «Сгенерированный код»).</summary>
    public string LastGeneratedCode { get; private set; } = "";

    public async Task<BuildResult> BuildAsync(
        ModProject project,
        string projectPath,
        List<BuildStep> steps,
        Action<string> log)
    {
        var result = new BuildResult();
        var gen = new GraphToCSharp();
        if (steps.Count < 6) throw new ArgumentException("Ожидалось 6 шагов сборки");

        try
        {
            // 1. референсы
            steps[0].Running();
            var refs = ReferenceResolver.Resolve(project.GamePath);
            if (refs.GameManaged.Count == 0)
                throw new InvalidOperationException($"Не найдены DLL игры в {project.GamePath}/BALDI_Data/Managed — проверьте папку игры");
            log($"Референсы: игра {refs.GameManaged.Count}, BepInEx {refs.BepInExCore.Count}, плагины {refs.Plugins.Count}");
            steps[0].Done();

            // 2. генерация C#
            steps[1].Running();
            var sources = new Dictionary<string, string>();
            var codeAll = new System.Text.StringBuilder();
            var usedClassNames = new HashSet<string>();
            // сначала классы компонентов (имена классов уникальны, файлы — тоже)
            foreach (var c in project.AllComponents().Where(c => c.Graph != null && c.Kind != ComponentKind.Root))
            {
                var fileBase = GraphToCSharp.Slug(c.Name);
                var file = fileBase + ".cs";
                int k = 0;
                while (sources.ContainsKey(file)) { k++; file = fileBase + "_" + c.Id[..4] + (k > 1 ? k.ToString() : "") + ".cs"; }
                sources[file] = gen.GenerateClassFile(c, usedClassNames);
                codeAll.AppendLine(sources[file]);
            }
            // затем плагин (видит те же имена классов для регистрации)
            sources["Plugin.cs"] = gen.GeneratePluginFile(project, usedClassNames);
            codeAll.AppendLine(sources["Plugin.cs"]);
            LastGeneratedCode = codeAll.ToString();
            foreach (var w in gen.Warnings) { log("WARN: " + w); }
            log($"Сгенерировано файлов: {sources.Count}");
            steps[1].Done();

            // 3. компиляция
            steps[2].Running();
            var asmName = GraphToCSharp.Slug(project.Name) + ".dll";
            result.AssemblyName = asmName;
            var compiled = RoslynCompiler.Compile(sources, refs.All, Path.GetFileNameWithoutExtension(asmName));
            foreach (var e in compiled.Errors) log("ERR: " + e);
            foreach (var w in compiled.Warnings.Take(20)) log("warn: " + w);
            if (!compiled.Success)
            {
                result.Errors.AddRange(compiled.Errors);
                steps[2].Failed();
                result.Success = false;
                return result;
            }
            steps[2].Done();
            steps[3].Done(); // pdb+xml выпускаются вместе с dll
            log($"Скомпилировано: {compiled.Dll.Length / 1024} КБ dll, {compiled.Pdb.Length / 1024} КБ pdb");

            // 4. ресурсы → StreamingAssets/Modded/{GUID}/
            steps[4].Running();
            var modDir = Path.Combine(project.GamePath, "BALDI_Data", "StreamingAssets", "Modded", project.Guid);
            result.ModFolderInGame = modDir;
            Directory.CreateDirectory(modDir);
            foreach (var asset in project.Assets)
            {
                var src = Path.Combine(project.ProjectDir, asset.RelativePath);
                if (!File.Exists(src)) continue;
                var sub = AssetKindUtil.SubFolder(asset.Kind);
                var destDir = Path.Combine(modDir, sub);
                Directory.CreateDirectory(destDir);
                File.Copy(src, Path.Combine(destDir, asset.Name), overwrite: true);
            }
            // манифест мода
            var manifest = new
            {
                guid = project.Guid,
                name = project.Name,
                version = project.Version,
                author = project.Author,
                description = project.Description,
                assets = project.Assets.Select(a => new { a.Name, a.Kind, path = $"{AssetKindUtil.SubFolder(a.Kind)}/{a.Name}" })
            };
            File.WriteAllText(Path.Combine(modDir, "mod.json"),
                JsonSerializer.Serialize(manifest, JsonOpts.Default));
            log($"Ресурсы скопированы: {modDir}");
            steps[4].Done();

            // 5. плагин → BepInEx/plugins (flat или папкой)
            steps[5].Running();
            var pluginsDir = Path.Combine(project.GamePath, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsDir);
            string dllName = project.Guid + ".dll";
            if (project.Settings.PluginInFolder)
            {
                var folder = Path.Combine(pluginsDir, project.Guid);
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, dllName), compiled.Dll);
                File.WriteAllBytes(Path.Combine(folder, project.Guid + ".pdb"), compiled.Pdb);
                File.WriteAllText(Path.Combine(folder, project.Guid + ".xml"), compiled.XmlDoc);
                result.PluginDllPath = Path.Combine(folder, dllName);
            }
            else
            {
                File.WriteAllBytes(Path.Combine(pluginsDir, dllName), compiled.Dll);
                File.WriteAllBytes(Path.Combine(pluginsDir, project.Guid + ".pdb"), compiled.Pdb);
                File.WriteAllText(Path.Combine(pluginsDir, project.Guid + ".xml"), compiled.XmlDoc);
                result.PluginDllPath = Path.Combine(pluginsDir, dllName);
            }
            log($"Плагин записан: {result.PluginDllPath}");
            steps[5].Done();

            result.Success = true;
            _logger.Info($"Мод {project.Name} собран успешно");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            log("ОШИБКА: " + ex.Message);
            _logger.Error(ex.ToString());
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].StatusIcon == "▶") steps[i].Failed();
        }
        return await Task.FromResult(result);
    }

    public void OpenModFolder(ModProject project)
    {
        var dir = Path.Combine(project.GamePath, "BepInEx", "plugins", project.Guid);
        if (!Directory.Exists(dir)) dir = Path.Combine(project.GamePath, "BALDI_Data", "StreamingAssets", "Modded", project.Guid);
        if (Directory.Exists(dir))
            Process.Start("explorer.exe", $"\"{dir}\"");
    }
}
