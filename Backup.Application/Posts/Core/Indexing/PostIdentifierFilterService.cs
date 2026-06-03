namespace Backup.Application.Posts;

public sealed class PostIdentifierFilterService : IPostIdentifierFilterService
{
    public IReadOnlySet<string> Normalize(IReadOnlyCollection<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
}
