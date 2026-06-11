using System.Text.Json;
using Chishazi.Services;
using Microsoft.JSInterop;

namespace Chishazi.Tests;

public sealed class BrowserCacheServiceTests
{
    [Fact]
    public async Task Cache_RoundTripsArbitraryJsonData()
    {
        var jsRuntime = new CacheJsRuntime();
        var cache = new BrowserCacheService(jsRuntime);
        var value = new ArbitraryPayload(
            "any-data",
            42,
            new Dictionary<string, bool> { ["enabled"] = true });

        await cache.SetAsync("arbitrary-key", value);
        var restored = await cache.GetAsync<ArbitraryPayload>("arbitrary-key");

        Assert.NotNull(restored);
        Assert.Equal(value.Name, restored.Name);
        Assert.Equal(value.Count, restored.Count);
        Assert.Equal(value.Flags, restored.Flags);
    }

    [Fact]
    public async Task Remove_DeletesCachedValue()
    {
        var jsRuntime = new CacheJsRuntime();
        var cache = new BrowserCacheService(jsRuntime);

        await cache.SetAsync(
            "key",
            new ArbitraryPayload("value", 1, new Dictionary<string, bool>()));
        await cache.RemoveAsync("key");

        Assert.Null(await cache.GetAsync<ArbitraryPayload>("key"));
    }

    private sealed record ArbitraryPayload(
        string Name,
        int Count,
        IReadOnlyDictionary<string, bool> Flags);

    private sealed class CacheJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _values = [];

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            var key = (string?)args?[0]
                      ?? throw new InvalidOperationException("A cache key is required.");

            object? result = identifier switch
            {
                "chishaziCache.get" => _values.GetValueOrDefault(key),
                "chishaziCache.set" => Set(key, (string?)args?[1]),
                "chishaziCache.remove" => Remove(key),
                _ => throw new InvalidOperationException($"Unexpected JS call: {identifier}")
            };

            return ValueTask.FromResult(
                result is null
                    ? default!
                    : (TValue)result);
        }

        private object? Set(string key, string? json)
        {
            _values[key] = json ?? throw new JsonException("Cache JSON is required.");
            return null;
        }

        private object? Remove(string key)
        {
            _values.Remove(key);
            return null;
        }
    }
}
