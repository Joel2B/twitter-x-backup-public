using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostDownloadSessionAdapter(
    ILogger logger,
    IPostDownloader downloader,
    IPostLogger postLogger,
    IPostDomainParser parser,
    IDumpData dump,
    IPostDomainData postData,
    ApiContext context
) : IPostDownloadSession
{
    private readonly ILogger _logger = logger;
    private readonly IPostDownloader _downloader = downloader;
    private readonly IPostLogger _postLogger = postLogger;
    private readonly IPostDomainParser _parser = parser;
    private readonly IDumpData _dump = dump;
    private readonly IPostDomainData _postData = postData;
    private readonly ApiContext _context = context;
    private DumpData? _resumeData;

    public int DefaultQueryCount => Convert.ToInt32(_context.Request.Query.Variables["count"]);

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

    public void SetCursor(string cursor) => _context.Request.Query.Variables["cursor"] = cursor;

    public void OnPageCycle(PostDownloadPlan plan)
    {
        _logger.LogInformation(
            "Downloading {posts}:{count}:{total} posts",
            plan.QueryCount,
            plan.DownloadedCount,
            plan.TotalCount
        );
    }

    public void OnAttempt(int attemptNumber) => _logger.LogWarning("Attempt #{attempt}", attemptNumber);

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
            await _postLogger.Save($"{_context.UserId}_{_context.Id}", response, cancellationToken);

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

    public async Task AddPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts) =>
        await _postData.AddPosts(_context.UserId, _context.Id, [.. posts]);
}
