using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityEvaluationService
{
    MediaMaintenanceIntegrityEvaluation Evaluate(
        long? cacheFileSize,
        bool isValidMediaFile,
        long maxFileSizeBytes
    );
}
