namespace Backup.Application.Media.Filter;

public sealed class MediaErrorFilterPolicyService : IMediaErrorFilterPolicyService
{
    public bool ShouldExclude(string? message) =>
        string.Equals(message, "NotFound", StringComparison.Ordinal)
        || string.Equals(message, "Forbidden", StringComparison.Ordinal);
}
