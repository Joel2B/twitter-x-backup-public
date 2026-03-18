using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaPrune
{
    public Task Prune(List<Download> downloads);
}
