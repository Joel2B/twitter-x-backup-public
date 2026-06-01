using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDownloadModelMapper
{
    List<MediaDownload> ToApplication(IEnumerable<Download> downloads);
    List<Download> ToInfrastructure(IEnumerable<MediaDownload> downloads);
}
