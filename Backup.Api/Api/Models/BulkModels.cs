using System.ComponentModel.DataAnnotations;

namespace Backup.Api.Models;

public sealed class BulkRunRequest
{
    [Required]
    [RegularExpression(@".*\S.*")]
    public required string UserId { get; init; }
}
