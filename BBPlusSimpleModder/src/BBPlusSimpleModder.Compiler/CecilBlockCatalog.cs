using Mono.Cecil;
using BBPlusSimpleModder.Core.BlockEngine;

namespace BBPlusSimpleModder.Compiler;

/// <summary>
/// Авто-каталог блоков из метаданных сборок игры (Mono.Cecil, read-only):
/// каждый публичный метод интересных типов → блок «Вызвать метод».
/// </summary>
public static class CecilBlockCatalog
{
    /// <summary>Типы игры, из которых генерируем блоки (NPC, Item, менеджеры…).</summary>
    private static readonly HashSet<string> Interesting = new(StringComparer.Ordinal)
    {
        "NPC", "Entity", "PlayerManager", "EnvironmentController", "RoomController", "Cell",
        "Item", "ItemManager", "AudioManager", "PropagatedAudioManager", "MusicManager",
        "HudManager", "SubtitleManager", "StandardDoor", "Window", "Elevator", "ElevatorManager",
        "BaseGameManager", "CoreGameManager", "MainGameManager", "RandomEvent", "RoomFunction",
        "Baldi", "Principal", "Playtime", "Bully", "Beans", "DrReflex", "GottaSweep", "Cumulo", "ChalkFace"
    };

    /// <summary>Построить авто-блоки из списка DLL. Каждый блок вызывает метод через call-блок.</summary>
    public static List<BlockDefinition> BuildAutoBlocks(IEnumerable<string> assemblyPaths)
    {
        var blocks = new List<BlockDefinition>();
        foreach (var path in assemblyPaths)
        {
            AssemblyDefinition? asm;
            try { asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadWrite = false }); }
            catch { continue; }
            using (asm)
            {
                foreach (var module in asm.Modules)
                foreach (var type in module.Types)
                {
                    if (!Interesting.Contains(type.Name)) continue;
                    foreach (var m in type.Methods)
                    {
                        if (!m.IsPublic || m.IsGetter || m.IsSetter || m.IsConstructor) continue;
                        var id = $"auto.{type.Name}.{m.Name}";
                        var args = string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name));
                        var title = $"{type.Name}.{m.Name}({args})";
                        var captured = m;
                        blocks.Add(new BlockDefinition
                        {
                            Id = id,
                            Title = title,
                            Category = $"Игра: {type.Name}",
                            Icon = "🎮",
                            Description = $"Вызов {type.FullName}.{m.Name}",
                            IsExpression = true,
                            Ports =
                            {
                                // target берём из Values, аргументы — тоже (строкой)
                            },
                            EmitExpr = (n, c) =>
                            {
                                var target = n.Values.TryGetValue("target", out var tv) && !string.IsNullOrWhiteSpace(tv?.ToString())
                                    ? tv!.ToString()! : "this";
                                var argVals = string.Join(", ", captured.Parameters.Select(p =>
                                    c.Input(p.Name, DataType.Any)));
                                return $"{target}.{captured.Name}({argVals})";
                            }
                        });
                        // data-порты по параметрам
                        foreach (var p in m.Parameters)
                            blocks[^1].Ports.Add(BlockPort.DataIn(p.Name, MapType(p.ParameterType.Name)));
                        if (m.ReturnType.Name != "Void")
                            blocks[^1].Ports.Add(BlockPort.DataOut("result", MapType(m.ReturnType.Name)));
                    }
                }
            }
        }
        return blocks;
    }

    private static DataType MapType(string clrName) => clrName switch
    {
        "Boolean" => DataType.Bool,
        "Int32" => DataType.Int,
        "Single" or "Double" => DataType.Float,
        "String" => DataType.String,
        "Vector3" => DataType.Vector3,
        "Color" => DataType.Color,
        "GameObject" => DataType.GameObject,
        "NPC" => DataType.NPC,
        "Entity" => DataType.Entity,
        "PlayerManager" => DataType.PlayerManager,
        "Item" => DataType.Item,
        "RoomController" => DataType.RoomController,
        "Cell" => DataType.Cell,
        "EnvironmentController" => DataType.EnvironmentController,
        "SoundObject" => DataType.SoundObject,
        "Texture2D" => DataType.Texture2D,
        "AudioClip" => DataType.AudioClip,
        _ => DataType.Any
    };
}
