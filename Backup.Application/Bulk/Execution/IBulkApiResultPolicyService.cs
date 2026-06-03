using Backup.Domain.Posts;

namespace Backup.Application.Bulk;

public interface IBulkApiResultPolicyService
{
    bool ShouldLogRawResponse(ParseResult result);
}
