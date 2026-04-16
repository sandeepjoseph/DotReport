using DotReport.Client.Interop;

namespace DotReport.Client.Services;

/// <summary>
/// Wraps the browser's IndexedDB for persistent model segment storage.
/// Models survive page refreshes — provisioning only happens once per device. UAC 7.3.
/// </summary>
public sealed class IndexedDbService(IndexedDbInterop interop)
{
    private const string DbName    = "dotreport-edgecore";
    private const string StoreName = "model-segments";

    public Task StoreAsync(string key, byte[] data)
        => interop.PutAsync(DbName, StoreName, key, data);

    public Task<byte[]?> GetAsync(string key)
        => interop.GetAsync(DbName, StoreName, key);

    public Task<bool> ExistsAsync(string key)
        => interop.ExistsAsync(DbName, StoreName, key);

    public Task DeleteAsync(string key)
        => interop.DeleteAsync(DbName, StoreName, key);

    public Task ClearStoreAsync()
        => interop.ClearAsync(DbName, StoreName);

    public Task<long> GetStoreSizeBytesAsync()
        => interop.GetStoreSizeAsync(DbName, StoreName);
}
