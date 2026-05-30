using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkRequestFactory
{
    Request? BuildUserByScreenName(IReadOnlyDictionary<string, ApiConfig> api);
    Request? BuildUserMedia(IReadOnlyDictionary<string, ApiConfig> api);
}
