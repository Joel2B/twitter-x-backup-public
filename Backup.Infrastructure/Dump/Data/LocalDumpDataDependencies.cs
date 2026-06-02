using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.IO;
using Backup.Infrastructure.Dump.Abstractions.Services;

namespace Backup.Infrastructure.Dump.Data;

public sealed class LocalDumpDataDependencies(
    ISecondaryStoreSelectionService secondaryStoreSelectionService,
    IDumpContextEligibilityService dumpContextEligibilityService,
    IDumpLifecycleService dumpLifecycleService,
    IDumpPathService dumpPathService,
    IDumpIndexLoadService dumpIndexLoadService,
    IDumpSaveExecutionService dumpSaveExecutionService,
    IDumpFlushOrchestrationService dumpFlushOrchestrationService,
    IDumpReplicationPlanningService dumpReplicationPlanningService,
    IDumpPersistenceIOService dumpPersistenceIOService,
    IDataStoreGuardService dataStoreGuardService,
    IDateTimeProvider dateTimeProvider
)
{
    public ISecondaryStoreSelectionService SecondaryStoreSelectionService { get; } =
        secondaryStoreSelectionService;
    public IDumpContextEligibilityService DumpContextEligibilityService { get; } =
        dumpContextEligibilityService;
    public IDumpLifecycleService DumpLifecycleService { get; } = dumpLifecycleService;
    public IDumpPathService DumpPathService { get; } = dumpPathService;
    public IDumpIndexLoadService DumpIndexLoadService { get; } = dumpIndexLoadService;
    public IDumpSaveExecutionService DumpSaveExecutionService { get; } = dumpSaveExecutionService;
    public IDumpFlushOrchestrationService DumpFlushOrchestrationService { get; } =
        dumpFlushOrchestrationService;
    public IDumpReplicationPlanningService DumpReplicationPlanningService { get; } =
        dumpReplicationPlanningService;
    public IDumpPersistenceIOService DumpPersistenceIOService { get; } = dumpPersistenceIOService;
    public IDataStoreGuardService DataStoreGuardService { get; } = dataStoreGuardService;
    public IDateTimeProvider DateTimeProvider { get; } = dateTimeProvider;
}
