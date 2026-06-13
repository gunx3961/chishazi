using System.Text.Json;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class TagSheetParserTests
{
    private readonly TagSheetParser _parser = new();

    [Fact]
    public void Parse_MapsIdsAndDisplayNames()
    {
        var snapshot = CreateSnapshot(
            """
            [
              ["displayName", "id"],
              ["Quick", "tag-1"],
              ["Dinner", "tag-2"]
            ]
            """);

        var result = _parser.Parse(snapshot);

        Assert.Empty(result.Errors);
        Assert.Collection(
            result.Tags,
            tag =>
            {
                Assert.Equal(2, tag.RowNumber);
                Assert.Equal("tag-1", tag.Id);
                Assert.Equal("Quick", tag.DisplayName);
            },
            tag =>
            {
                Assert.Equal("tag-2", tag.Id);
                Assert.Equal("Dinner", tag.DisplayName);
            });
    }

    [Fact]
    public void Parse_ReportsMissingValuesAndDuplicateIds()
    {
        var snapshot = CreateSnapshot(
            """
            [
              ["id", "displayName"],
              ["", "Missing"],
              ["tag-1", ""],
              ["tag-2", "Dinner"],
              ["tag-2", "Second dinner"]
            ]
            """);

        var result = _parser.Parse(snapshot);

        Assert.Equal(2, result.Tags.Count);
        Assert.Equal(
            [
                "Row 2: Tag id and displayName are required.",
                "Row 3: Tag id and displayName are required.",
                "Duplicate Tag id: tag-2."
            ],
            result.Errors);
    }

    private static SpreadsheetSnapshot CreateSnapshot(string valuesJson)
    {
        var values = JsonSerializer.Deserialize<List<List<JsonElement>>>(valuesJson)
                     ?? throw new InvalidOperationException("Test JSON did not deserialize.");

        return new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
            [
                new WorksheetSnapshot(
                    2,
                    0,
                    "Tag",
                    "GRID",
                    values.Select(row => (IReadOnlyList<JsonElement>)row).ToList())
            ]);
    }
}
