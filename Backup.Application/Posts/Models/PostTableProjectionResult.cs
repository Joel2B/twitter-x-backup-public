namespace Backup.Application.Posts.Models;

public sealed class PostTableProjectionResult
{
    public IReadOnlyList<PostTablePostRow> Posts { get; init; } = [];
    public IReadOnlyList<PostTableProfileRow> Profiles { get; init; } = [];
    public IReadOnlyList<PostTableHashtagRow> Hashtags { get; init; } = [];
    public IReadOnlyList<PostTableMediaRow> Medias { get; init; } = [];
    public IReadOnlyList<PostTableMediaVariantRow> MediaVariants { get; init; } = [];
    public IReadOnlyList<PostTableIndexEntryRow> IndexEntries { get; init; } = [];
}
