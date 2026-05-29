using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Downloads;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Config.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Models.Config;

public static class ConfigLoader
{
    public static string GetConfigDirectory() => Path.Combine(AppContext.BaseDirectory, "config");

    public static AppConfig Load() => LoadSplit(GetConfigDirectory());

    public static DataConfig LoadData() =>
        LoadFile<DataConfig>(GetConfigDirectory(), "Data.json", prefix: "BACKUP__");

    public static void SaveData(DataConfig data)
    {
        string path = Path.Combine(GetConfigDirectory(), "Data.json");
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

        File.WriteAllText(path, json);
    }

    private static AppConfig LoadSplit(string configDirectory)
    {
        ServicesConfig services = LoadFile<ServicesConfig>(configDirectory, "Services.json");
        ValidateServices(services);

        Dictionary<string, Dictionary<string, ApiConfig>> apiByUser = LoadApiFiles(
            configDirectory,
            services
        );

        foreach (Dictionary<string, ApiConfig> api in apiByUser.Values)
            ValidateAndNormalizeApi(api);

        Dictionary<string, FetchItem> fetch = LoadFetchFile(configDirectory);

        foreach (var api in apiByUser)
            ApplyFetchToApi(api.Key, api.Value, fetch);

        List<UsersContext> contexts = services
            .Users.Select(user => new UsersContext { UserId = user.Id, Api = apiByUser[user.Id] })
            .ToList();

        return new()
        {
            UsersContext = contexts,
            Fetch = fetch,
            Services = services,
            Data = LoadFile<DataConfig>(configDirectory, "Data.json", prefix: "BACKUP__"),
            Downloads = LoadFile<DownloadsConfig>(configDirectory, "Downloads.json"),
            Medias = LoadFile<MediasConfig>(configDirectory, "Medias.json"),
            Proxy = LoadFile<ProxyConfig>(configDirectory, "Proxy.json"),
            Debug = LoadFile<DebugConfig>(configDirectory, "Debug.json"),
            Tasks = LoadFile<TasksConfig>(configDirectory, "Tasks.json"),
            Bulk = LoadFile<BulkConfig>(configDirectory, "Bulk.json"),
            Network = LoadFile<NetworkConfig>(configDirectory, "Network.json"),
        };
    }

    private static Dictionary<string, Dictionary<string, ApiConfig>> LoadApiFiles(
        string configDirectory,
        ServicesConfig services
    )
    {
        string apiDirectory = Path.Combine(configDirectory, "Api");

        if (!Directory.Exists(apiDirectory))
            throw new Exception(
                "error deserializing config folder 'Api': directory does not exist"
            );

        Dictionary<string, Dictionary<string, ApiConfig>> apiByUser = [];
        string[] files = Directory.GetFiles(apiDirectory, "*.json", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            throw new Exception("error deserializing config folder 'Api': no json files found");

        foreach (UserService user in services.Users)
        {
            string file = Path.Combine(apiDirectory, $"{user.Id}.json");

            if (!File.Exists(file))
                throw new Exception(
                    $"error deserializing config folder 'Api': file '{user.Id}.json' not found for user '{user.Id}'"
                );

            apiByUser[user.Id] = LoadApiFile(file);
        }

        return apiByUser;
    }

    private static Dictionary<string, ApiConfig> LoadApiFile(string path)
    {
        Dictionary<string, ApiConfig> api = LoadFileByPath<Dictionary<string, ApiConfig>>(path);

        foreach (var kvp in api)
        {
            string key = kvp.Key;
            ApiConfig value = kvp.Value;

            if (value is null)
                throw new Exception(
                    $"error deserializing api file '{Path.GetFileName(path)}': entry '{key}' is null"
                );

            if (string.IsNullOrWhiteSpace(value.Id))
                throw new Exception(
                    $"error deserializing api file '{Path.GetFileName(path)}': entry '{key}' is missing required field 'Id'"
                );

            if (value.Request is null)
                throw new Exception(
                    $"error deserializing api file '{Path.GetFileName(path)}': entry '{key}' is missing required field 'Request'"
                );
        }

        return api;
    }

    private static Dictionary<string, FetchItem> LoadFetchFile(string configDirectory)
    {
        Dictionary<string, FetchItem> fetch = LoadFile<Dictionary<string, FetchItem>>(
            configDirectory,
            "Fetch.json"
        );

        foreach (var kvp in fetch)
        {
            string key = kvp.Key;
            FetchItem value = kvp.Value;

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
        string userId,
        IReadOnlyDictionary<string, ApiConfig> api,
        IReadOnlyDictionary<string, FetchItem> fetch
    )
    {
        foreach (var kvp in fetch)
        {
            if (!api.TryGetValue(kvp.Key, out ApiConfig? entry))
                continue;

            Request request = entry.Request;

            if (!request.Query.Variables.ContainsKey("count"))
                throw new Exception(
                    $"error deserializing config file 'Fetch.json': key '{kvp.Key}' requires 'Request:Query:Variables:count' in api config"
                );

            request.Query.Variables["count"] = kvp.Value.Api;
            request.Query.Variables["cursor"] = null;
        }
    }

    private static void ValidateAndNormalizeApi(IReadOnlyDictionary<string, ApiConfig> api)
    {
        foreach (var kvp in api)
        {
            string key = kvp.Key;
            ApiConfig entry = kvp.Value;
            Request request = entry.Request;

            if (string.IsNullOrWhiteSpace(request.Url))
                throw new Exception(
                    $"error deserializing api file: entry '{key}' is missing required field 'Request:Url'"
                );

            if (request.Query is null)
                throw new Exception(
                    $"error deserializing api file: entry '{key}' is missing required field 'Request:Query'"
                );

            if (request.Query.Variables is null)
                throw new Exception(
                    $"error deserializing api file: entry '{key}' is missing required field 'Request:Query:Variables'"
                );

            RequestMergeUtils.NormalizeVariables(request.Query.Variables);

            request.Query.Features ??= [];
            request.Query.FieldToggles ??= [];
            request.Headers ??= [];

            if (!request.Query.Variables.TryGetValue("count", out object? countValue))
                continue;

            if (!TryReadCount(countValue, out int normalizedCount))
                throw new Exception(
                    $"error deserializing api file: entry '{key}' has invalid 'Request:Query:Variables:count' (must be positive integer or -1)"
                );

            request.Query.Variables["count"] = normalizedCount;
            request.Query.Variables["cursor"] = null;
        }
    }

    private static void ValidateServices(ServicesConfig services)
    {
        if (services.Users.Count == 0)
            throw new Exception(
                "error deserializing config file 'Services.json': section 'Users' is required"
            );

        HashSet<string> userIds = [];

        foreach (UserService user in services.Users)
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

    private static T LoadFile<T>(string configDirectory, string fileName, string? prefix = null)
    {
        string path = Path.Combine(configDirectory, fileName);
        return LoadFileByPath<T>(path, prefix);
    }

    private static T LoadFileByPath<T>(string path, string? prefix = null)
    {
        string fileName = Path.GetFileName(path);

        ConfigurationBuilder builder = new();
        builder.AddJsonFile(path, optional: false, reloadOnChange: false);

        if (prefix is not null)
            builder.AddEnvironmentVariables(prefix: prefix);

        IConfigurationRoot config = builder.Build();

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

