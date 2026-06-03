using Backup.Application.Bulk.Models;
using Backup.Application.IO;

namespace Backup.Application.Bulk;

public sealed class BulkDatedPathExtractionService : IBulkDatedPathExtractionService
{
    public IReadOnlyList<DatedPath> Extract(IEnumerable<string> paths)
    {
        List<DatedPath> result = [];

        foreach (string path in paths)
        {
            DateTime? date = PathFormattingPolicy.ParseTimestampFromPath(path);
            if (date is null)
                continue;

            result.Add(new DatedPath(path, date.Value));
        }

        return result;
    }
}
