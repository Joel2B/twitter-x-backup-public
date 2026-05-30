using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Data.Dump;
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
    IPostDownloadOrchestrationService postDownloadOrchestrationService,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostDomainParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly IPostDownloadOrchestrationService _postDownloadOrchestrationService =
        postDownloadOrchestrationService;
    private readonly IPostDownloader _downloader = _downloader;

    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostDomainParser _parser = _parser;
    private readonly IDumpData _dump = _dump;

    public async Task Download(IPostDomainData postData, ApiContext context)
    {
        _logger.LogInformation("download loaded {count} posts", await postData.GetCount());

        using CancellationTokenSource tokenSource = new();
        try
        {
            await _postDownloadOrchestrationService.Run(
                new PostDownloadSession(
                    _logger,
                    _downloader,
                    _postLogger,
                    _parser,
                    _dump,
                    postData,
                    context
                ),
                tokenSource.Token
            );
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
        finally
        {
            await _postLogger.Prune();
        }

        await Save(postData);
    }

    private async Task Save(IPostDomainData postData)
    {
        _logger.LogInformation("saving posts");
        await postData.Save();
    }

    private sealed class PostDownloadSession(
        ILogger<PostDownload> logger,
        IPostDownloader downloader,
        IPostLogger postLogger,
        IPostDomainParser parser,
        IDumpData dump,
        IPostDomainData postData,
        ApiContext context
    ) : IPostDownloadSession
    {
        private readonly ILogger<PostDownload> _logger = logger;
        private readonly IPostDownloader _downloader = downloader;
        private readonly IPostLogger _postLogger = postLogger;
        private readonly IPostDomainParser _parser = parser;
        private readonly IDumpData _dump = dump;
        private readonly IPostDomainData _postData = postData;
        private readonly ApiContext _context = context;
        private DumpData? _resumeData;

        public int DefaultQueryCount =>
            Convert.ToInt32(_context.Request.Query.Variables["count"]);

        public int DefaultTotalCount => _context.Count;

        public string? DefaultCursor =>
            _context.Request.Query.Variables.TryGetValue("cursor", out object? cursorValue)
                ? cursorValue?.ToString()
                : null;

        public async Task<PostDownloadResumePoint?> GetResumePoint()
        {
            _resumeData = await _dump.GetData(_context);

            return _resumeData is null
                ? null
                : new PostDownloadResumePoint
                {
                    QueryCount = _resumeData.QueryCount,
                    TotalCount = _resumeData.Count,
                    Cursor = _resumeData.Cursor,
                };
        }

        public void ApplyPlan(PostDownloadPlan plan)
        {
            _context.Request.Query.Variables["count"] = plan.QueryCount.ToString();
            _context.Count = plan.TotalCount;

            if (!string.IsNullOrEmpty(plan.Cursor))
                _context.Request.Query.Variables["cursor"] = plan.Cursor;
        }

        public void SetCursor(string cursor) =>
            _context.Request.Query.Variables["cursor"] = cursor;

        public void OnPageCycle(PostDownloadPlan plan)
        {
            _logger.LogInformation(
                "Downloading {posts}:{count}:{total} posts",
                plan.QueryCount,
                plan.DownloadedCount,
                plan.TotalCount
            );
        }

        public void OnAttempt(int attemptNumber) =>
            _logger.LogWarning("Attempt #{attempt}", attemptNumber);

        public async Task<PostDownloadPageResult> FetchPage(CancellationToken cancellationToken)
        {
            string response = "";
            ParseResult result = new([], null);

            try
            {
                response = await _downloader.Download(_context.Request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", ex.Message);
            }

            if (!string.IsNullOrEmpty(response))
            {
                await _postLogger.Save(
                    $"{_context.UserId}_{_context.Id}",
                    response,
                    cancellationToken
                );

                result = _parser.Parse(_context.UserId, _context.Id, response);
            }

            return new PostDownloadPageResult
            {
                Posts = result.Posts,
                NextCursor = result.NextCursor,
                RawResponse = response,
            };
        }

        public async Task PersistResumeState(PostDownloadPageResult pageResult)
        {
            if (_resumeData is null)
                return;

            await _dump.Save(
                pageResult.RawResponse,
                pageResult.Posts.Select(PostReplicationMapper.ToApp).ToList(),
                pageResult.NextCursor!,
                _context
            );
        }

        public async Task FlushResumeState()
        {
            if (_resumeData is null)
                return;

            await _dump.Flush(_postData, _context.UserId, _context);
        }

        public async Task AddPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts)
            => await _postData.AddPosts(_context.UserId, _context.Id, [.. posts]);
    }
}


