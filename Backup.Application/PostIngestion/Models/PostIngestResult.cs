namespace Backup.Application.PostIngestion.Models;

public record PostIngestResult(
    int ReceivedPosts,
    int SavedPosts,
    string? NextCursor = null,
    PostIngestDiagnostics? Diagnostics = null
);
