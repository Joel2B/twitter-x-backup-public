using Backup.Application.Dump;

namespace Backup.Tests;

public class DumpSessionNamingPolicyServiceTests
{
    private readonly DumpSessionNamingPolicyService _sut = new();

    [Fact]
    public void CreateCurrentSessionName_UsesExpectedFormat()
    {
        string current = _sut.CreateCurrentSessionName(new DateTime(2026, 5, 30, 4, 5, 6));

        Assert.Equal("2026.05.30-04.05.06", current);
    }
}
