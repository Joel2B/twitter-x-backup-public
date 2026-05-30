namespace Backup.Infrastructure.Models.Posts;

public record DomainParseResult(List<Backup.Domain.Posts.Post> Posts, string? NextCursor);
