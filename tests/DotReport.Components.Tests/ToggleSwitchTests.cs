using Bunit;
using DotReport.Client.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DotReport.Components.Tests;

/// <summary>
/// bUnit tests for the ToggleSwitch component.
/// Verifies the "chunky 1970s lab equipment" toggle physical behaviour (UAC 7.4).
/// </summary>
public sealed class ToggleSwitchTests : TestContext
{
    [Fact]
    public void ToggleSwitch_DefaultState_RendersOffLabel()
    {
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "CONCURRENT INFERENCE")
             .Add(c => c.Value, false));

        cut.Find(".ec-phys-toggle__state").TextContent.Should().Be("OFF");
    }

    [Fact]
    public void ToggleSwitch_WhenValueTrue_RendersOnLabel()
    {
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "TOKEN STREAMING")
             .Add(c => c.Value, true));

        cut.Find(".ec-phys-toggle__state").TextContent.Should().Be("ON");
    }

    [Fact]
    public void ToggleSwitch_Clicked_TogglesValue()
    {
        bool? received = null;
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "TEST")
             .Add(c => c.Value, false)
             .Add(c => c.ValueChanged, EventCallback.Factory.Create<bool>(this, v => received = v)));

        cut.Find(".ec-phys-toggle").Click();

        received.Should().BeTrue();
    }

    [Fact]
    public void ToggleSwitch_OnStateHasBodyOnClass()
    {
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "TEST")
             .Add(c => c.Value, true));

        cut.Find(".ec-phys-toggle__body")
           .ClassList.Should().Contain("ec-phys-toggle__body--on");
    }

    [Fact]
    public void ToggleSwitch_OffStateHasBodyOffClass()
    {
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "TEST")
             .Add(c => c.Value, false));

        cut.Find(".ec-phys-toggle__body")
           .ClassList.Should().Contain("ec-phys-toggle__body--off");
    }

    [Fact]
    public void ToggleSwitch_RendersLabelText()
    {
        var cut = RenderComponent<ToggleSwitch>(p =>
            p.Add(c => c.Label, "VRAM LOCK")
             .Add(c => c.Value, false));

        cut.Find(".ec-phys-toggle__label").TextContent.Should().Be("VRAM LOCK");
    }
}
