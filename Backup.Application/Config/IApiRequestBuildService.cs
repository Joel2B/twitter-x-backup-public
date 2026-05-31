using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public interface IApiRequestBuildService
{
    ApiRequestBuildResult? Build(IReadOnlyDictionary<string, ApiRequestBuildSource> requests, string key);
}
