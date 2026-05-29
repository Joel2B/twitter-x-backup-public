using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Utils;

namespace Backup.App.Services.Media;

public class MediaIntegrity : IMediaIntegrity
{
    public async Task Check(List<Download> downloads, IMediaData data)
    {
        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                string extension = Path.GetExtension(data.Path);

                return extension != FileExtension.JPG
                    && extension != FileExtension.PNG
                    && extension != FileExtension.MP4;
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);
        await data.CheckIntegrity(downloads);
    }
}
