using Microsoft.JSInterop;

namespace DotReport.Client.Interop;

/// <summary>
/// C# bridge to the browser's IndexedDB via JS interop.
/// Used by IndexedDbService to persist ONNX model segments across sessions. UAC 7.3.
/// </summary>
public sealed class IndexedDbInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>(
            "import", "./js/indexed-db.js");
        return _module;
    }

    public async Task PutAsync(string dbName, string storeName, string key, byte[] data)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("put", dbName, storeName, key, data);
    }

    public async Task<byte[]?> GetAsync(string dbName, string storeName, string key)
    {
        var m = await GetModuleAsync();
        return await m.InvokeAsync<byte[]?>("get", dbName, storeName, key);
    }

    public async Task<bool> ExistsAsync(string dbName, string storeName, string key)
    {
        var m = await GetModuleAsync();
        return await m.InvokeAsync<bool>("exists", dbName, storeName, key);
    }

    public async Task DeleteAsync(string dbName, string storeName, string key)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("remove", dbName, storeName, key);
    }

    public async Task ClearAsync(string dbName, string storeName)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("clear", dbName, storeName);
    }

    public async Task<long> GetStoreSizeAsync(string dbName, string storeName)
    {
        var m = await GetModuleAsync();
        return await m.InvokeAsync<long>("getStoreSize", dbName, storeName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
