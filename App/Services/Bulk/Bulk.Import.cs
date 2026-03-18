using Backup.App.Models.Bulk;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService
{
    private async Task Import()
    {
        _logger.LogInformation("running import");
        _logger.LogInformation("getting sources");
        List<Source> sources = await _bulkSourceData.GetSources();

        _logger.LogInformation("getting bulks");
        List<Models.Bulk.Bulk> bulks = await _bulkData.GetBulks() ?? [];

        _logger.LogInformation("filtering sources");
        sources.RemoveAll(source =>
            source.Type is not SourceType.Media || bulks.Any(o => o.User.Name == source.UserName)
        );

        if (_config.Bulk.UsersPerCycle > 0)
            sources = sources.Take(_config.Bulk.UsersPerCycle).ToList();

        int progress = 1;

        foreach (Source source in sources)
        {
            _logger.LogInformation(
                "progress: {progress}/{total}",
                progress,
                _config.Bulk.UsersPerCycle
            );

            _logger.LogInformation("import user: {user}", source.UserName);

            Models.Bulk.Bulk bulk = new()
            {
                User = new() { Name = source.UserName, Status = StatusUser.None },
            };

            bool valid = await _downloader.Verify();

            if (!valid)
            {
                _logger.LogInformation("downloader is not valid");
                break;
            }

            ParseUser? result = await GetUserByUser(source.UserName);

            if (result is null)
            {
                _logger.LogInformation("error in GetUserByUser");
                continue;
            }

            if (result?.User is not null)
            {
                bulk.User.Id = result.User.Id;
                bulk.User.Status = StatusUser.Active;
                bulk.Total = result.User.MediaCount;
            }

            bulks.Add(bulk);
            progress++;
        }

        await _bulkData.Save(bulks);
    }
}
