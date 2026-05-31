namespace Backup.Application.Media.Backup.Models;

public sealed class MediaPathDuplicateGroup
{
    public required string Path { get; init; }

    public required int Count { get; init; }

    public required IReadOnlyList<string> Entries { get; init; }
}
