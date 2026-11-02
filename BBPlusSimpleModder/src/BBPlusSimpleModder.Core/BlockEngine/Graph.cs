using System.Text.Json;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>
/// Граф кирпичиков: узлы + провода + переменные.
/// Сериализуется в JSON (файлы graphs/*.graph.json в папке проекта).
/// </summary>
public class Graph
{
    public GraphKind Kind { get; set; } = GraphKind.Function;
    public string Title { get; set; } = "Граф";

    public List<BlockNode> Nodes { get; } = new();
    public List<Wire> Wires { get; } = new();

    /// <summary>Переменные графа: имя → (тип, начальное значение).</summary>
    public Dictionary<string, VariableDef> Variables { get; } = new();

    public BlockNode? FindNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Исходящий exec-провод из порта.</summary>
    public Wire? ExecOut(string nodeId, string portName = "out")
        => Wires.FirstOrDefault(w => w.From.NodeId == nodeId && w.From.PortName == portName);

    /// <summary>Входящий data-провод в порт.</summary>
    public Wire? DataIn(string nodeId, string portName)
        => Wires.FirstOrDefault(w => w.To.NodeId == nodeId && w.To.PortName == portName);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts.Default);

    public static Graph FromJson(string json) => JsonSerializer.Deserialize<Graph>(json, JsonOpts.Default) ?? new Graph();
}

/// <summary>Переменная графа.</summary>
public class VariableDef
{
    public string Type { get; set; } = "float";
    public object? Initial { get; set; }
}
