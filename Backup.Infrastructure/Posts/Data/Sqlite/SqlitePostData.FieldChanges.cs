using System.Text.Json;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static string? SerializeJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
