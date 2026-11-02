using System.Globalization;
using System.Text.Json;

namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>
/// Определение блока (из каталога): порты + делегаты генерации C#.
/// Structural-блоки (ветвления/циклы/wait) обрабатываются особо в Codegen.GraphToCSharp по Id.
/// </summary>
public class BlockDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "Общее";
    public string Icon { get; set; } = "⬜";
    public string Description { get; set; } = "";

    public List<BlockPort> Ports { get; } = new();

    /// <summary>Если true — блок-выражение (EmitExpr возвращает C#-выражение для data-порта).</summary>
    public bool IsExpression { get; set; }

    /// <summary>Эмит statement(ов) для потокового блока.</summary>
    public Func<BlockNode, EmitContext, string>? Emit { get; set; }

    /// <summary>Эмит выражения для expression-блока.</summary>
    public Func<BlockNode, EmitContext, string>? EmitExpr { get; set; }

    public BlockPort? FindPort(string name, PortDirection dir)
        => Ports.FirstOrDefault(p => p.Name == name && p.Direction == dir);
}

/// <summary>Контекст эмиссии: доступ к графу, узлу, резолверу определений.</summary>
public class EmitContext
{
    public Graph Graph { get; }
    public BlockNode Node { get; }
    public Func<string, BlockDefinition?> Resolve { get; }

    public EmitContext(Graph graph, BlockNode node, Func<string, BlockDefinition?> resolve)
    {
        Graph = graph; Node = node; Resolve = resolve;
    }

    /// <summary>Выражение для data-in порта: либо по проводу (expression-узел), либо литерал из Values.</summary>
    public string Input(string portName, DataType expected)
    {
        var wire = Graph.DataIn(Node.Id, portName);
        if (wire != null)
        {
            var srcNode = Graph.FindNode(wire.From.NodeId);
            var srcDef = srcNode == null ? null : Resolve(srcNode.BlockId);
            if (srcNode != null && srcDef?.IsExpression == true && srcDef.EmitExpr != null)
                return srcDef.EmitExpr(srcNode, new EmitContext(Graph, srcNode, Resolve));
            return $"/* нет выражения: {wire.From} */ null";
        }
        // литерал
        Node.Values.TryGetValue(portName, out var v);
        return Literal(v, expected);
    }

    /// <summary>"#RRGGBB" → (r,g,b) 0..1.</summary>
    public static bool TryHexColor(string hex, out float r, out float g, out float b)
    {
        r = g = b = 0;
        if (string.IsNullOrEmpty(hex)) return false;
        var h = hex.TrimStart('#');
        if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            r = ((rgb >> 16) & 0xFF) / 255f;
            g = ((rgb >> 8) & 0xFF) / 255f;
            b = (rgb & 0xFF) / 255f;
            return true;
        }
        return false;
    }

    public static string Literal(object? v, DataType type)
    {
        if (v == null) return type switch
        {
            DataType.Bool => "false",
            DataType.Int => "0",
            DataType.Float => "0f",
            DataType.String => "\"\"",
            DataType.Vector3 => "Vector3.zero",
            DataType.Color => "Color.white",
            _ => "null"
        };
        if (v is JsonElement je) v = je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => je.ToString()
        };
        return type switch
        {
            DataType.String => JsonSerializer.Serialize(v?.ToString() ?? ""),
            DataType.Bool => v is bool bb
                ? (bb ? "true" : "false")
                : (string.Equals(v?.ToString(), "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false"),
            DataType.Int => Convert.ToInt32(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            DataType.Float => Convert.ToSingle(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "f",
            DataType.Vector3 => v?.ToString() is string s && (s.Contains(';') || s.Contains(','))
                ? "new Vector3(" + string.Join(", ", s.Split(';', ',').Select(x => x.Trim() + "f")) + ")"
                : "Vector3.zero",
            DataType.Color => v?.ToString() is string col && TryHexColor(col, out var r, out var g, out var b)
                ? "new Color(" + r.ToString(CultureInfo.InvariantCulture) + "f, "
                              + g.ToString(CultureInfo.InvariantCulture) + "f, "
                              + b.ToString(CultureInfo.InvariantCulture) + "f)"
                : "Color.white",
            // Any: выводим по runtime-типу, чтобы литералы переменных компилировались
            _ => v switch
            {
                bool bb => bb ? "true" : "false",
                string s => JsonSerializer.Serialize(s),
                int or long or short or byte => Convert.ToString(v, CultureInfo.InvariantCulture)!,
                float or double or decimal => Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "f",
                _ => v.ToString() ?? "null"
            }
        };
    }
}
