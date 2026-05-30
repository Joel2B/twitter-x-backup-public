using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Interfaces.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Proxy;
using Backup.Infrastructure.Utils;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Data.Proxy;

public class LocalProxyData(AppConfig _config, IPartition _partition) : IProxyData, ISetup
{
    private readonly AppConfig _config = _config;
    private readonly IPartition _partition = _partition;

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
        PartitionConfig partition = _partition.GetPartitions(_config.Debug.Partitions).First();

        return Path.Combine(
            [.. partition.Paths, .. _config.Proxy.Data.Paths, .. _config.Proxy.Data.Proxy.Paths]
        );
    }

    private string GetPathFile() =>
        Path.Combine(
            GetPath(),
            _config.Proxy.Data.Proxy.File ?? throw new Exception("file not configured")
        );

    public async Task<List<ProxyData>?> GetAll()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        List<ProxyData>? datas =
            JsonConvert.DeserializeObject<List<ProxyData>>(content)
            ?? throw new Exception("Error deserializing the file.");

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
