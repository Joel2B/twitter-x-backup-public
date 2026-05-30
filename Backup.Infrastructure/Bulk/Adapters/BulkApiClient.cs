using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;
using ParseUser = Backup.Domain.Posts.ParseUser;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkApiClient(
    ILogger<BulkApiClient> logger,
    IBulkRequestFactory bulkRequestFactory,
    IBulkSourceRouteProvider bulkSourceRouteProvider,
    IPostDownloader downloader,
    IPostDomainParser parser
) : IBulkApiClient
{
    private readonly ILogger<BulkApiClient> _logger = logger;
    private readonly IBulkRequestFactory _bulkRequestFactory = bulkRequestFactory;
    private readonly IBulkSourceRouteProvider _bulkSourceRouteProvider = bulkSourceRouteProvider;
    private readonly IPostDownloader _downloader = downloader;
    private readonly IPostDomainParser _parser = parser;

    public async Task<bool> Verify() => await _downloader.Verify();

    public async Task<ParseUser?> GetUserByUser(
        IReadOnlyDictionary<string, ApiConfig> api,
        string userName,
        CancellationToken cancellationToken
    )
    {
        Request? request = _bulkRequestFactory.BuildUserByScreenName(api);

        if (request is null)
        {
            _logger.LogWarning("api 'UserByScreenName' is disabled or not configured");
            return null;
        }

        request.Query.Variables["screen_name"] = userName;
        request.Headers["Referer"] = _bulkSourceRouteProvider.GetReferer(SourceType.Notifications);

        string response = "";

        try
        {
            response = await _downloader.Download(request, cancellationToken);
            return _parser.ParseUser(response);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}, response: {response}", ex.Message, response);
        }

        return null;
    }

    public async Task<ParseResult?> GetUserMedia(
        IReadOnlyDictionary<string, ApiConfig> api,
        string id,
        string origin,
        int count,
        string? cursor,
        CancellationToken cancellationToken
    )
    {
        Request? request = _bulkRequestFactory.BuildUserMedia(api);

        if (request is null)
        {
            _logger.LogWarning("api 'UserMedia' is disabled or not configured");
            return null;
        }

        request.Query.Variables["userId"] = id;
        request.Query.Variables["count"] = count;
        request.Query.Variables["cursor"] = cursor;
        request.Headers["Referer"] = _bulkSourceRouteProvider.GetReferer(SourceType.Notifications);

        string response = "";

        try
        {
            response = await _downloader.Download(request, cancellationToken);
            ParseResult parseResult = _parser.Parse(id, origin, response);

            if (parseResult.Posts.Count == 0)
                _logger.LogInformation("response: {response}", response);

            return parseResult;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}, response: {response}", ex.Message, response);
        }

        return null;
    }
}
