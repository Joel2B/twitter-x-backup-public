using Backup.Application.Dump;
using Backup.Application.Dump.Models;

namespace Backup.Tests;

public class DumpProgressPolicyServiceTests
{
    [Fact]
    public void AdvanceDirectory_Initializes_WhenIndexIsMinusOne()
    {
        DumpLifecycleService sut = new(
            new DumpSessionNamingPolicyService(),
            new DumpContextGuardService()
        );

        DumpProgressState state = new()
        {
            Index = -1,
            IndexFile = -1,
            Count = 100,
            QueryCount = 20,
        };

        DumpProgressState result = sut.AdvanceDirectory(
            state.Index,
            state.IndexFile,
            state.Count,
            state.QueryCount
        );

        Assert.Equal(0, result.Index);
        Assert.Equal(-1, result.IndexFile);
    }

    [Fact]
    public void AdvanceDirectory_Advances_WhenIndexFileReachedLimit()
    {
        DumpLifecycleService sut = new(
            new DumpSessionNamingPolicyService(),
            new DumpContextGuardService()
        );

        DumpProgressState state = new()
        {
            Index = 2,
            IndexFile = 4,
            Count = 100,
            QueryCount = 20,
        };

        DumpProgressState result = sut.AdvanceDirectory(
            state.Index,
            state.IndexFile,
            state.Count,
            state.QueryCount
        );

        Assert.Equal(3, result.Index);
        Assert.Equal(-1, result.IndexFile);
    }

    [Fact]
    public void AdvanceDirectory_DoesNotAdvance_WhenNotAtLimit()
    {
        DumpLifecycleService sut = new(
            new DumpSessionNamingPolicyService(),
            new DumpContextGuardService()
        );

        DumpProgressState state = new()
        {
            Index = 2,
            IndexFile = 3,
            Count = 100,
            QueryCount = 20,
        };

        DumpProgressState result = sut.AdvanceDirectory(
            state.Index,
            state.IndexFile,
            state.Count,
            state.QueryCount
        );

        Assert.Equal(2, result.Index);
        Assert.Equal(3, result.IndexFile);
    }
}
