using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Bulk;
using Backup.App.Models.Config.Request;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService(
    ILogger<BulkService> _logger,
    Models.Config.App _config,
    IEnumerable<IPostData> _postData,
    IEnumerable<IBulkSourceData> _bulkSourceData,
    IEnumerable<IBulkData> _bulkData,
    IPostDownloader _downloader,
    IPostParser _parser,
    IPostReplication _postReplication
) : IBulkService
{
    private readonly ILogger<BulkService> _logger = _logger;

    private readonly Models.Config.App _config = _config;
    private readonly IEnumerable<IPostData> _postData = _postData;
    private readonly IBulkSourceData _bulkSourceData = _bulkSourceData.First();
    private readonly IBulkData _bulkData = _bulkData.First();
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;
    private readonly IPostReplication _postReplication = _postReplication;

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task Download()
    {
        await Import();
        await Verify();
        await Phase1();
        await Phase2();
        await ResetPhase2();

        await _bulkData.Prune();
    }

    private async Task<ParseUser?> GetUserByUser(string userName)
    {
        Request? request = RequestMerge.Build(_config.Api, "UserByScreenName");

        if (request is null)
        {
            _logger.LogWarning("api 'UserByScreenName' is disabled or not configured");
            return null;
        }

        request.Query.Variables["screen_name"] = userName;
        request.Headers["Referer"] = GetReferer(SourceType.Notifications);

        string response = "";

        try
        {
            response = await _downloader.Download(request, _tokenSource.Token);
            return _parser.ParseUser(response);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}, response: {response}", ex.Message, response);
        }

        return null;
    }

    private async Task<ParseResult?> GetUserMedia(
        string id,
        string origin,
        int count,
        string? cursor
    )
    {
        Request? request = RequestMerge.Build(_config.Api, "UserMedia");

        if (request is null)
        {
            _logger.LogWarning("api 'UserMedia' is disabled or not configured");
            return null;
        }

        request.Query.Variables["userId"] = id;
        request.Query.Variables["count"] = count;
        request.Query.Variables["cursor"] = cursor;

        request.Headers["Referer"] = GetReferer(SourceType.Notifications);

        string response = "";

        try
        {
            response = await _downloader.Download(request, _tokenSource.Token);
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

    private static string? GetType(SourceType sourceType) =>
        sourceType switch
        {
            SourceType.Media => "media",
            SourceType.Notifications => "notifications",
            _ => null,
        };

    private static string GetReferer(
        SourceType sourceType = SourceType.Notifications,
        string? userName = null
    )
    {
        string baseUrl = "https://x.com/";
        string? type = GetType(sourceType);
        string url = "{type}";

        if (userName is not null)
            url = "{userName}/{type}";

        url = url.Replace("{userName}", userName).Replace("{type}", type);
        url = $"{baseUrl}{url}";

        return url;
    }
}
