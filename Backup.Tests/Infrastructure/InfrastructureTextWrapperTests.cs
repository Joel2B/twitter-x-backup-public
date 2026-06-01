namespace Backup.Tests;

public class InfrastructureTextWrapperTests
{
    [Fact]
    public void Diff_MapsApplicationResultToInfrastructureModel()
    {
        Backup.Infrastructure.Models.Utils.Diff result = Backup.Infrastructure.Utils.Text.Diff(
            "line1\nline2",
            "line2\nline3"
        );

        Assert.Equal(["line1"], result.Diff1);
        Assert.Equal(["line3"], result.Diff2);
    }
}
