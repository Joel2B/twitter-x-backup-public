using Backup.App.Models.Config.Request;
using Microsoft.Extensions.Configuration;
using ApiConfig = Backup.App.Models.Config.Api;

namespace Backup.App.Models.Config;

public static class ConfigLoader
{
    public static App Load()
    {
        string configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
        return LoadSplit(configDirectory);
    }

    private static App LoadSplit(string configDirectory)
    {
        Dictionary<string, ApiConfig.Api> api = LoadApiFile(configDirectory);
        ValidateAndNormalizeApi(api);

        Dictionary<string, FetchItem> fetch = LoadFetchFile(configDirectory, api);
        ApplyFetchToApi(api, fetch);

        Services services = LoadFile<Services>(configDirectory, "Services.json");
        ValidateServices(services);

        return new()
        {
            Api = api,
            Fetch = fetch,
            Services = services,
            Data = LoadFile<Data.Data>(configDirectory, "Data.json"),
            Downloads = LoadFile<Downloads.Downloads>(configDirectory, "Downloads.json"),
            Medias = LoadFile<Medias.Medias>(configDirectory, "Medias.json"),
            Proxy = LoadFile<Proxy.Proxy>(configDirectory, "Proxy.json"),
            Debug = LoadFile<Debug>(configDirectory, "Debug.json"),
            Tasks = LoadFile<Tasks.Tasks>(configDirectory, "Tasks.json"),
            Bulk = LoadFile<Bulk>(configDirectory, "Bulk.json"),
            Network = LoadFile<Network>(configDirectory, "Network.json"),
        };
    }

    private static Dictionary<string, ApiConfig.Api> LoadApiFile(string configDirectory)
    {
        Dictionary<string, ApiConfig.Api> api = LoadFile<Dictionary<string, ApiConfig.Api>>(
            configDirectory,
            "Api.json"
        );

        foreach (var kvp in api)
        {
            string key = kvp.Key;
            ApiConfig.Api value = kvp.Value;

            if (value is null)
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is null"
                );

            if (string.IsNullOrWhiteSpace(value.Id))
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is missing required field 'Id'"
                );

            if (value.Request is null)
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is missing required field 'Request'"
                );
        }

        return api;
    }

    private static Dictionary<string, FetchItem> LoadFetchFile(
        string configDirectory,
        IReadOnlyDictionary<string, ApiConfig.Api> api
    )
    {
        Dictionary<string, FetchItem> fetch = LoadFile<Dictionary<string, FetchItem>>(
            configDirectory,
            "Fetch.json"
        );

        foreach (var kvp in fetch)
        {
            string key = kvp.Key;
            FetchItem value = kvp.Value;

            if (!api.ContainsKey(key))
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{key}' does not exist in 'Api.json'"
                );

            if (value is null)
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{key}' is null"
                );

            if (!TryReadCount(value.Count, out int normalizedCount) || normalizedCount <= 0)
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{key}' has invalid field 'Count' (must be positive integer)"
                );

            if (!TryReadCount(value.Api, out int normalizedApiCount) || normalizedApiCount <= 0)
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{key}' has invalid field 'Api' (must be positive integer)"
                );

            value.Count = normalizedCount;
            value.Api = normalizedApiCount;
        }

        return fetch;
    }

    private static void ApplyFetchToApi(
        IReadOnlyDictionary<string, ApiConfig.Api> api,
        IReadOnlyDictionary<string, FetchItem> fetch
    )
    {
        foreach (var kvp in fetch)
        {
            ApiConfig.Api entry = api[kvp.Key];
            Request.Request request = entry.Request;

            if (!request.Query.Variables.ContainsKey("count"))
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{kvp.Key}' requires 'Request:Query:Variables:count' in 'Api.json'"
                );

            request.Query.Variables["count"] = kvp.Value.Api;
            request.Query.Variables["cursor"] = null;
        }
    }

    private static void ValidateAndNormalizeApi(IReadOnlyDictionary<string, ApiConfig.Api> api)
    {
        foreach (var kvp in api)
        {
            string key = kvp.Key;
            ApiConfig.Api entry = kvp.Value;
            Request.Request request = entry.Request;

            if (string.IsNullOrWhiteSpace(request.Url))
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is missing required field 'Request:Url'"
                );

            if (request.Query is null)
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is missing required field 'Request:Query'"
                );

            if (request.Query.Variables is null)
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' is missing required field 'Request:Query:Variables'"
                );

            RequestMergeUtils.NormalizeVariables(request.Query.Variables);

            request.Query.Features ??= [];
            request.Query.FieldToggles ??= [];
            request.Headers ??= [];

            if (!request.Query.Variables.TryGetValue("count", out object? countValue))
                continue;

            if (!TryReadCount(countValue, out int normalizedCount))
                throw new Exception(
                    $"error deserializing config file 'Api.json': entry '{key}' has invalid 'Request:Query:Variables:count' (must be positive integer or -1)"
                );

            request.Query.Variables["count"] = normalizedCount;
            request.Query.Variables["cursor"] = null;
        }
    }

    private static void ValidateServices(Services services)
    {
        if (services.User is null)
            throw new Exception(
                "error deserializing config file 'Services.json': section 'User' is required"
            );

        string userId = services.User.Id?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
            throw new Exception(
                "error deserializing config file 'Services.json': field 'User:Id' is required"
            );

        services.User.Id = userId;
    }

    private static T LoadFile<T>(string configDirectory, string fileName)
    {
        string path = Path.Combine(configDirectory, fileName);

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "BACKUP__")
            .Build();

        return config.Get<T>()
            ?? throw new Exception($"error deserializing config file '{fileName}'");
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
