using Backup.Application.Posts;

namespace Backup.Tests;

public class PostLogFolderPolicyServiceTests
{
    private readonly PostLogFolderPolicyService _sut = new();

    [Fact]
    public void CreateSessionFolderName_UsesExpectedFormat()
    {
        string folder = _sut.CreateSessionFolderName(new DateTime(2026, 5, 30, 4, 5, 6));

        Assert.Equal("2026.05.30-04.05.06", folder);
    }
}
