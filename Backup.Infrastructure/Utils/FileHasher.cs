using Backup.Application.IO;

namespace Backup.Infrastructure.Utils;

public static class FileHasher
{
    public static Task<string?> GetFileHash(string filePath, string algorithm = "SHA256") =>
        FileHashPolicy.GetFileHash(filePath, algorithm);
}
