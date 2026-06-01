using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaBackupCalculateStep : IMediaBackupPipelineStep
{
    public int Order => 10;
    public string TimerName => "calculate";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.CalculateAsync(cancellationToken);
}

public sealed class MediaBackupCalculateDirectStep : IMediaBackupPipelineStep
{
    public int Order => 20;
    public string TimerName => "calculate direct";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.CalculateDirectAsync(cancellationToken);
}

public sealed class MediaBackupApplyDirectStep : IMediaBackupPipelineStep
{
    public int Order => 30;
    public string TimerName => "apply direct";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.ApplyDirectAsync(cancellationToken);
}

public sealed class MediaBackupApplyStep : IMediaBackupPipelineStep
{
    public int Order => 40;
    public string TimerName => "apply";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.ApplyAsync(cancellationToken);
}

public sealed class MediaBackupCheckDuplicatesStep : IMediaBackupPipelineStep
{
    public int Order => 50;
    public string TimerName => "check duplicates";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.CheckDuplicatesAsync(cancellationToken);
}

public sealed class MediaBackupSetFileSizesStep : IMediaBackupPipelineStep
{
    public int Order => 60;
    public string TimerName => "set files sizes";
    public bool SkipWhenStopped => false;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.SetFileSizesAsync(cancellationToken);
}

public sealed class MediaBackupCheckIntegrityStep : IMediaBackupPipelineStep
{
    public int Order => 70;
    public string TimerName => "check integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.CheckIntegrityAsync(cancellationToken);
}

public sealed class MediaBackupFixIntegrityStep : IMediaBackupPipelineStep
{
    public int Order => 80;
    public string TimerName => "fix integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.FixIntegrityAsync(cancellationToken);
}

public sealed class MediaBackupCheckIntegrityAfterFixStep : IMediaBackupPipelineStep
{
    public int Order => 90;
    public string TimerName => "check integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(
        IMediaBackupPipelineActions actions,
        CancellationToken cancellationToken = default
    ) => actions.CheckIntegrityAsync(cancellationToken);
}
