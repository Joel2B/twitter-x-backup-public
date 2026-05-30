using Backup.Application.Media.Filter;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaFilter(
    IMediaLogger _mediaLogger,
    IMediaErrorFilterPolicyService mediaErrorFilterPolicyService
) : IMediaFilter
{
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IMediaErrorFilterPolicyService _mediaErrorFilterPolicyService =
        mediaErrorFilterPolicyService;

    public async Task Check(List<Download> downloads)
    {
        List<Logs> logs = await _mediaLogger.GetErrors() ?? [];

        HashSet<string> ids = logs.SelectMany(log => log.Messages)
            .Where(msg => _mediaErrorFilterPolicyService.ShouldExclude(msg.Message))
            .Select(msg => msg.Id)
            .ToHashSet();

        foreach (Download download in downloads)
            download.Data.RemoveAll(data => ids.Contains(data.Url));

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }
}
