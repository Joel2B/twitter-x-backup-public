using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaPrune
{
    public Task Prune(List<Download> downloads);
}
