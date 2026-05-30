namespace Backup.Infrastructure.Interfaces.Services.Media;

public interface IMediaBackupPipelineStep
{
    public int Order { get; }
    public string TimerName { get; }
    public bool SkipWhenStopped { get; }
    public Task Execute(IMediaBackupPipelineActions actions);
}
