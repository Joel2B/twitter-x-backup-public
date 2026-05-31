using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathAnalysisService : IMediaBackupPathAnalysisService
{
    public IReadOnlyList<MediaPathDuplicateGroup> FindDuplicates(IEnumerable<string> paths) =>
        paths.GroupBy(path => path)
            .Where(group => group.Count() > 1)
            .Select(group => new MediaPathDuplicateGroup
            {
                Path = group.Key,
                Count = group.Count(),
                Entries = group.ToList(),
            })
            .ToList();

    public MediaPathDiffResult Diff(IEnumerable<string> expectedPaths, IEnumerable<string> actualPaths)
    {
        List<string> expected = expectedPaths.ToList();
        List<string> actual = actualPaths.ToList();

        return new MediaPathDiffResult
        {
            MissingPaths = expected.Except(actual).ToList(),
            ExtraPaths = actual.Except(expected).ToList(),
        };
    }
}
