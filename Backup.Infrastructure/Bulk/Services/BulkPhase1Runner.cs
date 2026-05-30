using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Bulk;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Bulk;

public sealed class BulkPhase1Runner(
    ILogger<BulkPhase1Runner> logger,
    AppConfig config,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkSourceRouteProvider bulkSourceRouteProvider,
    IBulkApiClient bulkApiClient
) : IBulkPhase1Runner
{
    private readonly ILogger<BulkPhase1Runner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkSourceRouteProvider _bulkSourceRouteProvider = bulkSourceRouteProvider;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running phase 1");

        int postCount = await _postData.GetCount();
        _logger.LogInformation("post count: {count}", postCount);

        List<BulkData>? bulks = await _bulkData.GetBulks();

        if (bulks is null)
        {
            _logger.LogInformation("bulk data is null");
            return;
        }

        IQueryable<BulkData> query = bulks
            .Where(o => o.User.Status == StatusUser.Active && o.Order.Phase1 is not null)
            .OrderBy(o => o.Order.Phase1)
            .AsQueryable();

        List<BulkData> bulksLeft = query.ToList();

        _logger.LogInformation("bulks left: {count}", bulksLeft.Count);

        if (bulksLeft.Count > 0)
            _logger.LogInformation(
                "{names}",
                string.Join(", ", bulksLeft.Select(o => o.User.Name))
            );

        if (_config.Bulk.UsersPerCycle > 0)
            query = query.Take(_config.Bulk.UsersPerCycle);

        List<BulkData> bulksFiltered = query.ToList();

        string? origin = _bulkSourceRouteProvider.GetOrigin(SourceType.Media);

        if (origin is null)
        {
            _logger.LogInformation("origin is null");
            return;
        }

        int progress = 1;

        foreach (BulkData bulk in bulksFiltered)
        {
            if (bulk.User.Id is null)
                continue;

            _logger.LogInformation(
                "progress: {progress}/{total}",
                progress,
                _config.Bulk.UsersPerCycle
            );

            _logger.LogInformation(
                "user id: {id} user name: {name}, media count: {count}",
                bulk.User.Id,
                bulk.User.Name,
                bulk.Total
            );

            int index = 0;
            int count = 0;

            while (_config.Bulk.ApiPerCycle <= 0 || index < _config.Bulk.ApiPerCycle)
            {
                _logger.LogInformation("index: {index}, count: {count}", index, count);

                bool valid = await _bulkApiClient.Verify();

                if (!valid)
                {
                    _logger.LogInformation("downloader is not valid");
                    break;
                }

                ParseResult? result = null;
                int attempt = 0;

                while (attempt < _config.Bulk.ApiRetryCount)
                {
                    result = await _bulkApiClient.GetUserMedia(
                        api,
                        bulk.User.Id,
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

                if (result.Posts.Count == 0 || result.NextCursor is null)
                {
                    bulk.Order.Phase1 = null;
                    bulk.Cursor = null;
                }
                else
                    bulk.Order.Phase1++;

                bulk.Cursor = result.NextCursor;

                index++;
                count += result.Posts.Count;

                _logger.LogInformation("cursor: {cursor}", bulk.Cursor);

                if (bulk.Order.Phase1 is null || count >= _config.Bulk.MaxCountPost)
                    break;
            }

            if (progress % _config.Bulk.SavePerAction == 0)
            {
                _logger.LogInformation("saving posts");
                await _postData.Save();

                _logger.LogInformation("saving bulks");
                await _bulkData.Save(bulks);
            }

            progress++;
        }

        _logger.LogInformation("saving posts");
        await _postData.Save();

        _logger.LogInformation("saving bulks");
        await _bulkData.Save(bulks);
    }
}
