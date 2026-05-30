using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Posts;

public class PostDownload(
    ILogger<PostDownload> _logger,
    IPostDownloadFlowService downloadFlowService,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly IPostDownloadFlowService _downloadFlowService = downloadFlowService;
    private readonly IPostDownloader _downloader = _downloader;

    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostParser _parser = _parser;

    private IPostDomainData? _postData;
    private IPostDomainData PostData => _postData ?? throw new Exception("media data not initialized");

    private ApiContext? _context;

    private ApiContext Context => _context ?? throw new Exception("Post context not initialized");

    private string UserId => Context.UserId;

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task Download(IPostDomainData postData, ApiContext context)
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
            PostDownloadResumePoint? resumePoint = data is null
                ? null
                : new PostDownloadResumePoint
                {
                    QueryCount = data.QueryCount,
                    TotalCount = data.Count,
                    Cursor = data.Cursor,
                };

            int defaultQueryCount = Convert.ToInt32(Context.Request.Query.Variables["count"]);
            string? defaultCursor =
                Context.Request.Query.Variables.TryGetValue("cursor", out object? cursorValue)
                    ? cursorValue?.ToString()
                    : null;

            PostDownloadPlan plan = _downloadFlowService.CreatePlan(
                defaultQueryCount,
                Context.Count,
                defaultCursor,
                resumePoint
            );

            Context.Request.Query.Variables["count"] = plan.QueryCount.ToString();
            Context.Count = plan.TotalCount;

            if (!string.IsNullOrEmpty(plan.Cursor))
                Context.Request.Query.Variables["cursor"] = plan.Cursor;

            while (_downloadFlowService.ShouldContinue(plan))
            {
                _logger.LogInformation(
                    "Downloading {posts}:{count}:{total} posts",
                    plan.QueryCount,
                    plan.DownloadedCount,
                    plan.TotalCount
                );

                const int maxAttempts = 3;
                int attemptCount = 0;

                ParseResult result = new([], null);
                string response = "";

                while (true)
                {
                    _logger.LogWarning("Attempt #{attempt}", attemptCount + 1);
                    response = "";

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
                        await _postLogger.Save(
                            $"{UserId}_{Context.Id}",
                            response,
                            _tokenSource.Token
                        );

                        result = _parser.Parse(UserId, Context.Id, response);
                    }

                    bool hasValidPage = result.Posts.Count > 0 && result.NextCursor is not null;
                    PostDownloadPageDecision decision = _downloadFlowService.DecidePage(
                        hasValidPage,
                        attemptCount + 1,
                        maxAttempts,
                        hasResumePoint: data is not null
                    );

                    if (decision.Outcome == PostDownloadPageOutcome.Retry)
                    {
                        attemptCount++;
                        await Task.Delay(1 * 1000);
                        continue;
                    }

                    if (decision.Outcome == PostDownloadPageOutcome.Abort)
                    {
                        if (decision.ShouldFlushDump && data is not null)
                            await _dump.Flush(PostData, UserId, Context);

                        return;
                    }

                    if (data is not null)
                        await _dump.Save(response, result.Posts, result.NextCursor!, Context);

                    break;
                }

                await PostData.AddPosts(
                    UserId,
                    Context.Id,
                    result.Posts.Select(PostReplicationMapper.ToDomain).ToList()
                );

                _downloadFlowService.ApplySuccess(plan, result.NextCursor!);
                Context.Request.Query.Variables["cursor"] = plan.Cursor!;

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


