namespace Backup.Application.Posts.Models;

public sealed class PostTokenMaterializationBatchResult<T>
    where T : class
{
    public required IReadOnlyList<T> Items { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }
}
