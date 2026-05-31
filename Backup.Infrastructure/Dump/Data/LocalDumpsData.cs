using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Dump;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpsData(StorageDump _config, IPartition _partition, IDataStoreGuardService dataStoreGuardService) : IDumpsDataStore, ISetup
{
    public bool IsDefault { get; set; }
    private readonly StorageDump _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

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
        foreach (PartitionConfig partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPath(partition));
    }

    private string GetPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);

    private string GetPathFile(PartitionConfig? partition = null)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(_config.Paths.Dumps.File);

        PartitionConfig primary = partition ?? _partition.GetPrimary();
        string path = Path.Combine(GetPath(primary), fileName);

        return path;
    }

    public async Task<DumpsData> GetData()
    {
        string path = GetPathFile();

        _dataStoreGuardService.EnsureFileExists(path);

        string content = await File.ReadAllTextAsync(path);
        DumpsData? deserialized = JsonConvert.DeserializeObject<DumpsData>(content);
        DumpsData data = _dataStoreGuardService.RequireDeserialized(deserialized, "Error in deserialize");

        return data;
    }

    public async Task Save(DumpsData dumps)
    {
        string content = JsonConvert.SerializeObject(dumps);
        string path = GetPathFile();

        await File.WriteAllTextAsync(path, content);
    }
}
