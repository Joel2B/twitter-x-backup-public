using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaIntegrity
{
    public Task Check(List<Download> downloads, IMediaDataMaintenance data);
}
