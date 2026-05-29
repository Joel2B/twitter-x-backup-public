using Backup.Domain.Posts;

namespace Backup.Application.PostIngestion.Models;

public record RawPostParseResult(IReadOnlyCollection<Post> Posts, string? NextCursor);
