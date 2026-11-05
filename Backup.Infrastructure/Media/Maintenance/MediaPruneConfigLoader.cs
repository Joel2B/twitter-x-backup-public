using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backup.Infrastructure.Media.Maintenance;

internal static class MediaPruneConfigLoader
{
    internal static MediaPruneConfig Load(string path, string mediaStoreId, string cacheId)
    {
        if (mediaStoreId.Contains(':') || cacheId.Contains(':'))
            throw new InvalidOperationException(
                "Media store and cache IDs cannot contain ':' for PostgreSQL prune namespaces."
            );
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Data config not found.", fullPath);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fullPath));
        JsonElement root = RequireObject(document.RootElement, "Data config");
        Dictionary<string, string> aliases = ReadStringDictionary(
            RequireProperty(root, "Aliases", "Data config"),
            "Data config.Aliases"
        );
        Dictionary<int, ParsedPartition> partitions = [];

        foreach (
            JsonElement element in RequireArray(
                RequireProperty(root, "Partitions", "Data config"),
                "Data config.Partitions"
            )
        )
        {
            JsonElement partition = RequireObject(element, "Data config.Partitions item");
            int id = RequireInt32(
                RequireProperty(partition, "Id", "Data config.Partitions item"),
                "Data config.Partitions item.Id"
            );
            ParsedPartition parsed = new(
                ReadOptionalBoolean(partition, "Enabled", true, $"Partition {id}"),
                ReadStringArray(
                    RequireProperty(partition, "Paths", $"Partition {id}"),
                    $"Partition {id}.Paths"
                )
            );
            if (!partitions.TryAdd(id, parsed))
                throw new InvalidOperationException($"Partition Id must be unique: {id}");
        }

        JsonElement store = SelectById(
            RequireProperty(root, "Media", "Data config"),
            mediaStoreId,
            "Data config.Media",
            "media store"
        );
        if (!ReadOptionalBoolean(store, "Enabled", true, $"Media store '{mediaStoreId}'"))
            throw new InvalidOperationException($"Media store is disabled: {mediaStoreId}");

        JsonElement cache = SelectById(
            RequireProperty(store, "Cache", $"Media store '{mediaStoreId}'"),
            cacheId,
            $"Media store '{mediaStoreId}'.Cache",
            "media cache"
        );
        bool cacheEnabled = ReadOptionalBoolean(cache, "Enabled", true, $"Media cache '{cacheId}'");
        string cacheType = RequireString(
            RequireProperty(cache, "Type", $"Media cache '{cacheId}'"),
            $"Media cache '{cacheId}'.Type"
        );
        string connectionString = ResolveEnvironmentValue(
            RequireString(
                RequireProperty(cache, "ConnectionString", $"Media cache '{cacheId}'"),
                $"Media cache '{cacheId}'.ConnectionString"
            )
        );
        if (
            !cacheEnabled
            || !cacheType.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(connectionString)
        )
            throw new InvalidOperationException(
                "Media cache must be enabled postgres with ConnectionString."
            );

        JsonElement storePaths = RequireObject(
            RequireProperty(store, "Paths", $"Media store '{mediaStoreId}'"),
            $"Media store '{mediaStoreId}'.Paths"
        );
        JsonElement mediaPaths = RequireObject(
            RequireProperty(storePaths, "Media", $"Media store '{mediaStoreId}'.Paths"),
            $"Media store '{mediaStoreId}'.Paths.Media"
        );
        List<string> mediaPathSegments = ReadStringArray(
            RequireProperty(mediaPaths, "Paths", $"Media store '{mediaStoreId}'.Paths.Media"),
            $"Media store '{mediaStoreId}'.Paths.Media.Paths"
        );
        List<int> storePartitionIds = ReadInt32Array(
            RequireProperty(store, "Partitions", $"Media store '{mediaStoreId}'"),
            $"Media store '{mediaStoreId}'.Partitions"
        );

        Dictionary<int, string> roots = [];
        foreach (int partitionId in storePartitionIds.Distinct())
        {
            string? partitionRoot =
                partitions.TryGetValue(partitionId, out ParsedPartition? partition)
                && partition.Enabled
                    ? ResolvePartitionRoot(partition.Paths, aliases)
                    : null;
            if (partitionRoot is null)
                throw new InvalidOperationException(
                    $"Partition {partitionId} is disabled, missing, relative, or has an unresolved alias."
                );
            roots[partitionId] = Path.GetFullPath(
                Path.Combine([partitionRoot, .. mediaPathSegments])
            );
        }

        return new(mediaStoreId, cacheId, connectionString, roots);
    }

    internal static string? ResolvePartitionRoot(
        IReadOnlyList<string> segments,
        IReadOnlyDictionary<string, string> aliases
    )
    {
        if (segments.Count == 0)
            return null;
        string first = segments[0];
        if (first.StartsWith('@'))
        {
            if (first.Length == 1 || !aliases.TryGetValue(first[1..], out first!))
                return null;
        }

        if (Regex.IsMatch(first, @"^[A-Za-z]:$", RegexOptions.CultureInvariant))
            first += Path.DirectorySeparatorChar;
        if (!IsFullyQualifiedPath(first))
            return null;
        return Path.GetFullPath(Path.Combine([first, .. segments.Skip(1)]));
    }

    private static string ResolveEnvironmentValue(string value)
    {
        const string prefix = "env:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value;
        string name = value[prefix.Length..];
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Environment variable name is missing.");
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Environment variable is missing: {name}");
    }

    private static JsonElement SelectById(
        JsonElement value,
        string id,
        string path,
        string description
    )
    {
        List<JsonElement> matches = [];
        int index = 0;
        foreach (JsonElement element in RequireArray(value, path))
        {
            JsonElement item = RequireObject(element, $"{path}[{index}]");
            string itemId = RequireString(
                RequireProperty(item, "Id", $"{path}[{index}]"),
                $"{path}[{index}].Id"
            );
            if (itemId.Equals(id, StringComparison.Ordinal))
                matches.Add(item);
            index++;
        }
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"Expected exactly one {description} with Id '{id}'; found {matches.Count}."
            );
        return matches[0];
    }

    private static JsonElement RequireProperty(JsonElement value, string name, string path)
    {
        JsonElement match = default;
        int count = 0;
        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                match = property.Value;
                count++;
            }
        }
        if (count != 1)
            throw new InvalidOperationException(
                $"{path}.{name} must occur exactly once; found {count}."
            );
        return match;
    }

    private static JsonElement RequireObject(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{path} must be an object.");
        return value;
    }

    private static JsonElement.ArrayEnumerator RequireArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{path} must be an array.");
        return value.EnumerateArray();
    }

    private static string RequireString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"{path} must be a string.");
        return value.GetString()!;
    }

    private static int RequireInt32(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw new InvalidOperationException($"{path} must be a 32-bit integer.");
        return result;
    }

    private static bool ReadOptionalBoolean(
        JsonElement value,
        string name,
        bool defaultValue,
        string path
    )
    {
        JsonProperty? match = null;
        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (match.HasValue)
                throw new InvalidOperationException($"{path}.{name} must occur at most once.");
            match = property;
        }
        if (!match.HasValue)
            return defaultValue;
        if (match.Value.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"{path}.{name} must be a boolean.");
        return match.Value.Value.GetBoolean();
    }

    private static Dictionary<string, string> ReadStringDictionary(JsonElement value, string path)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (!result.TryAdd(property.Name, RequireString(property.Value, $"{path}.{property.Name}")))
                throw new InvalidOperationException(
                    $"{path} contains duplicate key '{property.Name}'."
                );
        }
        return result;
    }

    private static List<string> ReadStringArray(JsonElement value, string path)
    {
        List<string> result = [];
        int index = 0;
        foreach (JsonElement element in RequireArray(value, path))
            result.Add(RequireString(element, $"{path}[{index++}]"));
        return result;
    }

    private static List<int> ReadInt32Array(JsonElement value, string path)
    {
        List<int> result = [];
        int index = 0;
        foreach (JsonElement element in RequireArray(value, path))
            result.Add(RequireInt32(element, $"{path}[{index++}]"));
        return result;
    }

    private static bool IsFullyQualifiedPath(string path) =>
        OperatingSystem.IsWindows()
            ? Regex.IsMatch(path, @"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant)
                || path.StartsWith("\\\\", StringComparison.Ordinal)
            : path.StartsWith("/", StringComparison.Ordinal);

    private sealed record ParsedPartition(bool Enabled, List<string> Paths);
}

internal sealed record MediaPruneConfig(
    string MediaStoreId,
    string CacheId,
    string ConnectionString,
    Dictionary<int, string> MediaRoots
);
