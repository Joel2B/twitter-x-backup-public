using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaDataMaintenance
{
    public Task CheckData(List<Download> downloads);
    public Task Prune(List<Download> downloads);
    public Task CheckIntegrity(List<Download> downloads);
}
