namespace Backup.Application.Media.Filter;

public interface IMediaDownloadFilterPolicyService
{
    IReadOnlyList<MediaExclusionRule> Parse(IReadOnlyCollection<string>? filters);
    bool IsExcluded(
        IReadOnlyCollection<MediaExclusionRule> filters,
        string extension,
        string formatType,
        string resolutionName
    );
}
