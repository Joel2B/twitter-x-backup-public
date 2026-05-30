namespace Backup.Application.IO;

public static class PathCompositionPolicy
{
    public static string ComposePath(IReadOnlyList<string> paths, string baseDirectory)
    {
        List<string> parts = [.. paths];
        string root = string.Empty;

        if (parts.Contains("#Abs"))
        {
            root = baseDirectory;
            parts.RemoveAt(0);
        }

        return Path.Combine([root, .. parts]);
    }
}
