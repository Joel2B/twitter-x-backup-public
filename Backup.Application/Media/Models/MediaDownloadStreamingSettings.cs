namespace Backup.Application.Media.Models;

public sealed class MediaDownloadStreamingSettings
{
    public required int BufferSizeBytes { get; init; }
    public required long ProgressThresholdBytes { get; init; }
    public required int ProgressStepPercent { get; init; }
}
