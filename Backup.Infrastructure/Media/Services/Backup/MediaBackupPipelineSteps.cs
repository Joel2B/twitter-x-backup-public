using Backup.Infrastructure.Interfaces.Services.Media;

namespace Backup.Infrastructure.Services.Media;

public sealed class MediaBackupCalculateStep : IMediaBackupPipelineStep
{
    public int Order => 10;
    public string TimerName => "calculate";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.CalculateAsync();
}

public sealed class MediaBackupCalculateDirectStep : IMediaBackupPipelineStep
{
    public int Order => 20;
    public string TimerName => "calculate direct";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.CalculateDirectAsync();
}

public sealed class MediaBackupApplyDirectStep : IMediaBackupPipelineStep
{
    public int Order => 30;
    public string TimerName => "apply direct";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.ApplyDirectAsync();
}

public sealed class MediaBackupApplyStep : IMediaBackupPipelineStep
{
    public int Order => 40;
    public string TimerName => "apply";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.ApplyAsync();
}

public sealed class MediaBackupCheckDuplicatesStep : IMediaBackupPipelineStep
{
    public int Order => 50;
    public string TimerName => "check duplicates";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.CheckDuplicatesAsync();
}

public sealed class MediaBackupSetFileSizesStep : IMediaBackupPipelineStep
{
    public int Order => 60;
    public string TimerName => "set files sizes";
    public bool SkipWhenStopped => false;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.SetFileSizesAsync();
}

public sealed class MediaBackupCheckIntegrityStep : IMediaBackupPipelineStep
{
    public int Order => 70;
    public string TimerName => "check integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.CheckIntegrityAsync();
}

public sealed class MediaBackupFixIntegrityStep : IMediaBackupPipelineStep
{
    public int Order => 80;
    public string TimerName => "fix integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.FixIntegrityAsync();
}

public sealed class MediaBackupCheckIntegrityAfterFixStep : IMediaBackupPipelineStep
{
    public int Order => 90;
    public string TimerName => "check integrity";
    public bool SkipWhenStopped => true;

    public Task Execute(IMediaBackupPipelineActions actions) => actions.CheckIntegrityAsync();
}
