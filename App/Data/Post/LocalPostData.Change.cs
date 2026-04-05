using System.Text.Json;
using Backup.App.Models.Post;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private static readonly JsonSerializerOptions ChangeJsonOptions = new();

    private static Dictionary<string, IndexData>? NormalizeIndex(
        Dictionary<string, IndexData>? index
    ) =>
        index
            ?.OrderBy(o => o.Key, StringComparer.Ordinal)
            .ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);

    private static Dictionary<string, IndexData> CloneIndex(Dictionary<string, IndexData> index) =>
        index.ToDictionary(o => o.Key, o => o.Value.Clone(), StringComparer.Ordinal);

    private static string? SerializeJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, ChangeJsonOptions);

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, ChangeJsonOptions);
    }
}
