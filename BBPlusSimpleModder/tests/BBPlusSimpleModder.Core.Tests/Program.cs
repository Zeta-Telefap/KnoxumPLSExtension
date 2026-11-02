using BBPlusSimpleModder.Core.BlockEngine;
using BBPlusSimpleModder.Core.Codegen;
using BBPlusSimpleModder.Core.LvlFormat;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Core.Tests;

/// <summary>
/// Консольные тесты ядра без внешних пакетов:
/// запуск: dotnet run --project tests/BBPlusSimpleModder.Core.Tests
/// </summary>
public static class Program
{
    private static int _passed, _failed;

    public static int Main()
    {
        Test("Graph JSON roundtrip", GraphRoundtrip);
        Test("Codegen: персонаж идёт к точке", CodegenNpc);
        Test("Codegen: if/else и переменные", CodegenBranch);
        Test("Codegen: Harmony prefix", CodegenPatch);
        Test("Codegen: plugin-файл с GUID и зависимостями", CodegenPlugin);
        Test("Validator: типы проводов и недостижимость", ValidatorWorks);
        Test("Lvl: write/read roundtrip v10", LvlRoundtrip);
        Test("ModComponent: DeepCopy", ComponentDeepCopy);
        Test("Project: save/load .bbmod.json", ProjectRoundtrip);
        Test("AssetKind: расширения и подпапки", AssetKinds);
        Test("Slug", Slugs);

        Console.WriteLine();
        Console.WriteLine($"ИТОГО: {_passed} пройдено, {_failed} упало");
        return _failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Action body)
    {
        try
        {
            body();
            _passed++;
            Console.WriteLine($"✔ {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"❌ {name}: {ex.Message}");
        }
    }

    private static void Assert(bool cond, string msg)
    {
        if (!cond) throw new Exception("assert: " + msg);
    }

    private static void GraphRoundtrip()
    {
        var g = new Graph { Kind = GraphKind.NpcUpdate, Title = "Тест" };
        var start = new BlockNode { BlockId = "flow.start", X = 10, Y = 10 };
        var tp = new BlockNode { BlockId = "game.targetPosition", X = 250, Y = 10 };
        tp.Values["pos"] = "1;2;3";
        g.Nodes.Add(start);
        g.Nodes.Add(tp);
        g.Wires.Add(new Wire { From = new PortRef(start.Id, "out"), To = new PortRef(tp.Id, "in") });
        g.Variables["скорость"] = new VariableDef { Type = "float", Initial = 5f };

        var json = g.ToJson();
        var g2 = Graph.FromJson(json);
        Assert(g2.Nodes.Count == 2, "узлы");
        Assert(g2.Wires.Count == 1, "провода");
        Assert(g2.Variables.Count == 1, "переменные");
        Assert(g2.Kind == GraphKind.NpcUpdate, "kind");
    }

    private static void CodegenNpc()
    {
        var g = new Graph { Kind = GraphKind.NpcUpdate };
        var start = new BlockNode { BlockId = "flow.start" };
        var tp = new BlockNode { BlockId = "game.targetPosition" };
        tp.Values["pos"] = "1;2;3";
        var log = new BlockNode { BlockId = "game.log" };
        log.Values["message"] = "Привет!";
        g.Nodes.AddRange(new[] { start, tp, log });
        g.Wires.Add(new Wire { From = new PortRef(start.Id, "out"), To = new PortRef(tp.Id, "in") });
        g.Wires.Add(new Wire { From = new PortRef(tp.Id, "out"), To = new PortRef(log.Id, "in") });

        var code = new GraphToCSharp().GenerateMethod(g);
        Assert(code.Contains("public override void VirtualUpdate()"), "сигнатура VirtualUpdate");
        Assert(code.Contains("TargetPosition(new Vector3(1f, 2f, 3f));"), "TargetPosition с вектором, got:\n" + code);
        Assert(code.Contains("Debug.Log(\"Привет!\");"), "лог");
    }

    private static void CodegenBranch()
    {
        var g = new Graph { Kind = GraphKind.NpcUpdate };
        var start = new BlockNode { BlockId = "flow.start" };
        var br = new BlockNode { BlockId = "flow.branch" };
        br.Values["cond"] = false;
        var lit = new BlockNode { BlockId = "lit.bool" };
        lit.Values["value"] = true;
        var log = new BlockNode { BlockId = "game.log" };
        log.Values["message"] = "да";
        g.Nodes.AddRange(new[] { start, br, lit, log });
        g.Wires.Add(new Wire { From = new PortRef(start.Id, "out"), To = new PortRef(br.Id, "in") });
        g.Wires.Add(new Wire { From = new PortRef(lit.Id, "value"), To = new PortRef(br.Id, "cond") });
        g.Wires.Add(new Wire { From = new PortRef(br.Id, "then"), To = new PortRef(log.Id, "in") });
        g.Variables["счётчик"] = new VariableDef { Type = "int", Initial = 0 };

        var code = new GraphToCSharp().GenerateMethod(g);
        Assert(code.Contains("if (true)"), "if из литерала, got:\n" + code);
        Assert(code.Contains("int счётчик = 0;") || code.Contains("int _ = 0;") || code.Contains("счётчик"), "переменная объявлена");
    }

    private static void CodegenPatch()
    {
        var c = ModComponent.Create(ComponentKind.Patch, new ModComponent { Kind = ComponentKind.Root });
        c.Properties["Цель (класс)"] = "Baldi";
        c.Properties["Метод"] = "TakeApple";
        c.Properties["Тип патча"] = "Prefix";
        var start = new BlockNode { BlockId = "flow.start" };
        var raw = new BlockNode { BlockId = "stmt.raw" };
        raw.Values["code"] = "return false;";
        c.Graph!.Nodes.Add(start);
        c.Graph.Nodes.Add(raw);
        c.Graph.Wires.Add(new Wire { From = new PortRef(start.Id, "out"), To = new PortRef(raw.Id, "in") });

        var code = new GraphToCSharp().GenerateClassFile(c);
        Assert(code.Contains("[HarmonyLib.HarmonyPatch(typeof(Baldi), \"TakeApple\")]"), "HarmonyPatch атрибут, got:\n" + code);
        Assert(code.Contains("public static bool Prefix(Baldi __instance)"), "сигнатура Prefix");
        Assert(code.Contains("return false;"), "raw-код внутри");
        Assert(code.Contains("return true;"), "дефолтный return true для prefix");
    }

    private static void CodegenPlugin()
    {
        var p = new ModProject { Name = "Chaos School", Guid = "com.knoxum.baldiplus.simplemodder.chaos_school", Version = "1.0.0" };
        var npc = ModComponent.Create(ComponentKind.Character, p.Root);
        npc.Name = "Куст";
        p.Root.Children.Add(npc);

        var code = new GraphToCSharp().GeneratePluginFile(p);
        Assert(code.Contains("[BepInPlugin(\"com.knoxum.baldiplus.simplemodder.chaos_school\", \"Chaos School\", \"1.0.0\")]"), "BepInPlugin");
        Assert(code.Contains("mtm101.rulerp.bbplus.baldidevapi"), "зависимость от DevAPI");
        Assert(code.Contains("NPCBuilder<ChaosSchoolMod.Generated.KustNPC>"), "регистрация NPC через NPCBuilder");
        Assert(code.Contains("StreamingAssets/Modded/"), "путь ресурсов в комментарии");
    }

    private static void ValidatorWorks()
    {
        var g = new Graph { Kind = GraphKind.NpcUpdate };
        var a = new BlockNode { BlockId = "lit.string" };       // out string
        var b = new BlockNode { BlockId = "game.targetPosition" }; // in pos Vector3
        g.Nodes.Add(a);
        g.Nodes.Add(b);
        g.Wires.Add(new Wire { From = new PortRef(a.Id, "value"), To = new PortRef(b.Id, "pos") });
        var r = GraphValidator.Validate(g, BlockCatalog.Find);
        Assert(r.Warnings.Any(w => w.Contains("Несовпадение типов")), "warning о типах, got: " + string.Join(";", r.Warnings));
        Assert(r.Warnings.Any(w => w.Contains("недостижим")), "warning о недостижимости");
    }

    private static void LvlRoundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.lvl");
        var m = new BaldiLevelModel(10, 8) { LevelTitle = "Тестовый этаж", TimeLimit = 123f };
        m.SetCell(1, 1, 2);
        m.SetCell(2, 1, 1);
        m.Doors.Add(new DoorModel { X = 1, Y = 2, Direction = 1, Room = "Classroom" });
        m.Npcs.Add(new NpcSpawnModel { NpcGuid = "com.knoxum.test.npc", X = 3, Y = 3 });
        m.Items.Add(new ItemSpawnModel { ItemGuid = "com.knoxum.test.item", X = 4, Y = 4 });
        m.Posters.Add(new PosterModel { PosterGuid = "poster1", X = 5, Y = 5, Direction = 2 });
        m.Save(path);
        var m2 = BaldiLevelModel.Load(path);
        Assert(m2.Width == 10 && m2.Height == 8, "размер");
        Assert(m2.GetCell(1, 1) == 2 && m2.GetCell(2, 1) == 1, "клетки");
        Assert(m2.LevelTitle == "Тестовый этаж", "название");
        Assert(Math.Abs(m2.TimeLimit - 123f) < 0.001, "timeLimit");
        Assert(m2.Doors.Count == 1 && m2.Doors[0].Room == "Classroom", "двери");
        Assert(m2.Npcs.Count == 1 && m2.Npcs[0].NpcGuid.Contains("knoxum"), "NPC");
        Assert(m2.Items.Count == 1, "предметы");
        Assert(m2.Posters.Count == 1 && m2.Posters[0].Direction == 2, "постеры");
        File.Delete(path);
    }

    private static void ComponentDeepCopy()
    {
        var root = new ModComponent { Kind = ComponentKind.Root, Name = "Мод" };
        var npc = ModComponent.Create(ComponentKind.Character, root);
        npc.Name = "Балди 2";
        npc.Properties["Скорость"] = 9f;
        root.Children.Add(npc);

        var copy = npc.DeepCopy();
        Assert(copy.Id != npc.Id, "новый id");
        Assert(copy.Name.Contains("копия"), "имя с пометкой");
        Assert(copy.Graph != null && copy.Graph.Kind == GraphKind.NpcUpdate, "граф скопирован");
        Assert(copy.Children.Count == 0, "дети");
    }

    private static void ProjectRoundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var p = new ModProject
        {
            Name = "Тест",
            Guid = "com.knoxum.baldiplus.simplemodder.test",
            Version = "0.1",
            GamePath = "/games/bbplus",
            ProjectDir = dir
        };
        p.Root.Name = "Тест";
        var fl = ModComponent.Create(ComponentKind.Floor, p.Root);
        p.Root.Children.Add(fl);
        p.Assets.Add(new AssetEntry { Name = "hero.png", Kind = AssetKind.Sprite, RelativePath = "assets/sprites/hero.png" });
        p.Save(dir);

        var p2 = ModProject.Load(Path.Combine(dir, ModProject.ProjectFileName));
        Assert(p2.Guid == p.Guid, "guid");
        Assert(p2.Root.Children.Count == 1 && p2.Root.Children[0].Kind == ComponentKind.Floor, "дети восстановлены");
        Assert(p2.Assets.Count == 1, "ассеты");
        Assert(p2.Root.Children[0].Parent == p2.Root, "parent перевязан");
        Directory.Delete(dir, true);
    }

    private static void AssetKinds()
    {
        Assert(AssetKindUtil.FromExtension("a.png") == AssetKind.Sprite, "png→sprite");
        Assert(AssetKindUtil.FromExtension("m.OBJ") == AssetKind.Model, "obj→model");
        Assert(AssetKindUtil.FromExtension("s.wav") == AssetKind.Audio, "wav→audio");
        Assert(AssetKindUtil.SubFolder(AssetKind.Sprite) == "sprites", "subfolder sprites");
        Assert(AssetKindUtil.SubFolder(AssetKind.Model) == "models", "subfolder models");
    }

    private static void Slugs()
    {
        Assert(GraphToCSharp.Slug("Chaos School") == "ChaosSchool", "slug пробелы");
        Assert(GraphToCSharp.Slug("мой мод 2") == "MoyMod2", "кириллица транслитерируется: " + GraphToCSharp.Slug("мой мод 2"));
        Assert(GraphToCSharp.Slug("Куст") == "Kust", "Куст→Kust");
        Assert(GraphToCSharp.Slug("2 этаж") == "M2Etaph".Replace("Etaph","Etash") || GraphToCSharp.Slug("2 этаж").StartsWith("M2"), "цифра в начале: " + GraphToCSharp.Slug("2 этаж"));
        Assert(GraphToCSharp.Slug("") == "Thing", "пусто");
    }
}
