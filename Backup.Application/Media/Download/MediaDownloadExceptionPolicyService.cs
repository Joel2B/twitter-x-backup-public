namespace Backup.Application.Media;

public sealed class MediaDownloadExceptionPolicyService : IMediaDownloadExceptionPolicyService
{
    public bool ShouldRetryWithNextProxy(Exception ex) =>
        ex is TaskCanceledException || ex is HttpRequestException;
}
