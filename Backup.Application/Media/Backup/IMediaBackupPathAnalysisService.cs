using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPathAnalysisService
{
    IReadOnlyList<MediaPathDuplicateGroup> FindDuplicates(IEnumerable<string> paths);

    MediaPathDiffResult Diff(IEnumerable<string> expectedPaths, IEnumerable<string> actualPaths);
}
