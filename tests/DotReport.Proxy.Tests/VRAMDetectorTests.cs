using DotReport.Client.Interop;
using DotReport.Client.Models;
using DotReport.Client.Services;
using FluentAssertions;
using Moq;

namespace DotReport.Proxy.Tests;

/// <summary>
/// Tests for VRAMDetector — pre-flight hardware assessment.
/// UAC 7.1: WebGPU supported → Standard build. Less than 4GB → Lightweight.
/// </summary>
public sealed class VRAMDetectorTests
{
    private static DeviceCapabilities MakeCaps(bool webGpu, long vramMb) =>
        new()
        {
            WebGpuSupported      = webGpu,
            WebAssemblySupported = true,
            EstimatedVramMb      = vramMb,
            AdapterName          = "Test Adapter"
        };

    [Fact]
    public void DeviceCapabilities_WithWebGpuAnd8GbVram_RecommentsStandardBuild()
    {
        var caps = MakeCaps(webGpu: true, vramMb: 8192);

        caps.RecommendedBuild.Should().Be(BuildProfile.Standard);
        caps.HasSufficientVram.Should().BeTrue();
    }

    [Fact]
    public void DeviceCapabilities_WithLessThan4GbVram_RecommentsLightweightBuild()
    {
        var caps = MakeCaps(webGpu: true, vramMb: 2048);

        caps.RecommendedBuild.Should().Be(BuildProfile.Lightweight);
        caps.HasSufficientVram.Should().BeFalse();
    }

    [Fact]
    public void DeviceCapabilities_NoWebGpu_FallsBackToWasm()
    {
        var caps = MakeCaps(webGpu: false, vramMb: 0);

        caps.WebGpuSupported.Should().BeFalse();
        caps.WebAssemblySupported.Should().BeTrue();
        caps.RecommendedBuild.Should().Be(BuildProfile.Lightweight);
    }

    [Fact]
    public void DeviceCapabilities_ExactlyAt4GbThreshold_IsStandard()
    {
        var caps = MakeCaps(webGpu: true, vramMb: 4096);

        caps.HasSufficientVram.Should().BeTrue();
        caps.RecommendedBuild.Should().Be(BuildProfile.Standard);
    }

    [Fact]
    public async Task VRAMDetector_CachesResultAfterFirstCall()
    {
        var babylonMock = new Mock<BabylonInterop>(Mock.Of<Microsoft.JSInterop.IJSRuntime>());
        babylonMock
            .Setup(b => b.GetDeviceCapabilitiesAsync())
            .ReturnsAsync(MakeCaps(webGpu: true, vramMb: 8192));

        var detector = new VRAMDetector(babylonMock.Object);

        var first  = await detector.DetectAsync();
        var second = await detector.DetectAsync();

        first.Should().BeSameAs(second);
        babylonMock.Verify(b => b.GetDeviceCapabilitiesAsync(), Times.Once);
    }

    [Fact]
    public void VRAMDetector_BeforeFirstCall_HasNoCachedResult()
    {
        var detector = new VRAMDetector(Mock.Of<BabylonInterop>());

        detector.HasCachedResult.Should().BeFalse();
        detector.Cached.Should().BeNull();
    }
}
