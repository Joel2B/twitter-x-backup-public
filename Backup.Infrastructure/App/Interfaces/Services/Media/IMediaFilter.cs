using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaFilter
{
    public Task Check(List<Download> downloads);
}
