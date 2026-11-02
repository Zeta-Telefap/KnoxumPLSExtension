namespace BBPlusSimpleModder.Core.BlockEngine;

/// <summary>Экземпляр блока на канвасе.</summary>
public class BlockNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Id определения блока из каталога (BlockDefinition.Id).</summary>
    public string BlockId { get; set; } = "";

    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Значения data-портов без входящих проводов (литералы, выбранные опции).</summary>
    public Dictionary<string, object?> Values { get; } = new();
}

/// <summary>Ссылка на порт узла (для концов провода).</summary>
public class PortRef
{
    public string NodeId { get; set; } = "";
    public string PortName { get; set; } = "";

    public PortRef() { }
    public PortRef(string nodeId, string portName) { NodeId = nodeId; PortName = portName; }

    public override string ToString() => $"{NodeId}.{PortName}";
}

/// <summary>Провод между портами.</summary>
public class Wire
{
    public PortRef From { get; set; } = new();   // Out-порт
    public PortRef To { get; set; } = new();     // In-порт
}
