using System.Net;

namespace Backup.Application.Media;

public sealed class MediaDownloadContentValidationPolicyService
    : IMediaDownloadContentValidationPolicyService
{
    public void EnsureSuccessStatusCode(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.OK)
            return;

        throw new SystemException(statusCode.ToString());
    }

    public void EnsureReadable(Stream content)
    {
        if (content.CanRead)
            return;

        throw new SystemException("content is empty.");
    }

    public void EnsureNotEmpty(long streamLength)
    {
        if (streamLength > 0)
            return;

        throw new SystemException("content is empty.");
    }

    public void EnsureComplete(long? expectedContentLength, long streamLength)
    {
        if (expectedContentLength is null)
            return;

        if (streamLength == expectedContentLength.Value)
            return;

        throw new SystemException("incomplete download");
    }
}
