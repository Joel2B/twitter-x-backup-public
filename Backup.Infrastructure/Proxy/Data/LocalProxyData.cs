using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Models;
using Backup.Infrastructure.Utils;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Proxy.Data;

public class LocalProxyData(
    AppConfig _config,
    IPartition _partition,
    IDataStoreGuardService dataStoreGuardService
) : IProxyData, ISetup
{
    private readonly AppConfig _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private void SetupDirectory()
    {
        Directory.CreateDirectory(GetPath());
    }

    private string GetPath()
    {
        PartitionConfig partition = _partition.GetPrimary();

        return Path.Combine(
            [.. partition.Paths, .. _config.Proxy.Data.Paths, .. _config.Proxy.Data.Proxy.Paths]
        );
    }

    private string GetPathFile() =>
        Path.Combine(
            GetPath(),
            _dataStoreGuardService.RequireConfiguredFileName(_config.Proxy.Data.Proxy.File)
        );

    public async Task<List<ProxyData>?> GetAll()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        List<ProxyData>? deserialized = JsonConvert.DeserializeObject<List<ProxyData>>(content);
        List<ProxyData> datas = _dataStoreGuardService.RequireDeserialized(
            deserialized,
            "Error deserializing the file."
        );

        return datas;
    }

    public async Task<Dictionary<ProxyDataConfig, ProxyData>?> GetAllAsDictionary()
    {
        List<ProxyData>? datas = await GetAll();

        return datas?.ToDictionary(post => post.Proxy);
    }

    public async Task Save(List<ProxyData> data)
    {
        string json = JsonConvert.SerializeObject(data);
        string path = GetPathFile();

        await File.WriteAllTextAsync(path, json);
        await SaveFormatted(data);
    }

    private async Task SaveFormatted(List<ProxyData> data)
    {
        string path = UtilsPath.GetPathFormatted(GetPathFile());
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

        await File.WriteAllTextAsync(path, json);
    }
}
