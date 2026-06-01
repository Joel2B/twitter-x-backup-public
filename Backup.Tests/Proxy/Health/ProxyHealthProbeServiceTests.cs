using System.Net;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Tests;

public class ProxyHealthProbeServiceTests
{
    [Fact]
    public async Task Probe_ReturnsSuccess_WhenStatusIsOk()
    {
        ProxyHealthProbeService sut = new(new ProxyHealthCheckPolicyService());
        FakeProbePort port = new() { StatusToReturn = HttpStatusCode.OK };
        ProxyCandidate candidate = new()
        {
            Ip = "1.1.1.1",
            Port = "80",
            Protocol = "https",
        };

        ProxyHealthProbeResult result = await sut.Probe(candidate, port);

        Assert.True(result.Success);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("https", result.Candidate.Protocol);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Probe_FallsBackToHttp_WhenPolicyAllows()
    {
        ProxyHealthProbeService sut = new(new ProxyHealthCheckPolicyService());
        FakeProbePort port = new()
        {
            ThrowOnFirst = new InvalidOperationException(
                "The SSL connection could not be established"
            ),
            StatusToReturn = HttpStatusCode.OK,
        };
        ProxyCandidate candidate = new()
        {
            Ip = "2.2.2.2",
            Port = "443",
            Protocol = "https",
        };

        ProxyHealthProbeResult result = await sut.Probe(candidate, port);

        Assert.True(result.Success);
        Assert.Equal("http", result.Candidate.Protocol);
        Assert.Equal(2, port.CallCount);
    }

    [Fact]
    public async Task Probe_ReturnsFailure_WhenProbeThrowsWithoutFallback()
    {
        ProxyHealthProbeService sut = new(new ProxyHealthCheckPolicyService());
        FakeProbePort port = new() { ThrowOnFirst = new Exception("boom") };
        ProxyCandidate candidate = new()
        {
            Ip = "3.3.3.3",
            Port = "8080",
            Protocol = "http",
        };

        ProxyHealthProbeResult result = await sut.Probe(candidate, port);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.NotNull(result.Error);
    }

    private sealed class FakeProbePort : IProxyHealthProbePort
    {
        public HttpStatusCode StatusToReturn { get; init; } = HttpStatusCode.OK;

        public Exception? ThrowOnFirst { get; init; }

        public int CallCount { get; private set; }

        public Task<HttpStatusCode> Send(
            ProxyCandidate candidate,
            string url,
            TimeSpan timeout,
            CancellationToken cancellationToken
        )
        {
            CallCount++;

            if (CallCount == 1 && ThrowOnFirst is not null)
                throw ThrowOnFirst;

            return Task.FromResult(StatusToReturn);
        }
    }
}
