using Backup.Application.Core;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Adapters;

public sealed class ProxyRuntimeMutationService(
    IDateTimeProvider dateTimeProvider,
    ProxyRuntimeRecordMapper proxyRuntimeRecordMapper,
    IProxyUseHandlingOrchestrationService proxyUseHandlingOrchestrationService,
    IProxyErrorHandlingOrchestrationService proxyErrorHandlingOrchestrationService
) : IProxyRuntimeMutationService
{
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ProxyRuntimeRecordMapper _proxyRuntimeRecordMapper = proxyRuntimeRecordMapper;
    private readonly IProxyUseHandlingOrchestrationService _proxyUseHandlingOrchestrationService =
        proxyUseHandlingOrchestrationService;
    private readonly IProxyErrorHandlingOrchestrationService _proxyErrorHandlingOrchestrationService =
        proxyErrorHandlingOrchestrationService;

    public ProxyUseHandlingOutcome HandleUse(ProxyData proxy, int stopCount)
    {
        ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
        ProxyUseHandlingOutcome outcome = _proxyUseHandlingOrchestrationService.HandleUse(
            runtimeRecord,
            _dateTimeProvider.Now,
            stopCount
        );
        _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, disabledAt: null);

        return outcome;
    }

    public ProxyErrorHandlingOutcome HandleError(
        ProxyData proxy,
        Exception exception,
        int errorsToInactive
    )
    {
        DateTime now = _dateTimeProvider.Now;
        ProxyRuntimeRecord runtimeRecord = _proxyRuntimeRecordMapper.ToRuntimeRecord(proxy);
        ProxyErrorHandlingOutcome outcome = _proxyErrorHandlingOrchestrationService.Handle(
            runtimeRecord,
            proxy.Status.Current == StatusEnum.Active,
            exception.Message,
            exception.ToString(),
            errorsToInactive,
            now
        );

        if (outcome.ShouldApplyRuntimeRecord)
            _proxyRuntimeRecordMapper.ApplyRuntimeRecord(proxy, runtimeRecord, outcome.DisabledAt);

        return outcome;
    }
}
