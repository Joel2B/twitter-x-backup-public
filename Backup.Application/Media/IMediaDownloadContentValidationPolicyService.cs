using System.Net;

namespace Backup.Application.Media;

public interface IMediaDownloadContentValidationPolicyService
{
    void EnsureSuccessStatusCode(HttpStatusCode statusCode);
    void EnsureReadable(Stream content);
    void EnsureNotEmpty(long streamLength);
    void EnsureComplete(long? expectedContentLength, long streamLength);
}
