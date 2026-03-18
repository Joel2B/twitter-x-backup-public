using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaProcessingLogger
{
    public Task Save(string type, List<Download> downloads);
    public Task Prune();
}
