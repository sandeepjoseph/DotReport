using DotReport.Client.Models;
using FluentAssertions;

namespace DotReport.Proxy.Tests;

/// <summary>
/// Tests for ProxyState — the observable shared state container.
/// Ensures provisioning gates (UAC 7.3) and latency thresholds (UAC 7.2) behave correctly.
/// </summary>
public sealed class ProxyStateTests
{
    [Fact]
    public void ProxyState_InitialState_NeitherModelReady()
    {
        var state = new ProxyState();

        state.BothModelsReady.Should().BeFalse();
        state.BackupOnlyReady.Should().BeFalse();
        state.IsFullyProvisioned.Should().BeFalse();
    }

    [Fact]
    public void ProxyState_BothReady_BothModelsReadyIsTrue()
    {
        var state = new ProxyState
        {
            PrimaryStatus = ModelStatus.Ready,
            BackupStatus  = ModelStatus.Ready
        };

        state.BothModelsReady.Should().BeTrue();
        state.BackupOnlyReady.Should().BeFalse();
    }

    [Fact]
    public void ProxyState_BackupReadyPrimaryNot_BackupOnlyReadyIsTrue()
    {
        var state = new ProxyState
        {
            PrimaryStatus = ModelStatus.Loading,
            BackupStatus  = ModelStatus.Ready
        };

        state.BackupOnlyReady.Should().BeTrue();
        state.BothModelsReady.Should().BeFalse();
    }

    [Fact]
    public void ProxyState_AllFacesAssembled_IsFullyProvisionedIsTrue()
    {
        var state = new ProxyState
        {
            ProvisioningFacesAssembled = ProxyState.TotalDodecahedronFaces
        };

        state.IsFullyProvisioned.Should().BeTrue();
    }

    [Theory]
    [InlineData(0,  false)]
    [InlineData(6,  false)]
    [InlineData(11, false)]
    [InlineData(12, true)]
    public void ProxyState_ProvisioningFaceCount_IsFullyProvisioned(int faces, bool expected)
    {
        var state = new ProxyState { ProvisioningFacesAssembled = faces };

        state.IsFullyProvisioned.Should().Be(expected);
    }

    [Theory]
    [InlineData(0,   false)]
    [InlineData(499, false)]
    [InlineData(500, false)]
    [InlineData(501, true)]
    [InlineData(999, true)]
    public void ProxyState_PrimaryLatency_ExceedsThresholdAt501ms(long latencyMs, bool expected)
    {
        var state = new ProxyState { LastPrimaryLatencyMs = latencyMs };

        state.PrimaryExceedsLatencyThreshold.Should().Be(expected);
    }

    [Fact]
    public void ProxyState_TotalDodecahedronFaces_Is12()
    {
        ProxyState.TotalDodecahedronFaces.Should().Be(12);
    }
}
