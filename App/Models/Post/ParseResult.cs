namespace Backup.App.Models.Posts;

public record ParseResult(List<Post> Posts, string? NextCursor);

public record ParseUser(User? User);
