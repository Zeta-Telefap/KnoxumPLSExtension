using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Mono.Cecil;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Compiler;

/// <summary>
/// Импорт чужого мода (DLL): Mono.Cecil находит BepInEx-плагины и [HarmonyPatch],
/// ICSharpCode.Decompiler декомпилирует методы → список ImportItem для ModBrowserWindow.
/// Распознанные паттерны превращаются в кирпичики, остальное — Raw C# блоки.
/// </summary>
public static class ModDecompiler
{
    public static List<ImportItem> ScanDll(string dllPath)
    {
        var items = new List<ImportItem>();
        using var asm = AssemblyDefinition.ReadAssembly(dllPath);

        foreach (var module in asm.Modules)
        foreach (var type in module.Types)
        {
            // BepInEx-плагин
            var pluginAttr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "BepInPluginAttribute");
            if (pluginAttr != null)
            {
                var guid = pluginAttr.ConstructorArguments.ElementAtOrDefault(0).Value?.ToString() ?? type.FullName;
                items.Add(new ImportItem { Kind = "Plugin", Description = $"Плагин {type.FullName} (GUID {guid})", Payload = type.FullName });
            }
            // Harmony-патчи
            if (type.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPatchAttribute"))
            {
                foreach (var m in type.Methods.Where(m => m.IsStatic && (m.Name == "Prefix" || m.Name == "Postfix")))
                {
                    items.Add(new ImportItem
                    {
                        Kind = "HarmonyPatch",
                        Description = $"{type.FullName}.{m.Name}",
                        Payload = type.FullName + "|" + m.Name
                    });
                }
            }
            // NPC/Item-наследники
            if (type.BaseType?.Name is "NPC" or "Item" or "RandomEvent")
            {
                items.Add(new ImportItem
                {
                    Kind = type.BaseType.Name,
                    Description = $"{type.Name} : {type.BaseType?.Name}",
                    Payload = type.FullName
                });
            }
        }
        return items;
    }

    /// <summary>Декомпилировать метод типа в C# (для Raw-блока или pattern-matching в кирпичики).</summary>
    public static string DecompileMethod(string dllPath, string typeFullName, string methodName)
    {
        var settings = new DecompilerSettings(LanguageVersion.Latest);
        var decompiler = new CSharpDecompiler(dllPath, settings);
        var name = new FullTypeName(typeFullName);
        // декомпилируем весь тип; метод methodName будет внутри (для Raw C# блока)
        return decompiler.DecompileTypeAsString(name) ?? $"// тип {typeFullName} не найден";
    }
}
