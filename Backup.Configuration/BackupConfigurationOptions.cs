namespace Backup.Configuration;

public sealed class BackupConfigurationOptions
{
    public const string ConfigDirectoryEnvironmentVariable = "BACKUP__CONFIG__DIRECTORY";
    public const string ConfigDirectoryConfigurationKey = "Backup:Configuration:ConfigDirectory";
    public string? ConfigDirectory { get; init; }
}
