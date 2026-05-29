using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaIntegrity
{
    public Task Check(List<Download> downloads, IMediaData data);
}
