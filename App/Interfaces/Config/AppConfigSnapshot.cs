namespace Backup.App.Interfaces.Config;

public sealed record AppConfigSnapshot(
    long Version,
    DateTimeOffset LoadedAt,
    Models.Config.App Value
);
