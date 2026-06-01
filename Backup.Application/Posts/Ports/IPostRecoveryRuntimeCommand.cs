namespace Backup.Application.Posts.Ports;

public interface IPostRecoveryRuntimeCommand
{
    void OnRecoveryStarting();
    Task RunRecovery(CancellationToken cancellationToken = default);
}
