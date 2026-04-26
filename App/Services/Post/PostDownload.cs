using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Config.Api;
using Backup.App.Models.Dump;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostDownload(
    ILogger<PostDownload> _logger,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly IPostDownloader _downloader = _downloader;

    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostParser _parser = _parser;

    private IPostData? _postData;
    private IPostData PostData => _postData ?? throw new Exception("media data not initialized");

    private ApiContext? _context;

    private ApiContext Context => _context ?? throw new Exception("Post context not initialized");

    private string UserId => Context.UserId;

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task Download(IPostData postData, ApiContext context)
    {
        _postData = postData;
        _context = context;
        _logger.LogInformation("download loaded {count} posts", await postData.GetCount());

        await ProcessDownloads();
        await Save();
    }

    private async Task ProcessDownloads()
    {
        try
        {
            DumpData? data = await _dump.GetData(Context);

            if (data is not null)
            {
                Context.Request.Query.Variables["count"] = data.QueryCount.ToString();
                Context.Count = data.Count;
                Context.Request.Query.Variables["cursor"] = data.Cursor;
            }

            int queryCount = Convert.ToInt32(Context.Request.Query.Variables["count"]);
            int count = 0;

            while (count < Context.Count)
            {
                _logger.LogInformation(
                    "Downloading {posts}:{count}:{total} posts",
                    queryCount,
                    count,
                    Context.Count
                );

                int attempts = 3;
                int attemptCount = 0;

                ParseResult result = new([], null);

                while (true)
                {
                    if (attemptCount == attempts)
                    {
                        if (data is not null)
                            await _dump.Flush(PostData, UserId, Context);

                        return;
                    }

                    _logger.LogWarning("Attempt #{attempt}", attemptCount + 1);

                    string response = "";

                    try
                    {
                        response = await _downloader.Download(Context.Request, _tokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error: {error}", ex.Message);
                    }

                    if (!string.IsNullOrEmpty(response))
                    {
                        await _postLogger.Save(Context.Id, response, _tokenSource.Token);

                        result = _parser.Parse(UserId, Context.Id, response);
                    }

                    if (result.Posts.Count == 0 || result.NextCursor is null)
                    {
                        attemptCount++;
                        await Task.Delay(1 * 1000);
                        continue;
                    }

                    if (data is not null)
                        await _dump.Save(response, result.Posts, result.NextCursor, Context);

                    break;
                }

                await PostData.AddPosts(UserId, Context.Id, result.Posts);

                count += queryCount;
                Context.Request.Query.Variables["cursor"] = result.NextCursor;

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
        _logger.LogInformation("saving posts");
        await PostData.Save();
    }
}
