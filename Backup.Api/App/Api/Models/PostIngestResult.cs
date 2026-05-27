namespace Backup.App.Api.Models;

public record PostIngestResult(int ReceivedPosts, int SavedPosts, string? NextCursor = null);
