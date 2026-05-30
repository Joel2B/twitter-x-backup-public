using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using ApiRequest = Backup.Infrastructure.Models.Config.Request.Request;
using Backup.Application.Config;
using Backup.Application.Config.Models;
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
    private static readonly ConfigNormalizationService _normalization = new();
    private static readonly ConfigApiFileSelectionService _apiFileSelection = new();
    private static readonly ConfigDeserializationGuardService _deserializationGuard = new();

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
        IReadOnlyList<string> normalizedUserIds = _normalization.NormalizeUserIds(
            services.Users.Select(user => user.Id).ToList()
        );

        for (int index = 0; index < normalizedUserIds.Count; index++)
            services.Users[index].Id = normalizedUserIds[index];

        Dictionary<string, Dictionary<string, ApiConfig>> apiByUser = LoadApiFiles(
            configDirectory,
            services
        );

        foreach (Dictionary<string, ApiConfig> api in apiByUser.Values)
            NormalizeApi(api);

        Dictionary<string, FetchItem> fetch = LoadFetchFile(configDirectory);

        foreach (var api in apiByUser)
            ApplyFetchToApi(api.Value, fetch);

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
        _apiFileSelection.ValidateApiDirectoryExists(Directory.Exists(apiDirectory));

        Dictionary<string, Dictionary<string, ApiConfig>> apiByUser = [];
        string[] files = Directory.GetFiles(apiDirectory, "*.json", SearchOption.TopDirectoryOnly);
        List<string> availableFileNames = files
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();
        IReadOnlyDictionary<string, string> requiredFiles = _apiFileSelection.SelectRequiredFiles(
            services.Users.Select(user => user.Id).ToList(),
            availableFileNames
        );

        foreach (UserService user in services.Users)
        {
            string file = Path.Combine(apiDirectory, requiredFiles[user.Id]);

            apiByUser[user.Id] = LoadApiFile(file);
        }

        return apiByUser;
    }

    private static Dictionary<string, ApiConfig> LoadApiFile(string path)
    {
        Dictionary<string, ApiConfig> api = LoadFileByPath<Dictionary<string, ApiConfig>>(path);
        string fileName = Path.GetFileName(path);

        IReadOnlyDictionary<string, ConfigApiFileEntry?> entries = api.ToDictionary(
            kvp => kvp.Key,
            kvp =>
                kvp.Value is null
                    ? null
                    : new ConfigApiFileEntry
                    {
                        Key = kvp.Key,
                        Id = kvp.Value.Id,
                        HasRequest = kvp.Value.Request is not null,
                    }
        );

        _normalization.ValidateApiFileEntries(fileName, entries);

        return api;
    }

    private static Dictionary<string, FetchItem> LoadFetchFile(string configDirectory)
    {
        Dictionary<string, FetchItem> fetch = LoadFile<Dictionary<string, FetchItem>>(
            configDirectory,
            "Fetch.json"
        );

        List<ConfigFetchEntry> entries = fetch
            .Select(kvp => new ConfigFetchEntry
            {
                Key = kvp.Key,
                CountRaw = kvp.Value.Count,
                ApiRaw = kvp.Value.Api,
            })
            .ToList();

        _normalization.NormalizeFetch(entries);

        foreach (ConfigFetchEntry entry in entries)
        {
            if (!fetch.TryGetValue(entry.Key, out FetchItem? value))
                continue;

            value.Count = entry.Count;
            value.Api = entry.Api;
        }

        return fetch;
    }

    private static void NormalizeApi(IReadOnlyDictionary<string, ApiConfig> api)
    {
        List<ConfigApiEntry> entries = api
            .Select(kvp => ToConfigApiEntry(kvp.Key, kvp.Value))
            .ToList();

        _normalization.ValidateAndNormalizeApi(entries);
        ApplyConfigApiEntries(api, entries);
    }

    private static void ApplyFetchToApi(
        IReadOnlyDictionary<string, ApiConfig> api,
        IReadOnlyDictionary<string, FetchItem> fetch
    )
    {
        List<ConfigApiEntry> apiEntries = api
            .Select(kvp => ToConfigApiEntry(kvp.Key, kvp.Value))
            .ToList();

        List<ConfigFetchEntry> fetchEntries = fetch
            .Select(kvp => new ConfigFetchEntry
            {
                Key = kvp.Key,
                CountRaw = kvp.Value.Count,
                ApiRaw = kvp.Value.Api,
                Count = kvp.Value.Count,
                Api = kvp.Value.Api,
            })
            .ToList();

        _normalization.ApplyFetchToApi(apiEntries, fetchEntries);
        ApplyConfigApiEntries(api, apiEntries);
    }

    private static ConfigApiEntry ToConfigApiEntry(string key, ApiConfig value) =>
        new()
        {
            Key = key,
            Id = value.Id,
            Url = value.Request?.Url ?? string.Empty,
            Variables = value.Request?.Query?.Variables ?? [],
            Features = value.Request?.Query?.Features,
            FieldToggles = value.Request?.Query?.FieldToggles,
            Headers = value.Request?.Headers,
        };

    private static void ApplyConfigApiEntries(
        IReadOnlyDictionary<string, ApiConfig> api,
        IReadOnlyList<ConfigApiEntry> entries
    )
    {
        foreach (ConfigApiEntry entry in entries)
        {
            if (!api.TryGetValue(entry.Key, out ApiConfig? config))
                continue;

            ApiRequest request = config.Request;

            request.Url = entry.Url;
            request.Query.Variables = entry.Variables;
            request.Query.Features = entry.Features ?? [];
            request.Query.FieldToggles = entry.FieldToggles ?? [];
            request.Headers = entry.Headers ?? [];
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

        T? value = config.Get<T>();
        return _deserializationGuard.RequireConfig(value, fileName);
    }

}
