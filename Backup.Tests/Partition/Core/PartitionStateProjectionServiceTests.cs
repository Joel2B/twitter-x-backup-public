using Backup.Application.Partition;
using Backup.Application.Partition.Models;

namespace Backup.Tests;

public sealed class PartitionStateProjectionServiceTests
{
    private readonly PartitionStateProjectionService _sut = new();

    [Fact]
    public void ToState_MapsAllFields()
    {
        PartitionStateSource source = new()
        {
            Id = 7,
            Type = "primary",
            Tags = ["cache"],
            Size = 1000,
            UsableSpace = 85,
            Enabled = true,
            CurrentSize = 250,
        };

        PartitionState state = _sut.ToState(source);

        Assert.Equal(7, state.Id);
        Assert.Equal("primary", state.Type);
        Assert.Equal(["cache"], state.Tags);
        Assert.Equal(1000, state.Size);
        Assert.Equal(85, state.UsableSpace);
        Assert.True(state.Enabled);
        Assert.Equal(250, state.CurrentSize);
    }

    [Fact]
    public void ToStates_MapsEachSource()
    {
        IReadOnlyList<PartitionState> states = _sut.ToStates(
            [
                new PartitionStateSource
                {
                    Id = 1,
                    Type = "primary",
                    Tags = null,
                    Size = 100,
                    UsableSpace = 90,
                    Enabled = true,
                    CurrentSize = 10,
                },
                new PartitionStateSource
                {
                    Id = 2,
                    Type = "heavy",
                    Tags = ["heavy"],
                    Size = 200,
                    UsableSpace = 80,
                    Enabled = false,
                    CurrentSize = 50,
                },
            ]
        );

        Assert.Equal(2, states.Count);
        Assert.Equal(1, states[0].Id);
        Assert.Equal("primary", states[0].Type);
        Assert.Equal(2, states[1].Id);
        Assert.Equal("heavy", states[1].Type);
    }
}
