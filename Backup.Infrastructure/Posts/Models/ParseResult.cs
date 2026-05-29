namespace Backup.Infrastructure.Models.Posts;

public record ParseResult(List<Post> Posts, string? NextCursor);

public record ParseUser(PostUser? User);

