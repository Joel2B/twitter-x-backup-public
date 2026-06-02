using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Dump.Abstractions.Services;

public interface IDumpPersistenceIOService
{
    Task<DumpData?> ReadDumpData(string path, CancellationToken cancellationToken = default);
    Task WriteDumpData(string path, DumpData dumpData, CancellationToken cancellationToken = default);
    Task WritePostsIndex(string path, IReadOnlyList<Post> posts, CancellationToken cancellationToken = default);
    Task WriteApiResponse(string path, string response, CancellationToken cancellationToken = default);
    IReadOnlyList<string> EnumerateJsonFiles(string rootPath);
    void CopyDirectory(string sourcePath, string targetPath);
}
