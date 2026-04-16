using DotReport.Client.Interop;
using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Detects WebGPU support and available VRAM via JS interop.
/// Determines the correct build profile: Standard (≥4GB) or Lightweight (<4GB). UAC 7.1.
/// </summary>
public sealed class VRAMDetector(BabylonInterop babylon)
{
    private DeviceCapabilities? _cached;

    public async Task<DeviceCapabilities> DetectAsync()
    {
        if (_cached is not null) return _cached;
        _cached = await babylon.GetDeviceCapabilitiesAsync();
        return _cached;
    }

    public bool HasCachedResult => _cached is not null;
    public DeviceCapabilities? Cached => _cached;
}
