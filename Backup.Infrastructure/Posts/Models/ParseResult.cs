using Backup.Application.Posts.Models;

namespace Backup.Infrastructure.Models.Posts;

public record ParseResult(List<ParsedPostProjection> Posts, string? NextCursor);

