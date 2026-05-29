namespace Backup.Api.Models;

public record PostIngestDiagnostics(
    int BeforeCount,
    int AfterCount,
    int DeltaCount,
    int IgnoredPosts,
    long DurationMs
);

