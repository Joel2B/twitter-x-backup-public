using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckApplyPolicyService
{
    MediaCacheRecheckApplyResult Apply(string path, MediaCacheRecheckResult decision);
}
