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
        string rawRequestBody,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RawPostParseResult parsed = _postParser.Parse(userId, origin, rawRequestBody);
            int receivedPosts = parsed.Posts.Count;
            int savedPosts = parsed.Posts.Count;
            PostIngestDiagnostics diagnostics = await PersistPosts(
                userId,
                origin,
                [.. parsed.Posts],
                receivedPosts,
                savedPosts,
                cancellationToken
            );

            return new PostIngestResult(receivedPosts, savedPosts, parsed.NextCursor, diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PostIngestionException("Raw post request could not be processed.", ex);
        }
    }

    public async Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<Post> posts,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            int receivedPosts = posts.Count;
            int savedPosts = posts.Count;

            PostIngestDiagnostics diagnostics = await PersistPosts(
                userId,
                origin,
                [.. posts],
                receivedPosts,
                savedPosts,
                cancellationToken
            );

            return new PostIngestResult(receivedPosts, savedPosts, null, diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        int savedPosts,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch timer = Stopwatch.StartNew();
        int beforeCount = await _postStore.GetCount(cancellationToken);
        await _postStore.AddPosts(userId, origin, posts, cancellationToken);
        await _postStore.Save(cancellationToken);
        int afterCount = await _postStore.GetCount(cancellationToken);
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
