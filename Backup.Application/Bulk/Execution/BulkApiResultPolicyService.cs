using Backup.Domain.Posts;

namespace Backup.Application.Bulk;

public sealed class BulkApiResultPolicyService : IBulkApiResultPolicyService
{
    public bool ShouldLogRawResponse(ParseResult result) => result.Posts.Count == 0;
}
