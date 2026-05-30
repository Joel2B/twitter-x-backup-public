using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public sealed class ConfigNormalizationService
{
    public void ValidateApiFileEntries(
        string apiFileName,
        IReadOnlyDictionary<string, ConfigApiFileEntry?> api
    )
    {
        foreach (KeyValuePair<string, ConfigApiFileEntry?> kvp in api)
        {
            string key = kvp.Key;
            ConfigApiFileEntry? value = kvp.Value;

            if (value is null)
                throw new Exception(
                    $"error deserializing api file '{apiFileName}': entry '{key}' is null"
                );

            if (string.IsNullOrWhiteSpace(value.Id))
                throw new Exception(
                    $"error deserializing api file '{apiFileName}': entry '{key}' is missing required field 'Id'"
                );

            if (!value.HasRequest)
                throw new Exception(
                    $"error deserializing api file '{apiFileName}': entry '{key}' is missing required field 'Request'"
                );
        }
    }

    public void ValidateUsers(IReadOnlyList<ConfigUser> users)
    {
        if (users.Count == 0)
            throw new Exception(
                "error deserializing config file 'Services.json': section 'Users' is required"
            );

        HashSet<string> userIds = [];

        foreach (ConfigUser user in users)
        {
            string userId = user.Id?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userId))
                throw new Exception(
                    "error deserializing config file 'Services.json': field 'Users:Id' is required"
                );

            if (!userIds.Add(userId))
                throw new Exception(
                    $"error deserializing config file 'Services.json': duplicate user id '{userId}'"
                );

            user.Id = userId;
        }
    }

    public void ValidateAndNormalizeApi(IReadOnlyList<ConfigApiEntry> apiEntries)
    {
        foreach (ConfigApiEntry entry in apiEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Url))
                throw new Exception(
                    $"error deserializing api file: entry '{entry.Key}' is missing required field 'Request:Url'"
                );

            if (entry.Variables is null)
                throw new Exception(
                    $"error deserializing api file: entry '{entry.Key}' is missing required field 'Request:Query:Variables'"
                );

            QueryVariableNormalizer.Normalize(entry.Variables);

            entry.Features ??= [];
            entry.FieldToggles ??= [];
            entry.Headers ??= [];

            if (!entry.Variables.TryGetValue("count", out object? countValue))
                continue;

            if (!TryReadCount(countValue, out int normalizedCount))
                throw new Exception(
                    $"error deserializing api file: entry '{entry.Key}' has invalid 'Request:Query:Variables:count' (must be positive integer or -1)"
                );

            entry.Variables["count"] = normalizedCount;
            entry.Variables["cursor"] = null;
        }
    }

    public void NormalizeFetch(IReadOnlyList<ConfigFetchEntry> fetchEntries)
    {
        foreach (ConfigFetchEntry entry in fetchEntries)
        {
            if (!TryReadCount(entry.CountRaw, out int normalizedCount) || normalizedCount <= 0)
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{entry.Key}' has invalid field 'Count' (must be positive integer)"
                );

            if (!TryReadCount(entry.ApiRaw, out int normalizedApiCount) || normalizedApiCount <= 0)
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{entry.Key}' has invalid field 'Api' (must be positive integer)"
                );

            entry.Count = normalizedCount;
            entry.Api = normalizedApiCount;
        }
    }

    public void ApplyFetchToApi(
        IReadOnlyList<ConfigApiEntry> apiEntries,
        IReadOnlyList<ConfigFetchEntry> fetchEntries
    )
    {
        Dictionary<string, ConfigApiEntry> apiByKey = apiEntries.ToDictionary(entry => entry.Key);

        foreach (ConfigFetchEntry fetch in fetchEntries)
        {
            if (!apiByKey.TryGetValue(fetch.Key, out ConfigApiEntry? entry))
                continue;

            if (!entry.Variables.ContainsKey("count"))
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{fetch.Key}' requires 'Request:Query:Variables:count' in api config"
                );

            entry.Variables["count"] = fetch.Api;
            entry.Variables["cursor"] = null;
        }
    }

    private static bool TryReadCount(object? value, out int count)
    {
        count = 0;

        if (value is null)
            return false;

        switch (value)
        {
            case int intValue:
                count = intValue;
                break;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                count = (int)longValue;
                break;
            default:
                if (!int.TryParse(value.ToString(), out count))
                    return false;
                break;
        }

        return count > 0 || count == -1;
    }

}
