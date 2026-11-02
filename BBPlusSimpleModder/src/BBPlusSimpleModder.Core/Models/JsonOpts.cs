using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBPlusSimpleModder.Core.Models;

/// <summary>Общие настройки JSON для всего проекта (human-readable, enum как строки).</summary>
public static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}
