using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Bulk.Abstractions.Services;

public interface IBulkRequestFactory
{
    Request? BuildUserByScreenName(IReadOnlyDictionary<string, ApiConfig> api);
    Request? BuildUserVideoTimeline(IReadOnlyDictionary<string, ApiConfig> api);
    Request? BuildUserPhotoTimeline(IReadOnlyDictionary<string, ApiConfig> api);
}
