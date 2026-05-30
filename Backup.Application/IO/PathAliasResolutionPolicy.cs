namespace Backup.Application.IO;

public static class PathAliasResolutionPolicy
{
    public static IReadOnlyList<string> ResolveAliases(
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, string> aliases
    )
    {
        List<string> resolved = [];

        foreach (string path in paths)
        {
            if (!path.StartsWith('@'))
            {
                resolved.Add(path);
                continue;
            }

            string alias = path.Replace("@", "");

            if (!aliases.TryGetValue(alias, out string? value))
                throw new Exception($"alias '{path}' is not set");

            resolved.Add(value);
        }

        return resolved;
    }
}
