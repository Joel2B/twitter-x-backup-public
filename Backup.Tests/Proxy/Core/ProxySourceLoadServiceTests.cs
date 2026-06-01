using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;

namespace Backup.Tests;

public class ProxySourceLoadServiceTests
{
    [Fact]
    public async Task Load_AggregatesEndpointsFromAllResources()
    {
        ProxySourceLoadService sut = new();
        FakePort port = new();
        ProxyLoadProviderDefinition[] providers =
        [
            new()
            {
                ProviderType = "http",
                ProviderFormat = "ipport",
                Resources =
                [
                    new() { ResourceType = "http", ResourceValue = "r1" },
                    new() { ResourceType = "http", ResourceValue = "r2" },
                ],
            },
        ];

        IReadOnlyList<ProxyEndpoint> result = await sut.Load(providers, port);

        Assert.Equal(2, result.Count);
        Assert.Equal("r1", result[0].Ip);
        Assert.Equal("r2", result[1].Ip);
    }

    [Fact]
    public async Task Load_SkipsEmptyResponses()
    {
        ProxySourceLoadService sut = new();
        FakePort port = new() { ReturnEmptyOnSecond = true };
        ProxyLoadProviderDefinition[] providers =
        [
            new()
            {
                ProviderType = "config",
                ProviderFormat = "ipport",
                Resources =
                [
                    new() { ResourceType = "http", ResourceValue = "r1" },
                    new() { ResourceType = "http", ResourceValue = "r2" },
                ],
            },
        ];

        IReadOnlyList<ProxyEndpoint> result = await sut.Load(providers, port);

        Assert.Single(result);
        Assert.Equal("r1", result[0].Ip);
    }

    private sealed class FakePort : IProxyResourceLoadPort
    {
        public bool ReturnEmptyOnSecond { get; init; }

        private int _calls;

        public Task<IReadOnlyList<ProxyEndpoint>> Load(ProxyLoadRequest request)
        {
            _calls++;

            if (ReturnEmptyOnSecond && _calls == 2)
                return Task.FromResult<IReadOnlyList<ProxyEndpoint>>([]);

            return Task.FromResult<IReadOnlyList<ProxyEndpoint>>(
                [
                    new()
                    {
                        Ip = request.ResourceValue,
                        Port = "80",
                        Protocol = request.ResourceType,
                    },
                ]
            );
        }
    }
}
