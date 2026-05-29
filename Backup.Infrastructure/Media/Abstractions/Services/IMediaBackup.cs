using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaBackup
{
    public string? Id { get; set; }
    public Task Backup(List<Download> downloads, IMediaData mediaData);
}

