namespace Backup.Application.Dump;

public interface IDumpPathService
{
    string BuildDumpRootPath(
        IReadOnlyList<string> partitionPaths,
        IReadOnlyList<string> storagePaths,
        IReadOnlyList<string> dumpPaths
    );
    string BuildCurrentUserPath(string dumpRootPath, string currentSession, string userId);
    string BuildDataFilePath(string currentUserPath, string fileName);
    string BuildIndexPath(string currentUserPath, int index);
    string BuildApiPath(string indexPath, IReadOnlyList<string> apiPathSegments);
    string BuildIndexFileName(int indexFile);
}
