using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Logging;

namespace Backup.App.Services.Media;

public class MediaFilter(IMediaLogger _mediaLogger) : IMediaFilter
{
    private readonly IMediaLogger _mediaLogger = _mediaLogger;

    public async Task Check(List<Download> downloads)
    {
        List<Logs> logs = await _mediaLogger.GetErrors() ?? [];

        HashSet<string> ids = logs.SelectMany(log => log.Messages)
            .Where(msg => msg.Message == "NotFound" || msg.Message == "Forbidden")
            .Select(msg => msg.Id)
            .ToHashSet();

        foreach (Download download in downloads)
            download.Data.RemoveAll(data => ids.Contains(data.Url));

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }
}
