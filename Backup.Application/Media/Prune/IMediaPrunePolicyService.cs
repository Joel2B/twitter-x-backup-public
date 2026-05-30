namespace Backup.Application.Media.Prune;

public interface IMediaPrunePolicyService
{
    bool ShouldKeep(string extension, string formatType, string resolutionName);
}
