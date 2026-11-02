namespace BBPlusSimpleModder.Core.BlockEngine;

public enum PortDirection { In, Out }

/// <summary>Порт узла: exec (поток выполнения) или data (значение).</summary>
public class BlockPort
{
    public string Name { get; set; } = "";
    public PortDirection Direction { get; set; } = PortDirection.In;
    public bool IsExec { get; set; }
    public DataType Type { get; set; } = DataType.Any;

    public static BlockPort ExecIn(string name = "in") => new() { Name = name, Direction = PortDirection.In, IsExec = true };
    public static BlockPort ExecOut(string name = "out") => new() { Name = name, Direction = PortDirection.Out, IsExec = true };
    public static BlockPort DataIn(string name, DataType t) => new() { Name = name, Direction = PortDirection.In, Type = t };
    public static BlockPort DataOut(string name, DataType t) => new() { Name = name, Direction = PortDirection.Out, Type = t };
}
