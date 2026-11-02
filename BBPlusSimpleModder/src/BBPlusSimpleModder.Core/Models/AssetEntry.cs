using System.Text.Json.Serialization;

namespace BBPlusSimpleModder.Core.Models;

/// <summary>Ассет мода: файл внутри папки проекта, при сборке уезжает в StreamingAssets/Modded/{GUID}/.</summary>
public class AssetEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "asset";
    public AssetKind Kind { get; set; } = AssetKind.Other;

    /// <summary>Путь относительно папки проекта (напр. assets/sprites/hero.png).</summary>
    public string RelativePath { get; set; } = "";

    public long Size { get; set; }

    [JsonIgnore]
    public string Extension => Path.GetExtension(Name);
}
