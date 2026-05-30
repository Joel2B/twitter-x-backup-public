using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaBackupStrategy
{
    public string? Id { get; set; }
    public Task Backup(List<Download> downloads, IMediaStorage mediaData);
}
