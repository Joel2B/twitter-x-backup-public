using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckDecisionService
{
    MediaCacheRecheckApplyResult Decide(MediaCacheRecheckObservation observation);
}
