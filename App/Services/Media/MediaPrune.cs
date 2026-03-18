using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Microsoft.AspNetCore.WebUtilities;

namespace Backup.App.Services.Media;

public class MediaPrune(Models.Config.App _config) : IMediaPrune
{
    private readonly Utils.MediaFilter _filter = new(_config.Downloads.Prune.Filters);

    public Task Prune(List<Download> downloads)
    {
        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                Uri uri = new(data.Url);

                string extension = Path.GetExtension(uri.AbsolutePath);

                if (string.IsNullOrEmpty(extension))
                    extension = Path.GetExtension(data.Path);

                extension = extension.Trim('.');

                var query = QueryHelpers.ParseQuery(uri.Query);

                string format = query.GetValue("format");
                string name = query.GetValue("name");

                return !_filter.IsExcluded(extension, format, name);
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        return Task.CompletedTask;
    }
}
