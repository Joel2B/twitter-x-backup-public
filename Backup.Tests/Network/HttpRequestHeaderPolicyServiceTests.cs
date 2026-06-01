using Backup.Application.Network;

namespace Backup.Tests;

public class HttpRequestHeaderPolicyServiceTests
{
    private readonly HttpRequestHeaderPolicyService _sut = new();

    [Fact]
    public void ApplyHeaders_SkipsUnsupportedAndAddsRegularHeaders()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "https://x.com");

        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["authorization"] = "Bearer token",
            ["content-type"] = "application/json",
            ["accept-encoding"] = "gzip",
            ["referer"] = "https://x.com/home",
        };

        _sut.ApplyHeaders(request, headers);

        Assert.True(request.Headers.Contains("authorization"));
        Assert.False(request.Headers.Contains("accept-encoding"));
        Assert.Equal(new Uri("https://x.com/home"), request.Headers.Referrer);
    }
}
