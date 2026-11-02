using BBPlusSimpleModder.Core.Codegen;

namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>
/// Ручной каталог «кривых» блоков. Авто-блоки из сборок игры добавляет
/// BBPlusSimpleModder.Compiler (CecilBlockCatalog) поверх этого списка.
/// </summary>
public static class BlockCatalog
{
    public static readonly List<BlockDefinition> Handcrafted = Build();

    private static BlockDefinition Def(string id, string title, string cat, string icon, string desc,
        Action<BlockDefinition> ports, Func<BlockNode, EmitContext, string>? emit = null,
        Func<BlockNode, EmitContext, string>? expr = null, bool isExpr = false)
    {
        var d = new BlockDefinition { Id = id, Title = title, Category = cat, Icon = icon, Description = desc, IsExpression = isExpr, Emit = emit, EmitExpr = expr };
        ports(d);
        return d;
    }

    private static List<BlockDefinition> Build() => new()
    {
        // ---------- поток ----------
        Def("flow.start", "▶ Старт", "Поток", "▶", "Точка входа графа",
            d => d.Ports.Add(BlockPort.ExecOut())),
        Def("flow.branch", "Ветвление (if/else)", "Поток", "🔀", "if (условие) then / else",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("cond", DataType.Bool));
                   d.Ports.Add(BlockPort.ExecOut("then")); d.Ports.Add(BlockPort.ExecOut("else")); }),
        Def("flow.while", "Цикл while", "Поток", "🔁", "Пока условие истинно",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("cond", DataType.Bool));
                   d.Ports.Add(BlockPort.ExecOut("body")); d.Ports.Add(BlockPort.ExecOut("done")); }),
        Def("flow.for", "Цикл for (i от… до…)", "Поток", "🔂", "for (int i = от; i < до; i++)",
            d => { d.Ports.Add(BlockPort.ExecIn());
                   d.Ports.Add(BlockPort.DataIn("from", DataType.Int)); d.Ports.Add(BlockPort.DataIn("to", DataType.Int));
                   d.Ports.Add(BlockPort.ExecOut("body")); d.Ports.Add(BlockPort.ExecOut("done"));
                   d.Ports.Add(BlockPort.DataOut("i", DataType.Int)); }),
        Def("flow.wait", "Ждать (сек)", "Поток", "⏱", "Ждать N секунд (граф станет корутиной)",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("seconds", DataType.Float));
                   d.Ports.Add(BlockPort.ExecOut()); }),
        Def("flow.return", "Return (выход)", "Поток", "↩️", "Выйти из метода",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("value", DataType.Any)); }),
        Def("flow.sequence", "Последовательность", "Поток", "📋", "Выполнить несколько веток подряд",
            d => { d.Ports.Add(BlockPort.ExecIn());
                   for (int i = 0; i < 4; i++) d.Ports.Add(BlockPort.ExecOut($"out{i}")); }),

        // ---------- выражения ----------
        Def("lit.int", "Число (int)", "Данные", "🔢", "Литерал int",
            d => { d.Ports.Add(BlockPort.DataOut("value", DataType.Int)); },
            expr: (n, c) => c.Input("value", DataType.Int), isExpr: true),
        Def("lit.float", "Число (float)", "Данные", "🔣", "Литерал float",
            d => d.Ports.Add(BlockPort.DataOut("value", DataType.Float)),
            expr: (n, c) => c.Input("value", DataType.Float), isExpr: true),
        Def("lit.bool", "Правда/ложь", "Данные", "☑️", "Литерал bool",
            d => d.Ports.Add(BlockPort.DataOut("value", DataType.Bool)),
            expr: (n, c) => c.Input("value", DataType.Bool), isExpr: true),
        Def("lit.string", "Текст", "Данные", "💬", "Литерал string",
            d => d.Ports.Add(BlockPort.DataOut("value", DataType.String)),
            expr: (n, c) => c.Input("value", DataType.String), isExpr: true),
        Def("lit.vector3", "Вектор3 (x;y;z)", "Данные", "🧭", "Vector3 из строки 'x;y;z'",
            d => d.Ports.Add(BlockPort.DataOut("value", DataType.Vector3)),
            expr: (n, c) => c.Input("value", DataType.Vector3), isExpr: true),
        Def("var.get", "Получить переменную", "Данные", "📥", "Значение переменной графа",
            d => { d.Ports.Add(BlockPort.DataOut("value", DataType.Any)); },
            expr: (n, c) => GraphToCSharp.Sanitize(n.Values.TryGetValue("имя", out var v) && v != null ? v.ToString()! : "x"), isExpr: true),
        Def("math.op", "Математика (+ − × ÷)", "Данные", "➕", "a op b",
            d => { d.Ports.Add(BlockPort.DataIn("a", DataType.Float)); d.Ports.Add(BlockPort.DataIn("b", DataType.Float));
                   d.Ports.Add(BlockPort.DataOut("result", DataType.Float)); },
            expr: (n, c) =>
            {
                var op = n.Values.TryGetValue("op", out var o) ? o?.ToString() ?? "+" : "+";
                var sym = op switch { "-" => "-", "*" => "*", "/" => "/", "%" => "%", _ => "+" };
                return $"({c.Input("a", DataType.Float)} {sym} {c.Input("b", DataType.Float)})";
            }, isExpr: true),
        Def("math.compare", "Сравнение (== != < > ≤ ≥)", "Данные", "⚖️", "a op b → bool",
            d => { d.Ports.Add(BlockPort.DataIn("a", DataType.Float)); d.Ports.Add(BlockPort.DataIn("b", DataType.Float));
                   d.Ports.Add(BlockPort.DataOut("result", DataType.Bool)); },
            expr: (n, c) =>
            {
                var op = n.Values.TryGetValue("op", out var o) ? o?.ToString() ?? "==" : "==";
                var sym = op switch { "!=" => "!=", "<" => "<", ">" => ">", "<=" => "<=", ">=" => ">=", _ => "==" };
                return $"({c.Input("a", DataType.Float)} {sym} {c.Input("b", DataType.Float)})";
            }, isExpr: true),
        Def("random.range", "Случайное число", "Данные", "🎲", "Random.Range(min, max)",
            d => { d.Ports.Add(BlockPort.DataIn("min", DataType.Float)); d.Ports.Add(BlockPort.DataIn("max", DataType.Float));
                   d.Ports.Add(BlockPort.DataOut("value", DataType.Float)); },
            expr: (n, c) => $"UnityEngine.Random.Range({c.Input("min", DataType.Float)}, {c.Input("max", DataType.Float)})", isExpr: true),
        Def("call", "Вызвать метод…", "Данные", "📞", "target.method(аргументы) — target/аргументы задаются в свойствах",
            d => { d.Ports.Add(BlockPort.DataOut("result", DataType.Any)); },
            expr: (n, c) =>
            {
                var target = n.Values.TryGetValue("target", out var t) && !string.IsNullOrWhiteSpace(t?.ToString()) ? t!.ToString()! : "this";
                var method = n.Values.TryGetValue("method", out var m) ? m?.ToString() ?? "Method" : "Method";
                var args = n.Values.TryGetValue("args", out var a) ? a?.ToString() ?? "" : "";
                return $"{target}.{method}({args})";
            }, isExpr: true),
        Def("npc.self", "Сам персонаж (this)", "Игра", "🧍", "Ссылка на NPC, чья логика выполняется",
            d => d.Ports.Add(BlockPort.DataOut("npc", DataType.NPC)),
            expr: (n, c) => "this", isExpr: true),
        Def("player.get", "Игрок", "Игра", "🙂", "Текущий PlayerManager",
            d => d.Ports.Add(BlockPort.DataOut("player", DataType.PlayerManager)),
            expr: (n, c) => "UnityEngine.Object.FindObjectOfType<PlayerManager>()", isExpr: true),
        Def("env.get", "Окружение (EnvironmentController)", "Игра", "🏫", "Текущий EnvironmentController",
            d => d.Ports.Add(BlockPort.DataOut("env", DataType.EnvironmentController)),
            expr: (n, c) => "UnityEngine.Object.FindObjectOfType<EnvironmentController>()", isExpr: true),

        // ---------- операторы ----------
        Def("stmt.expression", "Выполнить выражение", "Поток", "🧾", "Любое expression-блок как оператор",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("expr", DataType.Any)); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => c.Input("expr", DataType.Any) + ";"),
        Def("stmt.setvar", "Задать переменную", "Поток", "📤", "переменная = значение",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("value", DataType.Any)); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => GraphToCSharp.Sanitize(n.Values.TryGetValue("имя", out var v) && v != null ? v.ToString()! : "x") + " = " + c.Input("value", DataType.Any) + ";"),
        Def("stmt.raw", "Свой C# код", "Поток", "📝", "Сырой C# (вставляется как есть)",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => n.Values.TryGetValue("code", out var v) ? v?.ToString() ?? "// код" : "// код"),
        Def("game.log", "Написать в лог", "Игра", "📜", "Debug.Log(сообщение) — видно в LogOutput.log",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("message", DataType.String)); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => $"UnityEngine.Debug.Log({c.Input("message", DataType.String)});"),
        Def("game.targetPosition", "Идти к точке (TargetPosition)", "Игра", "🎯", "NPC.TargetPosition(Vector3)",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("pos", DataType.Vector3)); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => $"TargetPosition({c.Input("pos", DataType.Vector3)});"),
        Def("game.setGuilt", "Обвинить (SetGuilt)", "Игра", "😠", "NPC.SetGuilt(time, rule)",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.DataIn("time", DataType.Float)); d.Ports.Add(BlockPort.DataIn("rule", DataType.String)); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => $"SetGuilt({c.Input("time", DataType.Float)}, {c.Input("rule", DataType.String)});"),
        Def("game.clearGuilt", "Снять обвинение", "Игра", "😐", "NPC.ClearGuilt()",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => "ClearGuilt();"),
        Def("game.detention", "Отправить в detention", "Игра", "⛓️", "NPC.SentToDetention()",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => "SentToDetention();"),
        Def("game.despawn", "Удалить персонажа (Despawn)", "Игра", "💨", "NPC.Despawn()",
            d => { d.Ports.Add(BlockPort.ExecIn()); d.Ports.Add(BlockPort.ExecOut()); },
            emit: (n, c) => "Despawn();"),
    };

    public static BlockDefinition? Find(string id) => Handcrafted.FirstOrDefault(b => b.Id == id);
}
