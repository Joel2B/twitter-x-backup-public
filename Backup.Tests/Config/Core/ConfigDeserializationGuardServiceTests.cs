using Backup.Application.Config;

namespace Backup.Tests;

public class ConfigDeserializationGuardServiceTests
{
    private sealed class Dummy
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void RequireConfig_ReturnsValue_WhenNotNull()
    {
        ConfigDeserializationGuardService sut = new();
        Dummy expected = new() { Value = "ok" };

        Dummy result = sut.RequireConfig(expected, "Data.json");

        Assert.Same(expected, result);
    }

    [Fact]
    public void RequireConfig_Throws_WhenNull()
    {
        ConfigDeserializationGuardService sut = new();

        FormatException ex = Assert.Throws<FormatException>(
            () => sut.RequireConfig<Dummy>(null, "Data.json")
        );

        Assert.Contains("error deserializing config file 'Data.json'", ex.Message);
    }
}
