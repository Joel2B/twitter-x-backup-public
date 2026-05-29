namespace Backup.Application.Posts.Models;

public sealed class PostRecoverySelection
{
    public static readonly PostRecoverySelection Disabled = new() { IsRecoveryEnabled = false };

    public bool IsRecoveryEnabled { get; init; } = true;
    public IReadOnlyList<string> PostIds { get; init; } = [];
}
