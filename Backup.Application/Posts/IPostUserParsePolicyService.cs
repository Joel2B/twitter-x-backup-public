using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostUserParsePolicyService
{
    bool IsUnavailable(string? typeName);

    PostUser? CreateUser(string? restId, int? mediaCount);
}
