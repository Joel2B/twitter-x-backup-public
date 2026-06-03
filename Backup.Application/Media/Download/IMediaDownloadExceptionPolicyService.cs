namespace Backup.Application.Media;

public interface IMediaDownloadExceptionPolicyService
{
    bool ShouldRetryWithNextProxy(Exception ex);
}
