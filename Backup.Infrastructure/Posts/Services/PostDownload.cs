using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Posts;

public class PostDownload(
    ILogger<PostDownload> _logger,
    IPostDownloadFlowService downloadFlowService,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostDomainParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly IPostDownloadFlowService _downloadFlowService = downloadFlowService;
    private readonly IPostDownloader _downloader = _downloader;

    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostDomainParser _parser = _parser;

    public async Task Download(IPostDomainData postData, ApiContext context)
    {
        _logger.LogInformation("download loaded {count} posts", await postData.GetCount());

        using CancellationTokenSource tokenSource = new();
        await ProcessDownloads(postData, context, tokenSource.Token);
        await Save(postData);
    }

    private async Task ProcessDownloads(
        IPostDomainData postData,
        ApiContext context,
        CancellationToken cancellationToken
    )
    {
        try
        {
            DumpData? data = await _dump.GetData(context);
            PostDownloadResumePoint? resumePoint = data is null
                ? null
                : new PostDownloadResumePoint
                {
                    QueryCount = data.QueryCount,
                    TotalCount = data.Count,
                    Cursor = data.Cursor,
                };

            int defaultQueryCount = Convert.ToInt32(context.Request.Query.Variables["count"]);
            string? defaultCursor =
                context.Request.Query.Variables.TryGetValue("cursor", out object? cursorValue)
                    ? cursorValue?.ToString()
                    : null;

            PostDownloadPlan plan = _downloadFlowService.CreatePlan(
                defaultQueryCount,
                context.Count,
                defaultCursor,
                resumePoint
            );

            context.Request.Query.Variables["count"] = plan.QueryCount.ToString();
            context.Count = plan.TotalCount;

            if (!string.IsNullOrEmpty(plan.Cursor))
                context.Request.Query.Variables["cursor"] = plan.Cursor;

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
                        response = await _downloader.Download(context.Request, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error: {error}", ex.Message);
                    }

                    if (!string.IsNullOrEmpty(response))
                    {
                        await _postLogger.Save(
                            $"{context.UserId}_{context.Id}",
                            response,
                            cancellationToken
                        );

                        result = _parser.Parse(context.UserId, context.Id, response);
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
                            await _dump.Flush(postData, context.UserId, context);

                        return;
                    }

                    if (data is not null)
                        await _dump.Save(
                            response,
                            result.Posts.Select(PostReplicationMapper.ToApp).ToList(),
                            result.NextCursor!,
                            context
                        );

                    break;
                }

                await postData.AddPosts(
                    context.UserId,
                    context.Id,
                    result.Posts
                );

                _downloadFlowService.ApplySuccess(plan, result.NextCursor!);
                context.Request.Query.Variables["cursor"] = plan.Cursor!;

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

    private async Task Save(IPostDomainData postData)
    {
        _logger.LogInformation("saving posts");
        await postData.Save();
    }
}


