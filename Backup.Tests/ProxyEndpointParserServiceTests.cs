using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyEndpointParserServiceTests
{
    [Fact]
    public void Parse_IpPort_ReturnsEndpoints()
    {
        ProxyEndpointParserService sut = new();
        string[] lines = ["1.2.3.4:8080", "5.6.7.8:9090"];

        var result = sut.Parse("ipport", lines, "http");

        Assert.Equal(2, result.Count);
        Assert.Equal("1.2.3.4", result[0].Ip);
        Assert.Equal("8080", result[0].Port);
        Assert.Equal("http", result[0].Protocol);
    }

    [Fact]
    public void Parse_IpPort_Throws_WhenLineInvalid()
    {
        ProxyEndpointParserService sut = new();
        string[] lines = ["invalid_line"];

        Assert.Throws<FormatException>(() => sut.Parse("ipport", lines, "http"));
    }

    [Fact]
    public void Parse_Throws_WhenFormatUnsupported()
    {
        ProxyEndpointParserService sut = new();
        string[] lines = ["1.2.3.4:8080"];

        Assert.Throws<NotSupportedException>(() => sut.Parse("unknown", lines, "http"));
    }
}
