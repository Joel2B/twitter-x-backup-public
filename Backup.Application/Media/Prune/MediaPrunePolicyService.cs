namespace Backup.Application.Media.Prune;

public sealed class MediaPrunePolicyService(List<string>? rawFilters) : IMediaPrunePolicyService
{
    private readonly HashSet<PruneFilter> _filters = Parse(rawFilters);

    public bool ShouldKeep(string extension, string formatType, string resolutionName) =>
        IsExcluded(extension, formatType, resolutionName);

    private bool IsExcluded(string extension, string formatType, string resolutionName)
    {
        string normalizedExtension = extension.ToLowerInvariant();
        string normalizedFormat = formatType.ToLowerInvariant();
        string normalizedResolution = resolutionName.ToLowerInvariant();

        foreach (PruneFilter filter in _filters)
        {
            bool matches =
                (filter.Extension == "*" || filter.Extension == normalizedExtension)
                && (filter.FormatType == "*" || filter.FormatType == normalizedFormat)
                && (filter.ResolutionName == "*" || filter.ResolutionName == normalizedResolution);

            if (matches)
                return true;
        }

        return false;
    }

    private static HashSet<PruneFilter> Parse(List<string>? filters)
    {
        if (filters is null)
            return [];

        return filters
            .Select(item => item.Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 3)
            .Select(parts =>
                new PruneFilter(
                    parts[0].ToLowerInvariant(),
                    parts[1].ToLowerInvariant(),
                    parts[2].ToLowerInvariant()
                )
            )
            .ToHashSet();
    }

    private sealed record PruneFilter(string Extension, string FormatType, string ResolutionName);
}
