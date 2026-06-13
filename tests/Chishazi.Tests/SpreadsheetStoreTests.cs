using System.Text.Json;
using Chishazi.Models;
using Chishazi.Options;
using Chishazi.Services;
using Microsoft.JSInterop;

namespace Chishazi.Tests;

public sealed class SpreadsheetStoreTests
{
    [Fact]
    public async Task GetAsync_DoesNotCreateMissingWorksheets()
    {
        var store = CreateStore();
        var snapshot = CreateSnapshot([]);
        await store.SetAsync(snapshot);

        var restored = await store.GetAsync();

        Assert.NotNull(restored);
        Assert.Empty(restored.Worksheets);
    }

    [Fact]
    public async Task DiscardLocalChangesAsync_RestoresBaseline()
    {
        var store = CreateStore();
        var baseline = CreateSnapshot([]);
        var working = CreateSnapshot(
            [
                new WorksheetSnapshot(
                    -1,
                    0,
                    "Tag",
                    "GRID",
                    [
                        [
                            JsonSerializer.SerializeToElement("id"),
                            JsonSerializer.SerializeToElement("displayName")
                        ]
                    ])
            ]);
        await store.SetBaselineAsync(baseline);
        await store.SetAsync(working);

        var restored = await store.DiscardLocalChangesAsync();

        AssertSnapshotEqual(baseline, restored);
        AssertSnapshotEqual(baseline, await store.GetAsync());
        AssertSnapshotEqual(baseline, await store.GetBaselineAsync());
    }

    private static SpreadsheetStore CreateStore() =>
        new(
            new BrowserCacheService(new CacheJsRuntime()),
            new GoogleSheetsOptions
            {
                ClientId = "client-id",
                SpreadsheetId = "spreadsheet-id"
            });

    private static SpreadsheetSnapshot CreateSnapshot(
        IReadOnlyList<WorksheetSnapshot> worksheets) =>
        new(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
            worksheets);

    private static void AssertSnapshotEqual(
        SpreadsheetSnapshot expected,
        SpreadsheetSnapshot? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(
            JsonSerializer.Serialize(expected),
            JsonSerializer.Serialize(actual));
    }

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
                "chishaziCache.remove" => _values.Remove(key),
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
    }
}
