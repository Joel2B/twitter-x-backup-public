namespace Backup.Application.BackupRun.Ports;

public interface IPostStoreVerifier
{
    Task Verify(CancellationToken cancellationToken = default);
}
