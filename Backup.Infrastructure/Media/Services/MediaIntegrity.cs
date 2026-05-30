using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Models.Utils;

namespace Backup.Infrastructure.Services.Media;

public class MediaIntegrity : IMediaIntegrity
{
    public async Task Check(List<Download> downloads, IMediaDataMaintenance data)
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

