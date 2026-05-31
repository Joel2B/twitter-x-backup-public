using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostUserParsePolicyService : IPostUserParsePolicyService
{
    public bool IsUnavailable(string? typeName) =>
        string.Equals(typeName, "UserUnavailable", StringComparison.Ordinal);

    public PostUser? CreateUser(string? restId, int? mediaCount)
    {
        if (string.IsNullOrWhiteSpace(restId) || mediaCount is null)
            return null;

        return new PostUser { Id = restId, MediaCount = mediaCount.Value };
    }
}
