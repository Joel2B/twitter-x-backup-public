using Backup.Application.Core;

namespace Backup.Tests;

public class StorageRegistrationPolicyServiceTests
{
    private readonly StorageRegistrationPolicyService _sut = new();

    [Fact]
    public void SelectEnabled_ReturnsEmpty_WhenNoEnabled()
    {
        List<FakeStorage> storages =
        [
            new()
            {
                Enabled = false,
                Default = false,
                Supported = true,
            },
        ];

        IReadOnlyList<FakeStorage> selected = _sut.SelectEnabled(
            storages,
            storage => storage.Enabled,
            storage => storage.Supported,
            storage => storage.Default
        );

        Assert.Empty(selected);
    }

    [Fact]
    public void SelectEnabled_Throws_WhenEnabledButNoDefault()
    {
        List<FakeStorage> storages =
        [
            new()
            {
                Enabled = true,
                Default = false,
                Supported = true,
            },
        ];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () =>
                _sut.SelectEnabled(
                    storages,
                    storage => storage.Enabled,
                    storage => storage.Supported,
                    storage => storage.Default
                )
        );

        Assert.Contains("Default=true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectEnabled_Throws_WhenMultipleDefaults()
    {
        List<FakeStorage> storages =
        [
            new()
            {
                Enabled = true,
                Default = true,
                Supported = true,
            },
            new()
            {
                Enabled = true,
                Default = true,
                Supported = true,
            },
        ];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () =>
                _sut.SelectEnabled(
                    storages,
                    storage => storage.Enabled,
                    storage => storage.Supported,
                    storage => storage.Default
                )
        );

        Assert.Contains("Only one enabled storage", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectEnabled_FiltersUnsupported_AndReturnsEnabled()
    {
        List<FakeStorage> storages =
        [
            new()
            {
                Enabled = true,
                Default = true,
                Supported = true,
            },
            new()
            {
                Enabled = true,
                Default = false,
                Supported = false,
            },
            new()
            {
                Enabled = false,
                Default = false,
                Supported = true,
            },
        ];

        IReadOnlyList<FakeStorage> selected = _sut.SelectEnabled(
            storages,
            storage => storage.Enabled,
            storage => storage.Supported,
            storage => storage.Default
        );

        Assert.Single(selected);
        Assert.True(selected[0].Default);
    }

    private sealed class FakeStorage
    {
        public bool Enabled { get; set; }
        public bool Default { get; set; }
        public bool Supported { get; set; }
    }
}
