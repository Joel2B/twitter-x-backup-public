using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckEvaluationService
{
    IReadOnlyList<MediaCacheRecheckEvaluation> Evaluate(
        IReadOnlyCollection<MediaCacheRecheckObservation> observations
    );
}
