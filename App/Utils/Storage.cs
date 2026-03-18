namespace Backup.App.Utils;

public class Storage
{
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "0 B";

        if (bytes == 0)
            return "0 B";

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}
