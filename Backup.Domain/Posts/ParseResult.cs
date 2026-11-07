namespace Backup.Domain.Posts;

public record ParseResult(List<Post> Posts, string? NextCursor, bool IsTerminal = false);

public record ParseUser(PostUser? User);
