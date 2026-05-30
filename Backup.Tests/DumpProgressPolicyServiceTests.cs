using Backup.Application.Dump;
using Backup.Application.Dump.Models;

namespace Backup.Tests;

public class DumpProgressPolicyServiceTests
{
    private readonly DumpProgressPolicyService _sut = new();

    [Fact]
    public void EnsureCurrent_UsesExisting_WhenProvided()
    {
        string current = _sut.EnsureCurrent("2026.05.30-10.00.00", new DateTime(2026, 1, 1));

        Assert.Equal("2026.05.30-10.00.00", current);
    }

    [Fact]
    public void EnsureCurrent_Generates_WhenMissing()
    {
        string current = _sut.EnsureCurrent(null, new DateTime(2026, 5, 30, 4, 5, 6));

        Assert.Equal("2026.05.30-04.05.06", current);
    }

    [Fact]
    public void AdvanceDirectoryIndex_Initializes_WhenIndexIsMinusOne()
    {
        DumpProgressState state = new()
        {
            Index = -1,
            IndexFile = -1,
            Count = 100,
            QueryCount = 20,
        };

        _sut.AdvanceDirectoryIndex(state);

        Assert.Equal(0, state.Index);
        Assert.Equal(-1, state.IndexFile);
    }

    [Fact]
    public void AdvanceDirectoryIndex_Advances_WhenIndexFileReachedLimit()
    {
        DumpProgressState state = new()
        {
            Index = 2,
            IndexFile = 4,
            Count = 100,
            QueryCount = 20,
        };

        _sut.AdvanceDirectoryIndex(state);

        Assert.Equal(3, state.Index);
        Assert.Equal(-1, state.IndexFile);
    }

    [Fact]
    public void AdvanceDirectoryIndex_DoesNotAdvance_WhenNotAtLimit()
    {
        DumpProgressState state = new()
        {
            Index = 2,
            IndexFile = 3,
            Count = 100,
            QueryCount = 20,
        };

        _sut.AdvanceDirectoryIndex(state);

        Assert.Equal(2, state.Index);
        Assert.Equal(3, state.IndexFile);
    }
}
