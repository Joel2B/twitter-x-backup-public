using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaFilter
{
    public Task Check(List<Download> downloads, CancellationToken cancellationToken = default);
}
