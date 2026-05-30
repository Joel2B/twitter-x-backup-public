using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Bulk;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Bulk;

public sealed class BulkPhase2Runner(
    ILogger<BulkPhase2Runner> logger,
    AppConfig config,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkSourceRouteProvider bulkSourceRouteProvider,
    IBulkApiClient bulkApiClient
) : IBulkPhase2Runner
{
    private readonly ILogger<BulkPhase2Runner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkSourceRouteProvider _bulkSourceRouteProvider = bulkSourceRouteProvider;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running phase 2");
        List<BulkData>? data = await _bulkData.GetBulks();

        if (data is null)
            return;

        List<BulkData> bulks = data.Where(o =>
                o.User.Status == StatusUser.Active && o.Order.Phase2 is not null
            )
            .Take(_config.Bulk.UsersPerPhase2)
            .ToList();

        string? origin = _bulkSourceRouteProvider.GetOrigin(SourceType.Media);

        if (origin is null)
        {
            _logger.LogInformation("origin is null");
            return;
        }

        int progress = 1;

        foreach (BulkData bulk in bulks)
        {
            _logger.LogInformation(
                "progress: {progress}/{total}",
                progress,
                _config.Bulk.UsersPerPhase2
            );

            _logger.LogInformation(
                "user id: {id} user name: {name}, media count: {count}",
                bulk.User.Id,
                bulk.User.Name,
                bulk.Total
            );

            if (bulk.Order.Phase2 == 0)
                bulk.Cursor = null;

            bool valid = await _bulkApiClient.Verify();

            if (!valid)
            {
                _logger.LogInformation("downloader is not valid");
                break;
            }

            ParseResult? result = await _bulkApiClient.GetUserMedia(
                api,
                bulk.User.Id ?? throw new Exception(),
                origin,
                _config.Bulk.MediaPerApi,
                bulk.Cursor,
                cancellationToken
            );

            if (result is null)
            {
                bulk.User.Status = StatusUser.Inactive;
                _logger.LogInformation("error in GetUserMedia in the id {id}", bulk.User.Id);
                continue;
            }

            if (result.Posts.Count == 0)
            {
                bulk.Order.Phase2 = null;
                continue;
            }

            int? mediaCount = result.Posts[0].Profile.Count?.Media;

            if (mediaCount is null || bulk.Total is null)
            {
                _logger.LogWarning("no media");
                continue;
            }

            if (mediaCount <= bulk.Total)
            {
                bulk.Order.Phase2 = null;
                continue;
            }

            int diff = (int)mediaCount - (int)bulk.Total;
            int index = 0;
            int count = 0;

            while (true)
            {
                _logger.LogInformation("index: {index}, count: {count}", index, count);

                valid = await _bulkApiClient.Verify();

                if (!valid)
                {
                    _logger.LogInformation("downloader is not valid");
                    break;
                }

                int attempt = 0;

                while (result is null && attempt < _config.Bulk.ApiRetryCount)
                {
                    result = await _bulkApiClient.GetUserMedia(
                        api,
                        bulk.User.Id ?? throw new Exception(),
                        origin,
                        _config.Bulk.MediaPerApi,
                        bulk.Cursor,
                        cancellationToken
                    );

                    if (result is not null)
                        break;

                    attempt++;

                    _logger.LogWarning("attempt: {attempt}", attempt);
                }

                if (result is null)
                {
                    bulk.User.Status = StatusUser.Inactive;
                    _logger.LogInformation("error in GetUserMedia in the id {id}", bulk.User.Id);
                    break;
                }

                _logger.LogInformation("ParseResult return {count} posts", result.Posts.Count);
                await _postData.AddPosts(bulk.User.Id, origin, result.Posts);

                index++;
                count += result.Posts.Count;

                if (
                    count >= diff
                    || result.Posts.Count == 0
                    || result.NextCursor is null
                    || count >= _config.Bulk.MaxCountPostPhase2
                )
                    break;

                bulk.Cursor = result.NextCursor;
                result = null;
            }

            bulk.Order.Phase2 = null;
            bulk.Total = mediaCount;

            if (progress % _config.Bulk.SavePerAction == 0)
            {
                await _postData.Save();
                await _bulkData.Save(data);
            }

            progress++;
        }

        await _postData.Save();
        await _bulkData.Save(data);
    }
}
