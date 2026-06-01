using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public sealed class PartitionPathResolutionServiceTests
{
    private readonly PartitionPathResolutionService _sut = new();

    [Fact]
    public void Resolve_ComposesAbsolutePathWhenAbsMarkerProvided()
    {
        string result = _sut.Resolve(
            new PartitionPathSource
            {
                Paths = ["#Abs", "data", "posts"],
                Aliases = new Dictionary<string, string>(),
                BaseDirectory = @"C:\base",
            }
        );

        Assert.Equal(Path.Combine(@"C:\base", "data", "posts"), result);
    }

    [Fact]
    public void Resolve_ResolvesAliasesBeforeComposition()
    {
        string result = _sut.Resolve(
            new PartitionPathSource
            {
                Paths = ["@store", "posts"],
                Aliases = new Dictionary<string, string> { ["store"] = @"D:\disk" },
                BaseDirectory = @"C:\base",
            }
        );

        Assert.Equal(Path.Combine(@"D:\disk", "posts"), result);
    }
}
