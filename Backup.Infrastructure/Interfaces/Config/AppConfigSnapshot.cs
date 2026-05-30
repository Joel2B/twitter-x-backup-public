using Backup.Infrastructure.Models.Config;

namespace Backup.Infrastructure.Interfaces.Config;

public sealed record AppConfigSnapshot(long Version, DateTimeOffset LoadedAt, AppConfig Value);
