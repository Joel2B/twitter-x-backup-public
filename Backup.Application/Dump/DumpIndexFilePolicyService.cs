using System.Text.RegularExpressions;

namespace Backup.Application.Dump;

public sealed partial class DumpIndexFilePolicyService : IDumpIndexFilePolicyService
{
    public IReadOnlyList<string> SelectIndexFiles(
        IEnumerable<string> paths,
        IReadOnlyList<string> apiPathParts
    )
    {
        List<string> selected = [];

        foreach (string path in paths)
        {
            string fileName = Path.GetFileName(path);
            string apiPath = Path.Combine([.. apiPathParts, fileName]);

            if (!IndexFileRegex().IsMatch(fileName))
                continue;

            if (path.Contains(apiPath, StringComparison.Ordinal))
                continue;

            selected.Add(path);
        }

        return selected;
    }

    [GeneratedRegex(@"^\d+")]
    private static partial Regex IndexFileRegex();
}
