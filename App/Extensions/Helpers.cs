using Microsoft.Extensions.Primitives;

namespace Backup.App.Extensions;

public static class DictionaryExtensions
{
    public static string GetValue(this IDictionary<string, StringValues> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }
}
