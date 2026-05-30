using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostRecoveryOrchestrationService(
    IPostRecoverySelectionService recoverySelectionService
) : IPostRecoveryOrchestrationService
{
    private readonly IPostRecoverySelectionService _recoverySelectionService = recoverySelectionService;

    public async Task<IReadOnlyCollection<Post>> Recover(
        IPostRecoverySession session,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(session);

        IReadOnlyCollection<PostRecoveryLog> logs = await session.GetRecoveryLogs();
        PostRecoverySelection selection = _recoverySelectionService.Select(
            session.RecoveryEnabled,
            logs,
            session.MaxRecoveryPosts
        );

        if (!selection.IsRecoveryEnabled)
        {
            session.OnRecoveryDisabled();
            return [];
        }

        session.OnSelectedPosts(selection.PostIds.Count);

        if (selection.PostIds.Count == 0)
            return [];

        if (!session.CanDownloadTweetDetail)
        {
            session.OnTweetDetailUnavailable();
            return [];
        }

        List<Post> posts = [];

        foreach (string id in selection.PostIds)
        {
            Post? post = await session.DownloadPost(id, cancellationToken);

            if (post is null)
                continue;

            posts.Add(post);
            await session.MarkRecovered(post.Id);
            session.OnRecoveredPost(id);
            await session.DelayBetweenDownloads(cancellationToken);
        }

        return posts;
    }
}
