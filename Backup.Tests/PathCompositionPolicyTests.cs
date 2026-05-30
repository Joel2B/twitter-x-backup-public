using Backup.Application.IO;

namespace Backup.Tests;

public class PathCompositionPolicyTests
{
    [Fact]
    public void ComposePath_UsesBaseDirectory_WhenAbsMarkerExists()
    {
        string baseDirectory = @"C:\base";

        string result = PathCompositionPolicy.ComposePath(["#Abs", "config", "Data.json"], baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "config", "Data.json"), result);
    }

    [Fact]
    public void ComposePath_UsesRelativePath_WhenAbsMarkerMissing()
    {
        string result = PathCompositionPolicy.ComposePath(["config", "Data.json"], @"C:\base");

        Assert.Equal(Path.Combine("config", "Data.json"), result);
    }
}
