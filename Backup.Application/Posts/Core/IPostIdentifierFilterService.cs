namespace Backup.Application.Posts;

public interface IPostIdentifierFilterService
{
    IReadOnlySet<string> Normalize(IReadOnlyCollection<string> ids);
}
