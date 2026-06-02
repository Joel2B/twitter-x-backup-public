using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Utils;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Dump.Data;

public sealed class LocalDumpPersistenceIOService : IDumpPersistenceIOService
{
    public async Task<DumpData?> ReadDumpData(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        string content = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonConvert.DeserializeObject<DumpData>(content);
    }

    public async Task WriteDumpData(
        string path,
        DumpData dumpData,
        CancellationToken cancellationToken = default
    )
    {
        string content = JsonConvert.SerializeObject(dumpData);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public async Task WritePostsIndex(
        string path,
        IReadOnlyList<Post> posts,
        CancellationToken cancellationToken = default
    )
    {
        string json = JsonConvert.SerializeObject(posts);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public async Task WriteApiResponse(
        string path,
        string response,
        CancellationToken cancellationToken = default
    )
    {
        await File.WriteAllTextAsync(path, response, cancellationToken);
    }

    public IReadOnlyList<string> EnumerateJsonFiles(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories).ToList();

    public void CopyDirectory(string sourcePath, string targetPath) =>
        UtilsPath.CopyDirectory(sourcePath, targetPath);
}
