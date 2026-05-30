using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaFilter
{
    public Task Check(List<Download> downloads);
}
