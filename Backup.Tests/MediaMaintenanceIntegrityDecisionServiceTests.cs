using Backup.Application.Media.Maintenance;

namespace Backup.Tests;

public sealed class MediaMaintenanceIntegrityDecisionServiceTests
{
    private readonly MediaMaintenanceIntegrityDecisionService _sut = new(
        new MediaMaintenanceFileProbePolicyService(),
        new MediaMaintenanceIntegrityEvaluationService(new MediaMaintenanceIntegrityPolicyService())
    );

    [Fact]
    public void ShouldProbe_UsesConfiguredThreshold()
    {
        Assert.True(_sut.ShouldProbe(999));
        Assert.False(_sut.ShouldProbe(1000));
        Assert.False(_sut.ShouldProbe(null));
    }

    [Fact]
    public void Evaluate_DelegatesToUnderlyingIntegrityPolicies()
    {
        var nullSize = _sut.Evaluate(null, isValid: false);
        Assert.True(nullSize.Remove);
        Assert.Equal(1, nullSize.NullCountIncrement);

        var smallAndInvalid = _sut.Evaluate(100, isValid: false);
        Assert.False(smallAndInvalid.Remove);
        Assert.Equal(1, smallAndInvalid.SizeCountIncrement);
        Assert.Equal(1, smallAndInvalid.InvalidCountIncrement);
    }
}
