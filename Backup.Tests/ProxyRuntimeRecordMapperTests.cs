using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Tests;

public sealed class ProxyRuntimeRecordMapperTests
{
    [Fact]
    public void ToRuntimeRecord_Maps_Status_Connections_And_Errors()
    {
        ProxyRuntimeRecordMapper sut = new(new ProxyRuntimeStatusTransitionService());
        ProxyData data = CreateProxyData(StatusEnum.Active);

        ProxyRuntimeRecord result = sut.ToRuntimeRecord(data);

        Assert.True(result.IsActive);
        Assert.Equal("127.0.0.1", result.Candidate.Ip);
        Assert.Single(result.Connections);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ToProxyData_Maps_Runtime_Record_Back_To_Storage_Model()
    {
        ProxyRuntimeRecordMapper sut = new(new ProxyRuntimeStatusTransitionService());
        ProxyRuntimeRecord runtime = new()
        {
            Candidate = new ProxyCandidate { Ip = "10.0.0.1", Port = "8080", Protocol = "http" },
            IsActive = false,
            Connections = [new ProxyRuntimeConnection { Date = DateTime.Now, TotalUses = 3 }],
            Errors =
            [
                new ProxyRuntimeError
                {
                    Short = "s",
                    Extended = "e",
                    TotalDuplicates = 1,
                    Date = DateTime.Now,
                },
            ],
        };

        ProxyData result = sut.ToProxyData(runtime);

        Assert.Equal(StatusEnum.Inactive, result.Status.Current);
        Assert.Equal("10.0.0.1", result.Proxy.Ip);
        Assert.Single(result.Connections);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ApplyRuntimeRecord_Sets_Inactive_And_Date_When_Disabled()
    {
        ProxyRuntimeRecordMapper sut = new(new ProxyRuntimeStatusTransitionService());
        ProxyData proxy = CreateProxyData(StatusEnum.Active);
        DateTime disabledAt = new(2026, 5, 30, 20, 0, 0, DateTimeKind.Utc);
        ProxyRuntimeRecord source = new()
        {
            Candidate = new ProxyCandidate { Ip = "1.1.1.1", Port = "80", Protocol = "http" },
            IsActive = false,
            Connections = [],
            Errors = [],
        };

        sut.ApplyRuntimeRecord(proxy, source, disabledAt);

        Assert.Equal(StatusEnum.Inactive, proxy.Status.Current);
        Assert.Equal(disabledAt, proxy.Status.Date);
    }

    private static ProxyData CreateProxyData(StatusEnum status) =>
        new()
        {
            Proxy = new ProxyDataConfig { Ip = "127.0.0.1", Port = "8080", Protocol = "http" },
            Status = new Status { Current = status, Date = DateTime.UtcNow },
            Connections = [new Connection { Date = DateTime.UtcNow, TotalUses = 1 }],
            Errors =
            [
                new Error
                {
                    Message = new ErrorMessage { Short = "x", Extended = "y" },
                    Date = DateTime.UtcNow,
                    TotalDuplicates = 2,
                },
            ],
        };
}
