using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Adapters;

public sealed class ProxyRuntimeRecordMapper(
    IProxyRuntimeStatusTransitionService proxyRuntimeStatusTransitionService
)
{
    private readonly IProxyRuntimeStatusTransitionService _proxyRuntimeStatusTransitionService =
        proxyRuntimeStatusTransitionService;

    public ProxyRuntimeRecord ToRuntimeRecord(ProxyData data) =>
        new()
        {
            Candidate = ToCandidate(data.Proxy),
            IsActive = data.Status.Current == StatusEnum.Active,
            Connections = data
                .Connections.Select(item => new ProxyRuntimeConnection
                {
                    Date = item.Date,
                    TotalUses = item.TotalUses,
                })
                .ToList(),
            Errors = data
                .Errors.Select(item => new ProxyRuntimeError
                {
                    Short = item.Message.Short,
                    Extended = item.Message.Extended,
                    TotalDuplicates = item.TotalDuplicates,
                    Date = item.Date,
                })
                .ToList(),
        };

    public ProxyData ToProxyData(ProxyRuntimeRecord record) =>
        new()
        {
            Proxy = ToProxyDataConfig(record.Candidate),
            Connections = record
                .Connections.Select(item => new Connection
                {
                    Date = item.Date,
                    TotalUses = item.TotalUses,
                })
                .ToList(),
            Errors = record
                .Errors.Select(item => new Error
                {
                    Message = new ErrorMessage { Short = item.Short, Extended = item.Extended },
                    TotalDuplicates = item.TotalDuplicates,
                    Date = item.Date,
                })
                .ToList(),
            Status = new Status
            {
                Current = record.IsActive ? StatusEnum.Active : StatusEnum.Inactive,
            },
        };

    public void ApplyRuntimeRecord(ProxyData proxy, ProxyRuntimeRecord source, DateTime? disabledAt)
    {
        proxy.Connections = source
            .Connections.Select(item => new Connection
            {
                Date = item.Date,
                TotalUses = item.TotalUses,
            })
            .ToList();
        proxy.Errors = source
            .Errors.Select(item => new Error
            {
                Message = new ErrorMessage { Short = item.Short, Extended = item.Extended },
                TotalDuplicates = item.TotalDuplicates,
                Date = item.Date,
            })
            .ToList();

        ProxyRuntimeStatusTransitionResult status =
            _proxyRuntimeStatusTransitionService.ResolveStatus(
                source.IsActive,
                proxy.Status.Date,
                disabledAt
            );
        proxy.Status.Current = status.IsActive ? StatusEnum.Active : StatusEnum.Inactive;

        if (status.StatusDate.HasValue)
            proxy.Status.Date = status.StatusDate.Value;
    }

    private static ProxyCandidate ToCandidate(ProxyDataConfig proxy) =>
        new()
        {
            Ip = proxy.Ip,
            Port = proxy.Port,
            Protocol = proxy.Protocol,
        };

    private static ProxyDataConfig ToProxyDataConfig(ProxyCandidate candidate) =>
        new()
        {
            Ip = candidate.Ip,
            Port = candidate.Port,
            Protocol = candidate.Protocol,
        };
}
