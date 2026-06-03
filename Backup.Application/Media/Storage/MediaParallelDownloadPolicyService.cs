using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaParallelDownloadPolicyService : IMediaParallelDownloadPolicyService
{
    public MediaParallelDownloadSettings Create(
        int minDegreeOfParallelism,
        int maxDegreeOfParallelism,
        int startDegreeOfParallelism
    ) =>
        new()
        {
            MinDegreeOfParallelism = minDegreeOfParallelism,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            StartDegreeOfParallelism = startDegreeOfParallelism,
            TargetDuration = TimeSpan.FromSeconds(5),
            JumpToMaxOnFastAverage = false,
            EnableHeavyCut = true,
            HeavyThreshold = TimeSpan.FromSeconds(30),
            StrictDecreaseGate = true,
            EnableDebug = true,
        };
}
