namespace Backup.Application.Media.Models;

public sealed class MediaParallelDownloadSettings
{
    public required int MinDegreeOfParallelism { get; init; }
    public required int MaxDegreeOfParallelism { get; init; }
    public required int StartDegreeOfParallelism { get; init; }
    public required TimeSpan TargetDuration { get; init; }
    public required bool JumpToMaxOnFastAverage { get; init; }
    public required bool EnableHeavyCut { get; init; }
    public required TimeSpan HeavyThreshold { get; init; }
    public required bool StrictDecreaseGate { get; init; }
    public required bool EnableDebug { get; init; }
}
