using Backup.Application.Media.Filter;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaFilter(
    IMediaLogger _mediaLogger,
    IMediaErrorExclusionService mediaErrorExclusionService
) : IMediaFilter
{
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IMediaErrorExclusionService _mediaErrorExclusionService =
        mediaErrorExclusionService;

    public async Task Check(List<Download> downloads)
    {
        List<Logs> logs = await _mediaLogger.GetErrors() ?? [];

        IReadOnlySet<string> ids = _mediaErrorExclusionService.GetExcludedIds(
            logs.SelectMany(log => log.Messages)
                .Select(message => new MediaErrorMessage { Id = message.Id, Message = message.Message })
        );

        foreach (Download download in downloads)
            download.Data.RemoveAll(data => ids.Contains(data.Url));

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }
}
