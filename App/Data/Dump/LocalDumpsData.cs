using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data.Dump;
using Backup.App.Models.Dump;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

public class LocalDumpsData(Storage _config, IPartition _partition) : IDumpsData, ISetup
{
    private readonly Storage _config = _config;
    private readonly IPartition _partition = _partition;

    public async Task Setup()
    {
        string path = GetPathFile();

        if (File.Exists(path))
            return;

        SetupDirectory();

        DumpsData dumps = new() { };
        string content = JsonConvert.SerializeObject(dumps);

        await File.WriteAllTextAsync(path, content);
    }

    private void SetupDirectory()
    {
        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    private string GetPath(Models.Config.Data.Partition partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);

    private string GetPathFile(Models.Config.Data.Partition? partition = null)
    {
        if (_config.Paths.Dumps.File is null)
            throw new Exception("file not configured");

        Models.Config.Data.Partition primary = partition ?? _partition.GetPrimary();
        string path = Path.Combine(GetPath(primary), _config.Paths.Dumps.File);

        return path;
    }

    public async Task<DumpsData> GetData()
    {
        string path = GetPathFile();

        if (!File.Exists(path))
            throw new Exception("File doesn't exist");

        string content = await File.ReadAllTextAsync(path);
        DumpsData? data = JsonConvert.DeserializeObject<DumpsData>(content);

        if (data is null)
            throw new Exception("Error in deserialize");

        return data;
    }

    public async Task Save(DumpsData dumps)
    {
        string content = JsonConvert.SerializeObject(dumps);
        string path = GetPathFile();

        await File.WriteAllTextAsync(path, content);
    }
}
