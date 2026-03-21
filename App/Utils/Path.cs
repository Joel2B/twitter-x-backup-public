namespace Backup.App.Utils;

public class Path
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

        return System.IO.Path.Combine([root, .. _paths]);
    }

    public static DateTime? ToDate(string path, bool isDir = false)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        if (isDir)
            name = System.IO.Path.GetFileName(path);

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
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        string extension = System.IO.Path.GetExtension(path);

        fileName = $"{fileName}.formatted{extension}";
        path = path.Replace(System.IO.Path.GetFileName(path), fileName);

        return path;
    }

    public static void CopyDirectory(string sourceDir, string destDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"No existe el origen: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.EnumerateFiles(sourceDir))
        {
            string destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (string dir in Directory.EnumerateDirectories(sourceDir))
        {
            string destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
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

    public static string GetPartitionPath(
        Models.Config.App config,
        Models.Config.Data.Partition partition
    )
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
