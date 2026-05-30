using Backup.Application.Network;

namespace Backup.Tests;

public class RateLimitHeaderParserServiceTests
{
    private readonly RateLimitHeaderParserService _sut = new();

    [Fact]
    public void Parse_ReturnsParsedValues_WhenInputsValid()
    {
        var headers = _sut.Parse("150", "23", "1748592000");

        Assert.Equal(150, headers.Limit);
        Assert.Equal(23, headers.Remaining);
        Assert.Equal(1748592000, headers.ResetUnixSeconds);
    }

    [Fact]
    public void Parse_ThrowsNoLimit_WhenLimitInvalid()
    {
        Exception ex = Assert.Throws<Exception>(() => _sut.Parse("x", "23", "1748592000"));
        Assert.Equal("no limit", ex.Message);
    }

    [Fact]
    public void Parse_ThrowsNoRemaining_WhenRemainingInvalid()
    {
        Exception ex = Assert.Throws<Exception>(() => _sut.Parse("150", "x", "1748592000"));
        Assert.Equal("no remaining", ex.Message);
    }

    [Fact]
    public void Parse_ThrowsNoReset_WhenResetInvalid()
    {
        Exception ex = Assert.Throws<Exception>(() => _sut.Parse("150", "23", "x"));
        Assert.Equal("no reset", ex.Message);
    }
}
