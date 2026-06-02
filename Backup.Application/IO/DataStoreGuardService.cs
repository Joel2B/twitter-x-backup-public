namespace Backup.Application.IO;

public sealed class DataStoreGuardService : IDataStoreGuardService
{
    public string RequireConfiguredFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? throw new InvalidOperationException("file not configured")
            : fileName;

    public void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File doesn't exist", path);
    }

    public T RequireDeserialized<T>(T? value, string message)
    {
        if (value is not null)
            return value;

        throw new InvalidOperationException(message);
    }

    public T RequireInitialized<T>(T? value, string message)
    {
        if (value is not null)
            return value;

        throw new InvalidOperationException(message);
    }

    public string RequireDirectoryName(string? directory, string message) =>
        string.IsNullOrWhiteSpace(directory)
            ? throw new InvalidOperationException(message)
            : directory;
}
