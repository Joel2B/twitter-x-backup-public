using System.Diagnostics;
using Backup.Application.PostIngestion.Models;
using Backup.Application.PostIngestion.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.PostIngestion;

public class PostIngestionService(IRawPostParser postParser, IPostStoreWriter postStore)
    : IPostIngestionService
{
    private readonly IRawPostParser _postParser = postParser;
    private readonly IPostStoreWriter _postStore = postStore;

    public async Task<PostIngestResult> IngestRaw(
        string userId,
        string origin,
        string rawRequestBody
    )
    {
        try
        {
            RawPostParseResult parsed = _postParser.Parse(userId, origin, rawRequestBody);
            int receivedPosts = parsed.Posts.Count;
            int savedPosts = parsed.Posts.Count;
            PostIngestDiagnostics diagnostics = await PersistPosts(
                userId,
                origin,
                [.. parsed.Posts],
                receivedPosts,
                savedPosts
            );

            return new PostIngestResult(receivedPosts, savedPosts, parsed.NextCursor, diagnostics);
        }
        catch (Exception ex)
        {
            throw new PostIngestionException("Raw post request could not be processed.", ex);
        }
    }

    public async Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<Post> posts
    )
    {
        try
        {
            int receivedPosts = posts.Count;
            int savedPosts = posts.Count;

            PostIngestDiagnostics diagnostics = await PersistPosts(
                userId,
                origin,
                [.. posts],
                receivedPosts,
                savedPosts
            );

            return new PostIngestResult(receivedPosts, savedPosts, null, diagnostics);
        }
        catch (Exception ex)
        {
            throw new PostIngestionException("Processed post payload could not be saved.", ex);
        }
    }

    private async Task<PostIngestDiagnostics> PersistPosts(
        string userId,
        string origin,
        List<Post> posts,
        int receivedPosts,
        int savedPosts
    )
    {
        Stopwatch timer = Stopwatch.StartNew();
        int beforeCount = await _postStore.GetCount();
        await _postStore.AddPosts(userId, origin, posts);
        await _postStore.Save();
        int afterCount = await _postStore.GetCount();
        timer.Stop();

        return new PostIngestDiagnostics(
            beforeCount,
            afterCount,
            afterCount - beforeCount,
            Math.Max(0, receivedPosts - savedPosts),
            timer.ElapsedMilliseconds
        );
    }
}
