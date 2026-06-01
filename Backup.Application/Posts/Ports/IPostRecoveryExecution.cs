namespace Backup.Application.Posts.Ports;

public interface IPostRecoveryExecution
{
    Task Recover(CancellationToken cancellationToken = default);
}
