using Backup.Application.Media.Models;

namespace Backup.Application.Media.Integrity;

public sealed class MediaIntegrityPolicyService : IMediaIntegrityPolicyService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".png", ".mp4" };

    public void KeepSupported(List<MediaDownload> downloads)
    {
        foreach (MediaDownload download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                string extension = Path.GetExtension(data.Path);
                return !SupportedExtensions.Contains(extension);
            });
        }

        downloads.RemoveAll(download => download.Data.Count == 0);
    }
}
