namespace Backup.Application.Posts.Models;

public record ParsedPostBatch(List<ParsedPostProjection> Posts, string? NextCursor);
