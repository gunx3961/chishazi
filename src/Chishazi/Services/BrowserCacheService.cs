using System.Text.Json;
using Microsoft.JSInterop;

namespace Chishazi.Services;

public sealed class BrowserCacheService(IJSRuntime jsRuntime)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string?>(
            "chishaziCache.get",
            cancellationToken,
            key);

        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await jsRuntime.InvokeVoidAsync(
            "chishaziCache.set",
            cancellationToken,
            key,
            json);
    }

    public ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        jsRuntime.InvokeVoidAsync("chishaziCache.remove", cancellationToken, key);
}
