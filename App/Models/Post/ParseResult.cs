namespace Backup.App.Models.Post;

public record ParseResult(List<Post> Posts, string? NextCursor);

public record ParseUser(User? User);
