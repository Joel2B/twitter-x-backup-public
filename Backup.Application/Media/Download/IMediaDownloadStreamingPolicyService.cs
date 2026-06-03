using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public interface IMediaDownloadStreamingPolicyService
{
    MediaDownloadStreamingSettings GetSettings();
    string BuildNoDataTimeoutMessage(int timeoutMilliseconds);
}
