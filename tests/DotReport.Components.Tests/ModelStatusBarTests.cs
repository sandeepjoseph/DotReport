using Bunit;
using DotReport.Client.Components;
using DotReport.Client.Models;
using DotReport.Client.Services;
using DotReport.Client.Interop;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DotReport.Components.Tests;

/// <summary>
/// bUnit tests for the ModelStatusBar component.
/// Validates LED colour states per model status — visual system health indicator.
/// </summary>
public sealed class ModelStatusBarTests : TestContext
{
    private ConsolidatorProxy BuildProxy(ModelStatus primary, ModelStatus backup)
    {
        var orchMock = new Mock<ModelOrchestrator>(
            Mock.Of<VRAMDetector>(),
            Mock.Of<IndexedDbService>(),
            Mock.Of<OnnxInterop>());

        var proxy = new ConsolidatorProxy(orchMock.Object);
        proxy.State.PrimaryStatus = primary;
        proxy.State.BackupStatus  = backup;
        return proxy;
    }

    [Fact]
    public void ModelStatusBar_BothReady_BothIndicatorsHaveReadyClass()
    {
        var proxy = BuildProxy(ModelStatus.Ready, ModelStatus.Ready);
        Services.AddSingleton(proxy);

        var cut = RenderComponent<ModelStatusBar>();

        var indicators = cut.FindAll(".ec-model-indicator");
        indicators[0].ClassList.Should().Contain("ec-model-indicator--ready");
        indicators[1].ClassList.Should().Contain("ec-model-indicator--ready");
    }

    [Fact]
    public void ModelStatusBar_PrimaryLoading_PrimaryHasLoadingClass()
    {
        var proxy = BuildProxy(ModelStatus.Loading, ModelStatus.Ready);
        Services.AddSingleton(proxy);

        var cut = RenderComponent<ModelStatusBar>();

        cut.FindAll(".ec-model-indicator")[0]
           .ClassList.Should().Contain("ec-model-indicator--loading");
    }

    [Fact]
    public void ModelStatusBar_PrimaryError_PrimaryHasErrorClass()
    {
        var proxy = BuildProxy(ModelStatus.Error, ModelStatus.Ready);
        Services.AddSingleton(proxy);

        var cut = RenderComponent<ModelStatusBar>();

        cut.FindAll(".ec-model-indicator")[0]
           .ClassList.Should().Contain("ec-model-indicator--error");
    }

    [Fact]
    public void ModelStatusBar_WhileProcessing_ShowsInfIndicator()
    {
        var proxy = BuildProxy(ModelStatus.Ready, ModelStatus.Ready);
        proxy.State.IsProcessing = true;
        Services.AddSingleton(proxy);

        var cut = RenderComponent<ModelStatusBar>();

        cut.FindAll(".ec-model-indicator").Should().HaveCount(3); // P + B + INF
    }

    [Fact]
    public void ModelStatusBar_NotProcessing_NoInfIndicator()
    {
        var proxy = BuildProxy(ModelStatus.Ready, ModelStatus.Ready);
        proxy.State.IsProcessing = false;
        Services.AddSingleton(proxy);

        var cut = RenderComponent<ModelStatusBar>();

        cut.FindAll(".ec-model-indicator").Should().HaveCount(2); // P + B only
    }
}
