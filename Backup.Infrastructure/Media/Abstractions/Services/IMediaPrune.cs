using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaPrune
{
    public Task Prune(List<Download> downloads, CancellationToken cancellationToken = default);
}
