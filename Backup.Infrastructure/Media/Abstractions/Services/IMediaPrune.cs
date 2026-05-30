using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaPrune
{
    public Task Prune(List<Download> downloads);
}
