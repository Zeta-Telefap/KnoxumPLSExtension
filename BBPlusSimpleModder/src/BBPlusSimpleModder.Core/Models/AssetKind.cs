namespace BBPlusSimpleModder.Core.Models;

/// <summary>Вид ассета мода. Определяет подпапку в StreamingAssets/Modded/{GUID}/.</summary>
public enum AssetKind
{
    Sprite,
    Texture,
    Model,
    Audio,
    Data,     // json и прочие данные
    Other
}

/// <summary>Утилиты определения вида ассета по расширению файла.</summary>
public static class AssetKindUtil
{
    public static AssetKind FromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" => AssetKind.Sprite,
            ".tga" or ".dds" or ".bmp" => AssetKind.Texture,
            ".obj" => AssetKind.Model,
            ".wav" or ".ogg" or ".mp3" => AssetKind.Audio,
            ".json" or ".txt" or ".csv" => AssetKind.Data,
            _ => AssetKind.Other
        };
    }

    /// <summary>Подпапка внутри StreamingAssets/Modded/{GUID}/ для данного вида.</summary>
    public static string SubFolder(AssetKind kind) => kind switch
    {
        AssetKind.Sprite => "sprites",
        AssetKind.Texture => "textures",
        AssetKind.Model => "models",
        AssetKind.Audio => "audio",
        AssetKind.Data => "data",
        _ => "other"
    };
}
