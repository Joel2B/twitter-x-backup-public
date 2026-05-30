using Backup.Application.Media.Models;

namespace Backup.Application.Media.Integrity;

public interface IMediaIntegrityPolicyService
{
    void KeepSupported(List<MediaDownload> downloads);
}
