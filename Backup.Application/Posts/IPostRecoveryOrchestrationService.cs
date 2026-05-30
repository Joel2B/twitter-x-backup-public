using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostRecoveryOrchestrationService
{
    Task<IReadOnlyCollection<Post>> Recover(
        IPostRecoverySession session,
        CancellationToken cancellationToken
    );
}

