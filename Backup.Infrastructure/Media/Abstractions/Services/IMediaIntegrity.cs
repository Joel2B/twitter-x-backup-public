using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaIntegrity
{
    public Task Check(List<Download> downloads, IMediaDataMaintenance data);
}

