namespace Backup.Application.Dump;

public sealed class DumpPathService : IDumpPathService
{
    public string BuildDumpRootPath(
        IReadOnlyList<string> partitionPaths,
        IReadOnlyList<string> storagePaths,
        IReadOnlyList<string> dumpPaths
    ) => Path.Combine([.. partitionPaths, .. storagePaths, .. dumpPaths]);

    public string BuildCurrentUserPath(string dumpRootPath, string currentSession, string userId) =>
        Path.Combine(dumpRootPath, currentSession, userId);

    public string BuildDataFilePath(string currentUserPath, string fileName) =>
        Path.Combine(currentUserPath, fileName);

    public string BuildIndexPath(string currentUserPath, int index) =>
        Path.Combine(currentUserPath, index.ToString());

    public string BuildApiPath(string indexPath, IReadOnlyList<string> apiPathSegments) =>
        Path.Combine([indexPath, .. apiPathSegments]);

    public string BuildIndexFileName(int indexFile) => $"{indexFile}.json";
}
