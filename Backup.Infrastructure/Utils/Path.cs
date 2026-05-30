using Backup.Application.IO;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Utils;

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
        => PathFormattingPolicy.ParseTimestampFromPath(path, isDir);

    public static string GetPathFormatted(string path)
        => PathFormattingPolicy.GetFormattedPath(path);

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
        => PathFormattingPolicy.NormalizePathForCurrentOs(path, save);

    public static string GetPartitionPath(AppConfig config, PartitionConfig partition)
    {
        IReadOnlyList<string> resolvedPaths = PathAliasResolutionPolicy.ResolveAliases(
            partition.Paths,
            config.Data.Aliases
        );

        return GetPath([.. resolvedPaths]);
    }
}
