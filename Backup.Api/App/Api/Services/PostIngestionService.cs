using Backup.App.Api.Errors;
using Backup.App.Api.Models;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.App.Api.Services;

public class PostIngestionService(
    IPostParser postParser,
    IPostData postData,
    ILogger<PostIngestionService> logger
) : IPostIngestionService
{
    private readonly IPostParser _postParser = postParser;
    private readonly IPostData _postData = postData;
    private readonly ILogger<PostIngestionService> _logger = logger;

    public async Task<PostIngestResult> IngestRaw(
        string userId,
        string origin,
        string rawRequestBody
    )
    {
        try
        {
            ParseResult parsed = _postParser.Parse(userId, origin, rawRequestBody);
            await PersistPosts(userId, origin, parsed.Posts);

            return new PostIngestResult(parsed.Posts.Count, parsed.Posts.Count, parsed.NextCursor);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Raw post ingestion failed for userId={userId}, origin={origin}",
                userId,
                origin
            );

            throw new ApiException("Raw post request could not be processed.");
        }
    }

    public async Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<ProcessedPostInput> posts
    )
    {
        try
        {
            List<Post> mappedPosts = ProcessedPostMapper.MapMany(posts);
            await PersistPosts(userId, origin, mappedPosts);

            return new PostIngestResult(posts.Count, posts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Processed post ingestion failed for userId={userId}, origin={origin}, count={count}",
                userId,
                origin,
                posts.Count
            );

            throw new ApiException("Processed post payload could not be saved.");
        }
    }

    private async Task PersistPosts(string userId, string origin, List<Post> posts)
    {
        _logger.LogInformation("{count}", await _postData.GetCount());
        await _postData.AddPosts(userId, origin, posts);
        await _postData.Save();
        _logger.LogInformation("{count}", await _postData.GetCount());
    }
}
