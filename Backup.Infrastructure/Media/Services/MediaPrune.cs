using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Media;
using Microsoft.AspNetCore.WebUtilities;

namespace Backup.Infrastructure.Services.Media;

public class MediaPrune(AppConfig _config) : IMediaPrune
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

                string format = query.TryGetValue("format", out var formatValue)
                    ? formatValue.ToString()
                    : string.Empty;
                string name = query.TryGetValue("name", out var nameValue)
                    ? nameValue.ToString()
                    : string.Empty;

                return !_filter.IsExcluded(extension, format, name);
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        return Task.CompletedTask;
    }
}
