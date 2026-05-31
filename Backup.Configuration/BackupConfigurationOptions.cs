namespace Backup.Configuration;

public sealed class BackupConfigurationOptions
{
    public const string ConfigDirectoryEnvironmentVariable = "BACKUP__CONFIG__DIRECTORY";
    public string? ConfigDirectory { get; init; }
}
