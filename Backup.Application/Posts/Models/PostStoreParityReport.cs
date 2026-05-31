namespace Backup.Application.Posts.Models;

public sealed class PostStoreParityReport
{
    public required IReadOnlyList<PostStoreParitySnapshotItem> Snapshots { get; init; }
    public required IReadOnlyList<PostStoreParityStatusItem> Statuses { get; init; }
}

public sealed class PostStoreParitySnapshotItem
{
    public required string Label { get; init; }
    public required int Posts { get; init; }
    public required int Profiles { get; init; }
    public required int Hashtags { get; init; }
    public required int Medias { get; init; }
    public required int MediaVariants { get; init; }
    public required int IndexEntries { get; init; }
    public required int Changes { get; init; }
    public required int ChangeFields { get; init; }
    public required int HashMeta { get; init; }
}

public sealed class PostStoreParityStatusItem
{
    public required string PrimaryLabel { get; init; }
    public required string SecondaryLabel { get; init; }
    public required bool IsMismatch { get; init; }
    public required string DiffsText { get; init; }
}
