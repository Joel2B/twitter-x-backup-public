namespace Backup.Application.IO;

public interface IDataStoreGuardService
{
    string RequireConfiguredFileName(string? fileName);
    void EnsureFileExists(string path);
    T RequireDeserialized<T>(T? value, string message);
    T RequireInitialized<T>(T? value, string message);
    string RequireDirectoryName(string? directory, string message);
}
