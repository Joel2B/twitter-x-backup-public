using System.ComponentModel.DataAnnotations;

namespace Backup.Api.Models;

public sealed class PostDownloadRequest
{
    [Required]
    [RegularExpression(@".*\S.*")]
    public required string UserId { get; init; }

    [Required]
    [RegularExpression(@".*\S.*")]
    public required string SourceId { get; init; }
}

public sealed class PostRecoveryRequest
{
    [Required]
    [RegularExpression(@".*\S.*")]
    public required string UserId { get; init; }
}

public sealed class PostIdsRequest
{
    [Required]
    [MinLength(1)]
    public required List<string> Ids { get; init; }
}

public sealed class PostCountsResponse
{
    public required int PrimaryCount { get; init; }
    public required IReadOnlyList<PostStoreCountsSummary> Stores { get; init; }
}

public sealed class PostStoreSummary
{
    public required string? Id { get; init; }
    public required bool IsDefault { get; init; }
    public required string StoreType { get; init; }
}

public sealed class PostStoreCountsSummary
{
    public required string? Id { get; init; }
    public required bool IsDefault { get; init; }
    public required int Posts { get; init; }
    public required int Profiles { get; init; }
    public required int Hashtags { get; init; }
    public required int Medias { get; init; }
    public required int MediaVariants { get; init; }
    public required int IndexEntries { get; init; }
    public required int Changes { get; init; }
    public required int ChangeFields { get; init; }
    public required int HashMeta { get; init; }
}

public sealed class PostStoreParityResponse
{
    public required DateTimeOffset VerifiedAt { get; init; }
    public required int StoreCount { get; init; }
    public required IReadOnlyList<PostStoreParitySnapshot> Snapshots { get; init; }
    public required IReadOnlyList<PostStoreParityStatus> Statuses { get; init; }
}

public sealed class PostStoreParitySnapshot
{
    public required string Label { get; init; }
    public required int Posts { get; init; }
    public required int Profiles { get; init; }
    public required int Hashtags { get; init; }
    public required int Medias { get; init; }
    public required int MediaVariants { get; init; }
    public required int IndexEntries { get; init; }
    public required int Changes { get; init; }
    public required int ChangeFields { get; init; }
    public required int HashMeta { get; init; }
}

public sealed class PostStoreParityStatus
{
    public required string PrimaryLabel { get; init; }
    public required string SecondaryLabel { get; init; }
    public required bool IsMismatch { get; init; }
    public required string DiffsText { get; init; }
}
