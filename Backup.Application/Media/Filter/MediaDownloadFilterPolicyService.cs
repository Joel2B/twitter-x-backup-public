namespace Backup.Application.Media.Filter;

public sealed class MediaDownloadFilterPolicyService : IMediaDownloadFilterPolicyService
{
    public IReadOnlyList<MediaExclusionRule> Parse(IReadOnlyCollection<string>? filters)
    {
        if (filters is null || filters.Count == 0)
            return [];

        return
        [
            .. filters
                .Select(value => value.Split(':', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 3)
                .Select(parts => new MediaExclusionRule(
                    parts[0].ToLowerInvariant(),
                    parts[1].ToLowerInvariant(),
                    parts[2].ToLowerInvariant()
                )),
        ];
    }

    public bool IsExcluded(
        IReadOnlyCollection<MediaExclusionRule> filters,
        string extension,
        string formatType,
        string resolutionName
    )
    {
        string normalizedExtension = extension.ToLowerInvariant();
        string normalizedFormatType = formatType.ToLowerInvariant();
        string normalizedResolutionName = resolutionName.ToLowerInvariant();

        foreach (MediaExclusionRule filter in filters)
        {
            bool matches =
                (filter.Extension == "*" || filter.Extension == normalizedExtension)
                && (filter.FormatType == "*" || filter.FormatType == normalizedFormatType)
                && (
                    filter.ResolutionName == "*"
                    || filter.ResolutionName == normalizedResolutionName
                );

            if (matches)
                return true;
        }

        return false;
    }
}
