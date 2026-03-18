using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaBackup
{
    public string? Id { get; set; }
    public Task Backup(List<Download> downloads);
}
