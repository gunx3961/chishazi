using Chishazi.Models;
using Chishazi.Options;

namespace Chishazi.Services;

public sealed class SpreadsheetStore(
    BrowserCacheService cache,
    GoogleSheetsOptions options)
{
    private string CacheKey => $"spreadsheet:{options.SpreadsheetId}:snapshot:v1";
    private string BaselineKey => $"spreadsheet:{options.SpreadsheetId}:baseline:v1";

    public Task<SpreadsheetSnapshot?> GetAsync() =>
        GetValidSnapshotAsync(CacheKey);

    public Task<SpreadsheetSnapshot?> GetBaselineAsync() =>
        GetValidSnapshotAsync(BaselineKey);

    public Task SetAsync(SpreadsheetSnapshot snapshot) =>
        cache.SetAsync(CacheKey, snapshot);

    public Task SetBaselineAsync(SpreadsheetSnapshot snapshot) =>
        cache.SetAsync(BaselineKey, snapshot);

    public async Task<SpreadsheetSnapshot> SetSynchronizedAsync(
        SpreadsheetSnapshot remoteSnapshot)
    {
        await SetAsync(remoteSnapshot);
        await SetBaselineAsync(remoteSnapshot);
        return remoteSnapshot;
    }

    public async Task<SpreadsheetSnapshot?> DiscardLocalChangesAsync()
    {
        var baseline = await GetBaselineAsync();
        if (baseline is null)
        {
            return null;
        }

        await SetAsync(baseline);
        return baseline;
    }

    public async Task RemoveAsync()
    {
        await cache.RemoveAsync(CacheKey);
        await cache.RemoveAsync(BaselineKey);
    }

    private async Task<SpreadsheetSnapshot?> GetValidSnapshotAsync(string key)
    {
        var snapshot = await cache.GetAsync<SpreadsheetSnapshot>(key);
        return snapshot is not null &&
               snapshot.FormatVersion == SpreadsheetSnapshot.CurrentFormatVersion &&
               snapshot.SpreadsheetId == options.SpreadsheetId
            ? snapshot
            : null;
    }
}
