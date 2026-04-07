using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Dump;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostDownload(
    ILogger<PostDownload> _logger,
    Models.Config.App _config,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IPostDownloader _downloader = _downloader;

    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostParser _parser = _parser;

    private IPostData? _postData;
    private IPostData PostData => _postData ?? throw new Exception("media data not initialized");

    private Dictionary<string, Models.Post.Post>? _posts;
    private Dictionary<string, Models.Post.Post> Posts =>
        _posts ?? throw new Exception("Posts not initialized");

    private string UserId =>
        _config.Source.Request.Query.Variables["userId"]?.ToString() ?? throw new Exception();

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task Download(IPostData postData)
    {
        _postData = postData;
        _posts = await postData.GetAllAsDictionary() ?? [];
        _logger.LogInformation("download loaded {count} posts", _posts.Count);

        await ProcessDownloads();
        await Save();
    }

    private async Task ProcessDownloads()
    {
        try
        {
            DumpData? data = await _dump.GetData();

            if (data is not null)
            {
                _config.Source.Request.Query.Variables["count"] = data.QueryCount.ToString();
                _config.Source.Count = data.Count;
                _config.Source.Request.Query.Variables["cursor"] = data.Cursor;
            }

            int queryCount = Convert.ToInt32(_config.Source.Request.Query.Variables["count"]);
            int count = 0;

            while (count < _config.Source.Count)
            {
                _logger.LogInformation(
                    "Downloading {posts}:{count}:{total} posts",
                    queryCount,
                    count,
                    _config.Source.Count
                );

                int attempts = 3;
                int attemptCount = 0;

                ParseResult result = new([], null);

                while (true)
                {
                    if (attemptCount == attempts)
                    {
                        if (data is not null)
                            _posts = await _dump.Flush(UserId);

                        return;
                    }

                    _logger.LogWarning("Attempt #{attempt}", attemptCount + 1);

                    string response = "";

                    try
                    {
                        response = await _downloader.Download(
                            _config.Source.Request,
                            _tokenSource.Token
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error: {error}", ex.Message);
                    }

                    if (!string.IsNullOrEmpty(response))
                    {
                        await _postLogger.Save(response, _tokenSource.Token);
                        result = _parser.Parse(UserId, _config.Source.Id, response);
                    }

                    if (result.Posts.Count == 0 || result.NextCursor is null)
                    {
                        attemptCount++;
                        await Task.Delay(1 * 1000);
                        continue;
                    }

                    if (data is not null)
                        await _dump.Save(response, result.Posts, result.NextCursor);

                    break;
                }

                _posts = await PostData.AddPosts(UserId, _config.Source.Id, result.Posts);

                count += queryCount;
                _config.Source.Request.Query.Variables["cursor"] = result.NextCursor;

                await Task.Delay(5 * 1000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
        finally
        {
            await _postLogger.Prune();
        }
    }

    private async Task Save()
    {
        _logger.LogInformation("Saving {data} data", Posts.Values.Count);
        await PostData.Save([.. Posts.Values]);
    }
}
