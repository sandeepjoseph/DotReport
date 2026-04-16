using DotReport.Client.Models;
using Microsoft.JSInterop;

namespace DotReport.Client.Interop;

/// <summary>
/// C# bridge to the Babylon.js 3D engine.
/// Manages dodecahedron provisioning animation and drafting animations. UAC 7.3 / UAC 7.4.
/// </summary>
public sealed class BabylonInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>(
            "import", "./js/babylon-scene.js");
        return _module;
    }

    /// <summary>Mounts the Babylon.js canvas inside the given element ID.</summary>
    public async Task InitSceneAsync(string canvasId, string theme)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("initScene", canvasId, theme);
    }

    /// <summary>Begins the dodecahedron unfolding animation (pre-provisioning).</summary>
    public async Task StartUnfoldAsync()
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("startUnfold");
    }

    /// <summary>
    /// Snaps one face into place on the dodecahedron (called per downloaded segment).
    /// faceIndex: 1–12. When 12, triggers the "locked" animation. UAC 7.3.
    /// </summary>
    public async Task AssembleFaceAsync(int faceIndex)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("assembleFace", faceIndex);
    }

    /// <summary>Triggers the "locked & ready" animation when provisioning is complete.</summary>
    public async Task LockDodecahedronAsync()
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("lockDodecahedron");
    }

    /// <summary>Starts the "drafting lines" processing animation. UAC 7.4.</summary>
    public async Task StartDraftingAnimationAsync(string canvasId)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("startDraftingAnimation", canvasId);
    }

    /// <summary>Stops the drafting animation.</summary>
    public async Task StopDraftingAnimationAsync()
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("stopDraftingAnimation");
    }

    /// <summary>Switches the scene theme without reinitializing. UAC 7.4.</summary>
    public async Task SetThemeAsync(string theme)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("setTheme", theme);
    }

    /// <summary>Queries the WebGPU adapter for device capabilities. UAC 7.1.</summary>
    public async Task<DeviceCapabilities> GetDeviceCapabilitiesAsync()
    {
        var m = await GetModuleAsync();
        var raw = await m.InvokeAsync<DeviceCapabilitiesDto>("getDeviceCapabilities");
        return new DeviceCapabilities
        {
            WebGpuSupported     = raw.WebGpuSupported,
            WebAssemblySupported = raw.WebAssemblySupported,
            EstimatedVramMb     = raw.EstimatedVramMb,
            GpuDescription      = raw.GpuDescription,
            AdapterName         = raw.AdapterName,
            FallbackReason      = raw.FallbackReason
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("disposeScene");
            await _module.DisposeAsync();
        }
    }

    // DTO for JS deserialization
    private sealed class DeviceCapabilitiesDto
    {
        public bool WebGpuSupported { get; set; }
        public bool WebAssemblySupported { get; set; }
        public long EstimatedVramMb { get; set; }
        public string GpuDescription { get; set; } = string.Empty;
        public string AdapterName { get; set; } = string.Empty;
        public string FallbackReason { get; set; } = string.Empty;
    }
}
