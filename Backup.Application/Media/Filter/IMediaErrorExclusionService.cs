namespace Backup.Application.Media.Filter;

public interface IMediaErrorExclusionService
{
    IReadOnlySet<string> GetExcludedIds(IEnumerable<MediaErrorMessage> messages);
}
