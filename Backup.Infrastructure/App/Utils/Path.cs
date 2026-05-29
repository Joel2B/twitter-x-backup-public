using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;

namespace Backup.App.Utils;

public class UtilsPath
{
    public static string GetPath(List<string> paths)
    {
        List<string> _paths = [.. paths];

        string root = "";

        if (_paths.Contains("#Abs"))
        {
            root = AppDomain.CurrentDomain.BaseDirectory;
            _paths.RemoveAt(0);
        }

        return Path.Combine([root, .. _paths]);
    }

    public static DateTime? ToDate(string path, bool isDir = false)
    {
        string name = Path.GetFileNameWithoutExtension(path);

        if (isDir)
            name = Path.GetFileName(path);

        bool isDate = DateTime.TryParseExact(
            name,
            "yyyy.MM.dd-HH.mm.ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out DateTime parsed
        );

        if (!isDate)
            return null;

        return parsed;
    }

    public static string GetPathFormatted(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        fileName = $"{fileName}.formatted{extension}";
        path = path.Replace(Path.GetFileName(path), fileName);

        return path;
    }

    public static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"No existe el origen: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.EnumerateFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (string dir in Directory.EnumerateDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir, overwrite);
        }
    }

    public static string NormalizePath(string path, bool save = false)
    {
        if (OperatingSystem.IsWindows())
            return path;

        if (OperatingSystem.IsLinux())
            if (save)
                return path.Replace('/', '\\');
            else
                return path.Replace('\\', '/');

        throw new Exception();
    }

    public static string GetPartitionPath(AppConfig config, PartitionConfig partition)
    {
        List<string> paths = [];

        foreach (string path in partition.Paths)
        {
            if (!path.StartsWith('@'))
            {
                paths.Add(path);
                continue;
            }

            string alias = path.Replace("@", "");

            if (!config.Data.Aliases.TryGetValue(alias, out string? value))
                throw new Exception($"alias '{path}' is not set");

            paths.Add(value);
        }

        return GetPath(paths);
    }
}
