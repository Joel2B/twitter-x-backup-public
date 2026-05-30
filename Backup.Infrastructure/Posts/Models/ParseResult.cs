namespace Backup.Infrastructure.Models.Posts;

public record ParseResult(List<Post> Posts, string? NextCursor);

