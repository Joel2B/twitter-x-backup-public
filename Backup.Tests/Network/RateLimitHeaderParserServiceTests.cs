using Backup.Application.Network;

namespace Backup.Tests;

public class RateLimitHeaderParserServiceTests
{
    private readonly RateLimitHeaderParserService _sut = new();

    [Fact]
    public void Parse_ReturnsParsedValues_WhenInputsValid()
    {
        bool parsed = _sut.TryParse("150", "23", "1748592000", out var headers);

        Assert.True(parsed);
        Assert.NotNull(headers);
        Assert.Equal(150, headers.Limit);
        Assert.Equal(23, headers.Remaining);
        Assert.Equal(1748592000, headers.ResetUnixSeconds);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenLimitInvalid()
    {
        bool parsed = _sut.TryParse("x", "23", "1748592000", out var headers);

        Assert.False(parsed);
        Assert.Null(headers);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenRemainingInvalid()
    {
        bool parsed = _sut.TryParse("150", "x", "1748592000", out var headers);

        Assert.False(parsed);
        Assert.Null(headers);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenResetMissing()
    {
        bool parsed = _sut.TryParse("150", "23", null, out var headers);

        Assert.False(parsed);
        Assert.Null(headers);
    }
}
