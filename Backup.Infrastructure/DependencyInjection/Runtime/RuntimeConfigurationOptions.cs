namespace Backup.Infrastructure.DependencyInjection.Runtime;

public sealed class RuntimeConfigurationOptions
{
    public const string ConfigDirectoryEnvironmentVariable = "BACKUP__CONFIG__DIRECTORY";
    public const string ConfigDirectoryConfigurationKey = "Backup:Configuration:ConfigDirectory";

    public string? ConfigDirectory { get; init; }
}
