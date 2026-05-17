using Backup.App.Models.Config;

namespace Backup.App.Interfaces.Config;

public sealed record AppConfigSnapshot(long Version, DateTimeOffset LoadedAt, AppConfig Value);
