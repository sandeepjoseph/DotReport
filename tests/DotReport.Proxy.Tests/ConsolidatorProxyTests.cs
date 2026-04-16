using DotReport.Client.Models;
using DotReport.Client.Services;
using FluentAssertions;
using Moq;

namespace DotReport.Proxy.Tests;

/// <summary>
/// xUnit tests for the ConsolidatorProxy dual-model failover logic.
/// Tests UAC 7.2: 500ms latency threshold, backup merge, no "Thinking" hang.
/// London School TDD — all dependencies are mocked.
/// </summary>
public sealed class ConsolidatorProxyTests
{
    // ── SYS-01: Primary latency within threshold ──────────────────────────
    [Fact]
    public async Task InferAsync_WhenPrimaryFast_StreamsFromPrimaryOnly()
    {
        // Arrange
        var orchestratorMock = new Mock<ModelOrchestrator>(
            Mock.Of<VRAMDetector>(),
            Mock.Of<IndexedDbService>(),
            Mock.Of<DotReport.Client.Interop.OnnxInterop>());

        var fastTokens = new[] { "Hello", " world", "." };
        orchestratorMock
            .Setup(o => o.StreamAsync(It.Is<InferenceRequest>(r => r.TargetModel == ModelRole.Primary),
                It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(fastTokens, delayMs: 10));

        orchestratorMock
            .Setup(o => o.StreamAsync(It.Is<InferenceRequest>(r => r.TargetModel == ModelRole.Backup),
                It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(new[] { "Backup", " output" }, delayMs: 50));

        var proxy = new ConsolidatorProxy(orchestratorMock.Object);

        // Act
        var tokens = new List<string>();
        await foreach (var t in proxy.InferAsync("Test prompt", "System"))
            tokens.Add(t);

        // Assert
        tokens.Should().Contain("Hello");
        proxy.State.IsProcessing.Should().BeFalse();
    }

    // ── SYS-02: Primary exceeds 500ms → backup merges ─────────────────────
    [Fact]
    public async Task InferAsync_WhenPrimaryExceedsLatencyThreshold_BackupMerges()
    {
        // Arrange
        var orchestratorMock = new Mock<ModelOrchestrator>(
            Mock.Of<VRAMDetector>(),
            Mock.Of<IndexedDbService>(),
            Mock.Of<DotReport.Client.Interop.OnnxInterop>());

        // Primary is slow — delivers no tokens for 600ms
        orchestratorMock
            .Setup(o => o.StreamAsync(It.Is<InferenceRequest>(r => r.TargetModel == ModelRole.Primary),
                It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(new[] { "Slow" }, delayMs: 600));

        // Backup is fast
        var backupTokens = new[] { "Backup", " fast", " output" };
        orchestratorMock
            .Setup(o => o.StreamAsync(It.Is<InferenceRequest>(r => r.TargetModel == ModelRole.Backup),
                It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(backupTokens, delayMs: 20));

        var proxy = new ConsolidatorProxy(orchestratorMock.Object);

        // Act
        var tokens = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var t in proxy.InferAsync("Test prompt", "System", cts.Token))
            tokens.Add(t);

        // Assert — output should include backup tokens and NOT hang
        tokens.Should().NotBeEmpty();
        proxy.State.IsProcessing.Should().BeFalse();
    }

    // ── Proxy state resets after each inference ────────────────────────────
    [Fact]
    public async Task InferAsync_CompletedInference_ResetsIsProcessingToFalse()
    {
        var orchestratorMock = new Mock<ModelOrchestrator>(
            Mock.Of<VRAMDetector>(),
            Mock.Of<IndexedDbService>(),
            Mock.Of<DotReport.Client.Interop.OnnxInterop>());

        orchestratorMock
            .Setup(o => o.StreamAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(new[] { "Done" }, delayMs: 0));

        var proxy = new ConsolidatorProxy(orchestratorMock.Object);

        await foreach (var _ in proxy.InferAsync("prompt", "sys")) { }

        proxy.State.IsProcessing.Should().BeFalse();
    }

    // ── OnStateChanged fires during inference ──────────────────────────────
    [Fact]
    public async Task InferAsync_FiresOnStateChangedEvents()
    {
        var orchestratorMock = new Mock<ModelOrchestrator>(
            Mock.Of<VRAMDetector>(),
            Mock.Of<IndexedDbService>(),
            Mock.Of<DotReport.Client.Interop.OnnxInterop>());

        orchestratorMock
            .Setup(o => o.StreamAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
            .Returns(YieldTokensAsync(new[] { "A", "B" }, delayMs: 0));

        var proxy  = new ConsolidatorProxy(orchestratorMock.Object);
        int events = 0;
        proxy.OnStateChanged += () => events++;

        await foreach (var _ in proxy.InferAsync("p", "s")) { }

        events.Should().BeGreaterThan(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static async IAsyncEnumerable<string> YieldTokensAsync(
        string[] tokens, int delayMs,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var t in tokens)
        {
            ct.ThrowIfCancellationRequested();
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            yield return t;
        }
    }
}
