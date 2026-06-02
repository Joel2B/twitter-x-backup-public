using Backup.Application.IO;
using Backup.Infrastructure.Models.Dump;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpsDataPersistenceCoordinator(
    IDataStoreGuardService dataStoreGuardService,
    LocalDumpsDataPathLayout pathLayout
)
{
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly LocalDumpsDataPathLayout _pathLayout = pathLayout;

    public async Task EnsureInitialized(CancellationToken cancellationToken = default)
    {
        string path = _pathLayout.GetFilePath();

        if (File.Exists(path))
            return;

        _pathLayout.EnsureDirectories();

        string content = JsonConvert.SerializeObject(new DumpsData());
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public async Task<DumpsData> Read(CancellationToken cancellationToken = default)
    {
        string path = _pathLayout.GetFilePath();

        _dataStoreGuardService.EnsureFileExists(path);

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        DumpsData? deserialized = JsonConvert.DeserializeObject<DumpsData>(content);

        return _dataStoreGuardService.RequireDeserialized(deserialized, "Error in deserialize");
    }

    public async Task Write(DumpsData dumps, CancellationToken cancellationToken = default)
    {
        string content = JsonConvert.SerializeObject(dumps);
        string path = _pathLayout.GetFilePath();

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
