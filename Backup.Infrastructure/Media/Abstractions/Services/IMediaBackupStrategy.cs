using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupStrategy
{
    public string? Id { get; set; }
    public Task Backup(
        List<Download> downloads,
        IMediaStorage mediaData,
        CancellationToken cancellationToken = default
    );
}
