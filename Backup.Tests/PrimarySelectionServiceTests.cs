using Backup.Application.Core;

namespace Backup.Tests;

public class PrimarySelectionServiceTests
{
    private readonly PrimarySelectionService _sut = new();

    [Fact]
    public void ResolvePrimary_Throws_WhenItemsEmpty()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.ResolvePrimary<int>([], _ => false, "no-items", "multi-default")
        );

        Assert.Equal("no-items", ex.Message);
    }

    [Fact]
    public void ResolvePrimary_Throws_WhenMultipleDefaults()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.ResolvePrimary([1, 2, 3], value => value > 1, "no-items", "multi-default")
        );

        Assert.Equal("multi-default", ex.Message);
    }

    [Fact]
    public void ResolvePrimary_ReturnsDefault_WhenExists()
    {
        int selected = _sut.ResolvePrimary([1, 2, 3], value => value == 2, "no-items", "multi-default");

        Assert.Equal(2, selected);
    }

    [Fact]
    public void ResolvePrimary_ReturnsFirst_WhenNoDefault()
    {
        int selected = _sut.ResolvePrimary([10, 20], _ => false, "no-items", "multi-default");

        Assert.Equal(10, selected);
    }
}
