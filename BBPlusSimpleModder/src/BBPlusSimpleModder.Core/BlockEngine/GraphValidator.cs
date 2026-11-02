namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>Результат проверки графа.</summary>
public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Ok => Errors.Count == 0;
}

/// <summary>Простая валидация графа перед сборкой.</summary>
public static class GraphValidator
{
    public static ValidationResult Validate(Graph g, Func<string, BlockDefinition?> resolve)
    {
        var r = new ValidationResult();

        var start = g.Nodes.FirstOrDefault(n => n.BlockId == "flow.start");
        if (start == null && g.Nodes.Count > 0)
            r.Warnings.Add("Нет блока «Старт» — граф может не выполниться.");

        // недостижимые узлы (по exec-проводам от старта)
        var reachable = new HashSet<string>();
        if (start != null)
        {
            var queue = new Queue<BlockNode>();
            queue.Enqueue(start);
            reachable.Add(start.Id);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                foreach (var w in g.Wires.Where(w => w.From.NodeId == n.Id))
                {
                    var t = g.FindNode(w.To.NodeId);
                    if (t != null && reachable.Add(t.Id)) queue.Enqueue(t);
                }
            }
        }
        foreach (var n in g.Nodes)
            if (!reachable.Contains(n.Id) && n.BlockId != "flow.start")
                r.Warnings.Add($"Блок «{resolve(n.BlockId)?.Title ?? n.BlockId}» недостижим.");

        // проверка типов data-проводов
        foreach (var w in g.Wires)
        {
            var fromNode = g.FindNode(w.From.NodeId);
            var toNode = g.FindNode(w.To.NodeId);
            var fromDef = fromNode == null ? null : resolve(fromNode.BlockId);
            var toDef = toNode == null ? null : resolve(toNode.BlockId);
            var fp = fromDef?.FindPort(w.From.PortName, PortDirection.Out);
            var tp = toDef?.FindPort(w.To.PortName, PortDirection.In);
            if (fp == null || tp == null) continue;
            if (fp.IsExec != tp.IsExec)
                r.Errors.Add($"Провод соединяет exec и data-порты: {w.From} → {w.To}");
            else if (!fp.IsExec && fp.Type != DataType.Any && tp.Type != DataType.Any && fp.Type != tp.Type)
                r.Warnings.Add($"Несовпадение типов: {fp.Type} → {tp.Type} ({w.From} → {w.To})");
        }

        // Wait в не-корутинном хуке
        bool hasWait = g.Nodes.Any(n => n.BlockId == "flow.wait");
        bool coroutine = g.Kind is GraphKind.Function or GraphKind.EventRun or GraphKind.OnAssetsLoaded;
        if (hasWait && !coroutine)
            r.Warnings.Add("Блок «Ждать» вне корутины будет проигнорирован (или вызовет ошибку компиляции).");

        return r;
    }
}
