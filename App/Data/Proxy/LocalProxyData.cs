using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Proxy;
using Backup.App.Interfaces.Partition;
using Newtonsoft.Json;

namespace Backup.App.Data.Proxy;

public class LocalProxyData(Models.Config.App _config, IPartition _partition) : IProxyData, ISetup
{
    private readonly Models.Config.App _config = _config;
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
        Models.Config.Data.Partition partition = _partition
            .GetPartitions(_config.Debug.Partitions)
            .First();

        return Path.Combine(
            [.. partition.Paths, .. _config.Proxy.Data.Paths, .. _config.Proxy.Data.Proxy.Paths]
        );
    }

    private string GetPathFile() =>
        Path.Combine(
            GetPath(),
            _config.Proxy.Data.Proxy.File ?? throw new Exception("file not configured")
        );

    public async Task<List<Models.Proxy.Data>?> GetAll()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        List<Models.Proxy.Data>? datas =
            JsonConvert.DeserializeObject<List<Models.Proxy.Data>>(content)
            ?? throw new Exception("Error deserializing the file.");

        return datas;
    }

    public async Task<Dictionary<Models.Proxy.Proxy, Models.Proxy.Data>?> GetAllAsDictionary()
    {
        List<Models.Proxy.Data>? datas = await GetAll();

        return datas?.ToDictionary(post => post.Proxy);
    }

    public async Task Save(List<Models.Proxy.Data> data)
    {
        string json = JsonConvert.SerializeObject(data);
        string path = GetPathFile();

        await File.WriteAllTextAsync(path, json);
        await SaveFormatted(data);
    }

    private async Task SaveFormatted(List<Models.Proxy.Data> data)
    {
        string path = Utils.Path.GetPathFormatted(GetPathFile());
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

        await File.WriteAllTextAsync(path, json);
    }
}
