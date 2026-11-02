using System.Text;
using System.Text.RegularExpressions;
using BBPlusSimpleModder.Core.BlockEngine;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Core.Codegen;

/// <summary>
/// Генерация C# из графа кирпичиков:
///  - обход exec-потока от «Старт»;
///  - структурные блоки (if/while/for/sequence/wait/return) — рекурсивно;
///  - data-порты → выражения через EmitContext;
///  - на выходе метод с сигнатурой по GraphKind + полный файл класса компонента.
/// </summary>
public class GraphToCSharp
{
    private readonly Func<string, BlockDefinition?> _resolve;
    private bool _coroutine;
    public List<string> Warnings { get; } = new();

    public GraphToCSharp(Func<string, BlockDefinition?>? resolve = null)
        => _resolve = resolve ?? BlockCatalog.Find;

    // ================= метод =================

    /// <summary>Сигнатура метода по типу графа (класс-контекст: NPC/Item/RandomEvent/RoomFunction).</summary>
    public static string Signature(GraphKind kind, bool coroutine) => kind switch
    {
        GraphKind.NpcInitialize => "public override void Initialize()",
        GraphKind.NpcUpdate => "public override void VirtualUpdate()",
        GraphKind.NpcHear => "public override void Hear(GameObject source, Vector3 position, int value)",
        GraphKind.NpcPlayerSighted => "public override void PlayerSighted(PlayerManager player)",
        GraphKind.NpcPlayerLost => "public override void PlayerLost(PlayerManager player)",
        GraphKind.NpcTriggerEnter => "protected override void VirtualOnTriggerEnter(Entity otherEntity, Collider other)",
        GraphKind.NpcTriggerStay => "protected override void VirtualOnTriggerStay(Entity otherEntity, Collider other)",
        GraphKind.NpcTriggerExit => "protected override void VirtualOnTriggerExit(Entity otherEntity, Collider other)",
        GraphKind.NpcDoorHit => "public new void DoorHit(StandardDoor door)",
        GraphKind.ItemUse => "public override bool Use(PlayerManager pm)",
        GraphKind.ItemPostUse => "public override void PostUse(PlayerManager pm)",
        GraphKind.EventRun => "public void Trigger()",
        GraphKind.RoomBuild => "public override void Build(LevelBuilder builder, System.Random rng)",
        GraphKind.OnAssetsLoaded => "public void OnAssetsLoaded()",
        _ => coroutine ? "public IEnumerator Run()" : "public void Run()"
    };

    public string GenerateMethod(Graph g)
    {
        Warnings.Clear();
        var coroutine = g.Nodes.Any(n => n.BlockId == "flow.wait") ||
                        g.Kind is GraphKind.Function or GraphKind.EventRun or GraphKind.OnAssetsLoaded;
        _coroutine = coroutine;
        var sig = Signature(g.Kind, coroutine);
        if (g.Nodes.Any(n => n.BlockId == "flow.wait") && !coroutine)
            Warnings.Add("Блок «Ждать» в не-корутинном хуке: будет вставлен комментарий (метод не IEnumerator).");
        var sb = new StringBuilder();
        sb.AppendLine($"        {sig}");
        sb.AppendLine("        {");
        // переменные графа → локалы
        foreach (var (name, v) in g.Variables)
            sb.AppendLine($"            {v.Type} {Sanitize(name)} = {EmitContext.Literal(v.Initial, DataType.Any)};");
        var start = g.Nodes.FirstOrDefault(n => n.BlockId == "flow.start");
        if (start != null)
            EmitFlow(g, start, "out", 3, sb);
        else if (g.Nodes.Count > 0)
            sb.AppendLine("            // нет блока «Старт»");
        // страховка для bool-mетодов
        if (g.Kind == GraphKind.ItemUse)
            sb.AppendLine("            return true;");
        if (coroutine && g.Kind == GraphKind.ItemUse)
            sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    // ================= обход exec-потока =================

    private void EmitFlow(Graph g, BlockNode node, string outPort, int indent, StringBuilder sb)
    {
        var next = g.ExecOut(node.Id, outPort);
        if (next == null) return;
        var target = g.FindNode(next.To.NodeId);
        if (target == null) return;
        EmitNode(g, target, indent, sb);
    }

    private void EmitNode(Graph g, BlockNode n, int indent, StringBuilder sb)
    {
        var def = _resolve(n.BlockId);
        var pad = new string(' ', indent * 4);
        var ctx = new EmitContext(g, n, _resolve);

        switch (n.BlockId)
        {
            case "flow.branch":
            {
                var cond = ctx.Input("cond", DataType.Bool);
                sb.AppendLine($"{pad}if ({cond})");
                sb.AppendLine($"{pad}{{");
                EmitFlow(g, n, "then", indent + 1, sb);
                sb.AppendLine($"{pad}}}");
                if (g.ExecOut(n.Id, "else") != null)
                {
                    sb.AppendLine($"{pad}else");
                    sb.AppendLine($"{pad}{{");
                    EmitFlow(g, n, "else", indent + 1, sb);
                    sb.AppendLine($"{pad}}}");
                }
                return; // дальше идут только then/else
            }
            case "flow.while":
            {
                var cond = ctx.Input("cond", DataType.Bool);
                sb.AppendLine($"{pad}while ({cond})");
                sb.AppendLine($"{pad}{{");
                EmitFlow(g, n, "body", indent + 1, sb);
                sb.AppendLine($"{pad}}}");
                EmitFlow(g, n, "done", indent, sb);
                return;
            }
            case "flow.for":
            {
                var from = ctx.Input("from", DataType.Int);
                var to = ctx.Input("to", DataType.Int);
                sb.AppendLine($"{pad}for (int i = {from}; i < {to}; i++)");
                sb.AppendLine($"{pad}{{");
                EmitFlow(g, n, "body", indent + 1, sb);
                sb.AppendLine($"{pad}}}");
                EmitFlow(g, n, "done", indent, sb);
                return;
            }
            case "flow.sequence":
            {
                for (int i = 0; ; i++)
                {
                    var w = g.ExecOut(n.Id, $"out{i}");
                    if (w == null) break;
                    var t = g.FindNode(w.To.NodeId);
                    if (t != null) EmitNode(g, t, indent, sb);
                }
                return; // каждая ветка сама не продолжается общим out
            }
            case "flow.wait":
            {
                var sec = ctx.Input("seconds", DataType.Float);
                if (_coroutine)
                    sb.AppendLine($"{pad}yield return new UnityEngine.WaitForSeconds({sec});");
                else
                    sb.AppendLine($"{pad}// wait {sec}с — недоступно в этом хуке (нужна корутина)");
                EmitFlow(g, n, "out", indent, sb);
                return;
            }
            case "flow.return":
            {
                var hasVal = g.DataIn(n.Id, "value") != null || n.Values.ContainsKey("value");
                var val = hasVal ? ctx.Input("value", DataType.Any) : null;
                sb.AppendLine($"{pad}return{(val == null || val == "null" ? "" : " " + val)};");
                return;
            }
            case "flow.start":
                EmitFlow(g, n, "out", indent, sb);
                return;
            default:
            {
                if (def?.Emit != null)
                {
                    var code = def.Emit(n, ctx);
                    foreach (var line in code.Split('\n'))
                        sb.AppendLine(pad + line.TrimEnd());
                }
                else if (def == null)
                {
                    sb.AppendLine($"{pad}// неизвестный блок: {n.BlockId}");
                    Warnings.Add($"Неизвестный блок «{n.BlockId}» (узел {n.Id})");
                }
                EmitFlow(g, n, "out", indent, sb);
                return;
            }
        }
    }

    // ================= класс компонента =================

    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а']="a",['б']="b",['в']="v",['г']="g",['д']="d",['е']="e",['ё']="e",['ж']="zh",
        ['з']="z",['и']="i",['й']="y",['к']="k",['л']="l",['м']="m",['н']="n",['о']="o",
        ['п']="p",['р']="r",['с']="s",['т']="t",['у']="u",['ф']="f",['х']="h",['ц']="ts",
        ['ч']="ch",['ш']="sh",['щ']="sch",['ъ']="",['ы']="y",['ь']="",['э']="e",['ю']="yu",['я']="ya"
    };

    /// <summary>Имя → валидный C#-идентификатор (PascalCase), кириллица транслитерируется: «Куст» → «Kust».</summary>
    public static string Slug(string name)
    {
        var raw = (name ?? "").ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var ch in raw)
            sb.Append(char.IsAscii(ch) ? ch : (Translit.TryGetValue(ch, out var t) ? t : ""));
        var s = Regex.Replace(sb.ToString(), "[^a-z0-9]+", " ").Trim();
        if (s.Length == 0) return "Thing";
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(p => char.ToUpperInvariant(p[0]) + p[1..]);
        var slug = string.Concat(parts);
        if (slug.Length == 0) return "Thing";
        if (char.IsDigit(slug[0])) slug = "M" + slug;
        return slug;
    }

    private static string Sanitize(string name) => Regex.Replace(name ?? "x", "[^A-Za-z0-9_]", "_");

    /// <summary>Базовый класс генерируемого компонента.</summary>
    public static string BaseClass(ComponentKind kind) => kind switch
    {
        ComponentKind.Character => "NPC",
        ComponentKind.Item => "Item",
        ComponentKind.Event => "RandomEvent",
        ComponentKind.FloorGen or ComponentKind.Object => "RoomFunction",
        _ => "MonoBehaviour"
    };

    /// <summary>Полный C# файл класса компонента (его граф → метод).</summary>
    public string GenerateClassFile(ModComponent c)
    {
        var slug = Slug(c.Name);
        var className = c.Kind switch
        {
            ComponentKind.Character => slug + "NPC",
            ComponentKind.Item => slug + "Item",
            ComponentKind.Event => slug + "Event",
            _ => slug + "Component"
        };
        var sb = new StringBuilder();
        sb.AppendLine("// Сгенерировано BB+ Simple Modder из кирпичиков. Не редактируйте руками.");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"namespace {Slug(c.Name)}Mod.Generated");
        sb.AppendLine("{");
        if (c.Kind == ComponentKind.Patch)
        {
            sb.AppendLine(GeneratePatchClass(c, slug));
        }
        else
        {
            sb.AppendLine($"    public class {className} : {BaseClass(c.Kind)}");
            sb.AppendLine("    {");
            if (c.Graph != null)
                sb.Append(GenerateMethod(c.Graph));
            else
                sb.AppendLine("        // нет графа");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Статический Harmony-патч-класс из компонента Patch (цель/метод — в свойствах).</summary>
    public string GeneratePatchClass(ModComponent c, string slug)
    {
        var target = c.Properties.TryGetValue("Цель (класс)", out var t) ? t?.ToString() ?? "Baldi" : "Baldi";
        var method = c.Properties.TryGetValue("Метод", out var m) ? m?.ToString() ?? "Method" : "Method";
        var kind = c.Properties.TryGetValue("Тип патча", out var k) && k?.ToString() == "Postfix"
            ? GraphKind.HarmonyPostfix : GraphKind.HarmonyPrefix;
        var graph = c.Graph ?? new Graph { Kind = kind };
        var ret = kind == GraphKind.HarmonyPrefix ? "bool" : "void";
        var patchMethod = kind == GraphKind.HarmonyPrefix ? "Prefix" : "Postfix";
        var sb = new StringBuilder();
        sb.AppendLine($"    [HarmonyLib.HarmonyPatch(typeof({target}), \"{method}\")]");
        sb.AppendLine($"    public static class {slug}Patch");
        sb.AppendLine("    {");
        sb.AppendLine($"        public static {ret} {patchMethod}({target} __instance)");
        sb.AppendLine("        {");
        foreach (var (name, v) in graph.Variables)
            sb.AppendLine($"            {v.Type} {Sanitize(name)} = {EmitContext.Literal(v.Initial, DataType.Any)};");
        var start = graph.Nodes.FirstOrDefault(n => n.BlockId == "flow.start");
        if (start != null) EmitFlow(graph, start, "out", 3, sb);
        if (kind == GraphKind.HarmonyPrefix) sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    /// <summary>Главный класс плагина (BepInPlugin + зависимости + регистрация сущностей).</summary>
    public string GeneratePluginFile(ModProject p)
    {
        var slug = Slug(p.Name);
        var sb = new StringBuilder();
        sb.AppendLine("// Сгенерировано BB+ Simple Modder. Точка входа BepInEx-плагина.");
        sb.AppendLine("using BepInEx;");
        sb.AppendLine("using HarmonyLib;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using MTM101BaldAPI;");
        sb.AppendLine("using MTM101BaldAPI.ObjectCreation;");
        sb.AppendLine();
        sb.AppendLine($"namespace {slug}Mod");
        sb.AppendLine("{");
        sb.AppendLine($"    [BepInPlugin(\"{p.Guid}\", \"{p.Name}\", \"{p.Version}\")]");
        sb.AppendLine("    [BepInDependency(\"mtm101.rulerp.bbplus.baldidevapi\", BepInDependency.DependencyFlags.HardDependency)]");
        sb.AppendLine("    [BepInDependency(\"mtm101.rulerp.baldiplus.levelstudioloader\", BepInDependency.DependencyFlags.SoftDependency)]");
        sb.AppendLine("    [BepInDependency(\"pixelguy.pixelmodding.baldiplus.pixelinternalapi\", BepInDependency.DependencyFlags.SoftDependency)]");
        sb.AppendLine($"    public class {slug}Plugin : BaseUnityPlugin");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string ModGuid = \"{p.Guid}\";");
        sb.AppendLine("        private void Awake()");
        sb.AppendLine("        {");
        sb.AppendLine("            Logger.LogInfo($\"[Mod] {p.Name} v{p.Version} загружен\");");
        sb.AppendLine("            var harmony = new Harmony(ModGuid);");
        sb.AppendLine("            harmony.PatchAll();   // все [HarmonyPatch]-классы мода");
        // регистрация сущностей через API
        foreach (var c in p.AllComponents())
        {
            if (c.Kind == ComponentKind.Character && c.Graph != null)
                sb.AppendLine($"            var npc_{Slug(c.Name)} = new NPCBuilder<{Slug(c.Name)}Mod.Generated.{Slug(c.Name)}NPC>(Info).Build();");
            if (c.Kind == ComponentKind.Item)
                sb.AppendLine($"            // TODO: ItemBuilder для {c.Name}");
            if (c.Kind == ComponentKind.Event)
                sb.AppendLine($"            // TODO: RandomEventBuilder для {c.Name}");
        }
        sb.AppendLine("            // ассеты лежат в StreamingAssets/Modded/" + ModGuid + "/ (sprites/, models/, audio/, data/)");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
