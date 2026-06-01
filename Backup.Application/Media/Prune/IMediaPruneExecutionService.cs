using Backup.Application.Media.Models;

namespace Backup.Application.Media.Prune;

public interface IMediaPruneExecutionService
{
    IReadOnlyList<MediaDownload> Execute(IReadOnlyList<MediaDownload> downloads);
}
