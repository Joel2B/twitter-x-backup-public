namespace Backup.Application.Media.Backup.Models;

public sealed class MediaPathDiffResult
{
    public required IReadOnlyList<string> MissingPaths { get; init; }

    public required IReadOnlyList<string> ExtraPaths { get; init; }
}
