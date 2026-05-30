namespace Backup.Application.Media.Filter;

public interface IMediaErrorFilterPolicyService
{
    bool ShouldExclude(string? message);
}
