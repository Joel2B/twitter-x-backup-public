namespace Backup.Api.Models;

public sealed record OperationResult(string Operation, string Status, string? Detail = null);
