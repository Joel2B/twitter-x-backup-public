using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public sealed class PostRecoveryCommandService(
    IPostRecoveryOrchestrationService postRecoveryOrchestrationService
) : IPostRecoveryCommandService
{
    private readonly IPostRecoveryOrchestrationService _postRecoveryOrchestrationService =
        postRecoveryOrchestrationService;

    public async Task Execute(IPostRecoveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            IReadOnlyCollection<Backup.Domain.Posts.Post> posts =
                await _postRecoveryOrchestrationService.Recover(
                    command.CreateSession(),
                    cancellationToken
                );

            if (posts.Count == 0)
            {
                command.OnNoPostsRecovered();
                return;
            }

            await command.MergeRecoveredPosts(posts);
            command.OnPostsMerged(posts.Count);
            await command.SavePosts();
        }
        catch (Exception ex)
        {
            command.OnError(ex);
        }
    }
}
