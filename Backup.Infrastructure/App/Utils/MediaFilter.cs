using Backup.App.Models.Utils;

namespace Backup.App.Utils;

public class MediaFilter(List<string>? _rawFilters)
{
    private readonly HashSet<DataFilter> _filters = Parse(_rawFilters);

    private static HashSet<DataFilter> Parse(List<string>? filters)
    {
        if (filters is null)
            return [];

        return filters
            .Select(e => e.Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Where(e => e.Length == 3)
            .Select(e => new DataFilter(
                e[0].ToLowerInvariant(),
                e[1].ToLowerInvariant(),
                e[2].ToLowerInvariant()
            ))
            .ToHashSet();
    }

    public bool IsExcluded(string extension, string formatType, string resolutionName)
    {
        foreach (DataFilter ex in _filters)
        {
            bool matches =
                (ex.Extension == "*" || ex.Extension == extension)
                && (ex.FormatType == "*" || ex.FormatType == formatType)
                && (ex.ResolutionName == "*" || ex.ResolutionName == resolutionName);

            if (matches)
                return true;
        }

        return false;
    }
}
