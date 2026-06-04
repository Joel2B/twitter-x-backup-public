using System.Globalization;

namespace Backup.Application.IO;

public static class PathFormattingPolicy
{
    public static DateTime? ParseTimestampFromPath(string path, bool isDirectory = false)
    {
        string name = GetPathLeaf(path);

        if (!isDirectory)
            name = Path.GetFileNameWithoutExtension(name);

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
        string fileName = GetPathLeaf(path);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string formattedName = $"{fileNameWithoutExtension}.formatted{extension}";

        return path[..^fileName.Length] + formattedName;
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

    private static string GetPathLeaf(string path)
    {
        int slashIndex = path.LastIndexOf('/');
        int backslashIndex = path.LastIndexOf('\\');
        int separatorIndex = Math.Max(slashIndex, backslashIndex);

        return separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
    }
}
