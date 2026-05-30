using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDataMaintenance
{
    public string? Id { get; set; }
    public Task CheckData(List<Download> downloads);
    public Task Prune(List<Download> downloads);
    public Task CheckIntegrity(List<Download> downloads);
}
