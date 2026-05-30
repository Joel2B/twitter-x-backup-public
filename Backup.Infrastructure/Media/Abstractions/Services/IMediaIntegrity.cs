using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaIntegrity
{
    public Task Check(List<Download> downloads, IMediaDataMaintenance data);
}
