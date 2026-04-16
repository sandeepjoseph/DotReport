/**
 * webgpu-detector.js
 * Standalone WebGPU / device capability detection utility.
 * Used independently for capability checks before Babylon.js is initialized.
 * UAC 7.1
 */

export async function detectCapabilities() {
    const result = {
        webGpuSupported: false,
        webAssemblySupported: typeof WebAssembly !== 'undefined',
        estimatedVramMb: 0,
        adapterName: 'Unknown',
        gpuDescription: 'Unknown',
        fallbackReason: '',
        backendRecommendation: 'wasm'
    };

    if (!('gpu' in navigator)) {
        result.fallbackReason = 'WebGPU API not present in this browser. Using WASM fallback.';
        return result;
    }

    try {
        const adapter = await navigator.gpu.requestAdapter({
            powerPreference: 'high-performance'
        });

        if (!adapter) {
            result.fallbackReason = 'No high-performance WebGPU adapter available. Using WASM.';
            return result;
        }

        result.webGpuSupported = true;
        result.backendRecommendation = 'webgpu';

        const info = await adapter.requestAdapterInfo?.() ?? adapter.info ?? {};
        result.adapterName    = info.description ?? info.device ?? 'WebGPU Adapter';
        result.gpuDescription = info.vendor ?? info.architecture ?? 'Unknown Vendor';

        // Estimate VRAM
        const limits = adapter.limits;
        const rawMb  = Math.round((limits.maxBufferSize ?? 0) / (1024 * 1024));
        result.estimatedVramMb = rawMb === 0 ? 4096 : Math.min(rawMb, 24576);

        // Validate a device can actually be created
        const device = await adapter.requestDevice();
        device.destroy();

    } catch (err) {
        result.webGpuSupported = false;
        result.fallbackReason  = `WebGPU error: ${err.message}. Using WASM.`;
        result.backendRecommendation = 'wasm';
    }

    return result;
}
