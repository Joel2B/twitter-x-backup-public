using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaFilter
{
    public Task Check(List<Download> downloads);
}
