using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityDecisionService
{
    bool ShouldProbe(long? sizeBytes);
    MediaMaintenanceIntegrityEvaluation Evaluate(long? sizeBytes, bool isValid);
}
