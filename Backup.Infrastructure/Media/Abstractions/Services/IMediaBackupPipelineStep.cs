namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupPipelineStep
{
    public int Order { get; }
    public string TimerName { get; }
    public bool SkipWhenStopped { get; }
    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    );
}
