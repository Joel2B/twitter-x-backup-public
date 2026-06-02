using System.Globalization;

namespace Backup.Application.IO;

public static class PathFormattingPolicy
{
    public static DateTime? ParseTimestampFromPath(string path, bool isDirectory = false)
    {
        string name = isDirectory ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);

        bool isDate = DateTime.TryParseExact(
            name,
            "yyyy.MM.dd-HH.mm.ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsed
        );

        if (!isDate)
            return null;

        return parsed;
    }

    public static string GetFormattedPath(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string formattedName = $"{fileName}.formatted{extension}";

        return path.Replace(Path.GetFileName(path), formattedName);
    }

    public static string NormalizePathForCurrentOs(string path, bool save = false)
    {
        if (OperatingSystem.IsWindows())
            return path;

        if (OperatingSystem.IsLinux())
            return save ? path.Replace('/', '\\') : path.Replace('\\', '/');

        throw new PlatformNotSupportedException(
            "Path normalization is only supported on Windows and Linux."
        );
    }
}
