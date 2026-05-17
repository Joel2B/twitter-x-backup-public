using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Models.Bulk;
using Backup.App.Models.Config;
using Backup.App.Models.Config.Api;
using Backup.App.Models.Config.ApiRequest;
using Backup.App.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService(
    ILogger<BulkService> _logger,
    AppConfig _config,
    IPostData _postData,
    IBulkSourceData _bulkSourceData,
    IBulkData _bulkData,
    IPostDownloader _downloader,
    IPostParser _parser
) : IBulkService
{
    private readonly ILogger<BulkService> _logger = _logger;

    private readonly AppConfig _config = _config;
    private readonly IPostData _postData = _postData;
    private readonly IBulkSourceData _bulkSourceData = _bulkSourceData;
    private readonly IBulkData _bulkData = _bulkData;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;

    private readonly CancellationTokenSource _tokenSource = new();
    private UsersContext? _context;

    private Dictionary<string, ApiConfig> Api =>
        _context?.Api ?? throw new Exception("bulk context not initialized");

    public async Task Download(UsersContext context)
    {
        _context = context;

        await Import();
        await Verify();
        await Phase1();
        await Phase2();
        await ResetPhase2();

        await _bulkData.Prune();
    }

    private async Task<ParseUser?> GetUserByUser(string userName)
    {
        Request? request = RequestMerge.Build(Api, "UserByScreenName");

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
        Request? request = RequestMerge.Build(Api, "UserMedia");

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
