using Backup.Infrastructure.Models.Config;

namespace Backup.Infrastructure.Core.Abstractions.Config;

public sealed record AppConfigSnapshot(long Version, DateTimeOffset LoadedAt, AppConfig Value);
