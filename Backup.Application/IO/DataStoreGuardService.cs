namespace Backup.Application.IO;

public sealed class DataStoreGuardService : IDataStoreGuardService
{
    public string RequireConfiguredFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? throw new Exception("file not configured") : fileName;

    public void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
            throw new Exception("File doesn't exist");
    }

    public T RequireDeserialized<T>(T? value, string message)
    {
        if (value is not null)
            return value;

        throw new Exception(message);
    }

    public T RequireInitialized<T>(T? value, string message)
    {
        if (value is not null)
            return value;

        throw new Exception(message);
    }

    public string RequireDirectoryName(string? directory, string message) =>
        string.IsNullOrWhiteSpace(directory) ? throw new Exception(message) : directory;
}
