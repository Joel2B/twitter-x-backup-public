using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Bulk;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;
using ParseUser = Backup.Domain.Posts.ParseUser;

namespace Backup.Infrastructure.Services.Bulk;

public sealed class BulkImportRunner(
    ILogger<BulkImportRunner> logger,
    AppConfig config,
    IBulkSourceData bulkSourceData,
    IBulkData bulkData,
    IBulkApiClient bulkApiClient
) : IBulkImportRunner
{
    private readonly ILogger<BulkImportRunner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IBulkSourceData _bulkSourceData = bulkSourceData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running import");
        _logger.LogInformation("getting sources");
        List<Source> sources = await _bulkSourceData.GetSources();

        _logger.LogInformation("getting bulks");
        List<BulkData> bulks = await _bulkData.GetBulks() ?? [];

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

            BulkData bulk = new()
            {
                User = new() { Name = source.UserName, Status = StatusUser.None },
            };

            bool valid = await _bulkApiClient.Verify();

            if (!valid)
            {
                _logger.LogInformation("downloader is not valid");
                break;
            }

            ParseUser? result = await _bulkApiClient.GetUserByUser(
                api,
                source.UserName,
                cancellationToken
            );

            if (result is null)
            {
                _logger.LogInformation("error in GetUserByUser");
                continue;
            }

            if (result.User is not null)
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
