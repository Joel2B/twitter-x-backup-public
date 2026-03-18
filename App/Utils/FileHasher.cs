using System.Security.Cryptography;

namespace Backup.App.Utils;

public static class FileHasher
{
    public static async Task<string?> GetFileHash(string filePath, string algorithm = "SHA256")
    {
        if (!File.Exists(filePath))
            return null;

        using FileStream stream = File.OpenRead(filePath);

        using HashAlgorithm hasher = algorithm switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => throw new NotSupportedException($"algorithm '{algorithm}' is not supported."),
        };

        byte[] hashBytes = await hasher.ComputeHashAsync(stream);

        return Convert.ToHexString(hashBytes);
    }
}
