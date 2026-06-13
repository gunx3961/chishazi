using System.Text.Json;
using Chishazi.DataDefinitions;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class SpreadsheetMutationServiceTests
{
    private readonly SpreadsheetMutationService _service = new();

    [Fact]
    public void AppendRows_UsesWorksheetHeadersAndPreservesUnknownColumns()
    {
        var snapshot = CreateSnapshot(
            """
            [
              ["extra", "tags", "name", "description"],
              ["keep", "dinner", "Existing", "Current"]
            ]
            """);
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows =
        [
            new Dictionary<string, string>
            {
                ["name"] = "First",
                ["description"] = "One",
                ["tags"] = "quick,dinner"
            },
            new Dictionary<string, string>
            {
                ["name"] = "Second",
                ["description"] = "Two",
                ["tags"] = "lunch"
            }
        ];

        var updated = _service.AppendRows(
            snapshot,
            SpreadsheetDefinition.Recipe,
            rows);

        var values = Assert.Single(updated.Worksheets).Values;
        Assert.Equal(4, values.Count);
        Assert.Equal(string.Empty, values[2][0].GetString());
        Assert.Equal("quick,dinner", values[2][1].GetString());
        Assert.Equal("First", values[2][2].GetString());
        Assert.Equal("Two", values[3][3].GetString());
    }

    [Fact]
    public void AppendRows_DoesNotMutateOriginalSnapshot()
    {
        var snapshot = CreateSnapshot("""[["name","description","tags"]]""");

        var updated = _service.AppendRows(
            snapshot,
            SpreadsheetDefinition.Recipe,
            [
                new Dictionary<string, string>
                {
                    ["name"] = "Added",
                    ["description"] = string.Empty,
                    ["tags"] = string.Empty
                }
            ]);

        Assert.Single(snapshot.Worksheets[0].Values);
        Assert.Equal(2, updated.Worksheets[0].Values.Count);
    }

    [Fact]
    public void AppendRows_CreatesOnlyTheRequestedMissingWorksheet()
    {
        var snapshot = new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
            []);

        var updated = _service.AppendRows(
            snapshot,
            SpreadsheetDefinition.Tag,
            [
                new Dictionary<string, string>
                {
                    ["id"] = "tag-1",
                    ["displayName"] = "Quick"
                }
            ]);

        var worksheet = Assert.Single(updated.Worksheets);
        Assert.Equal("Tag", worksheet.Name);
        Assert.Equal(["id", "displayName"], worksheet.Values[0]
            .Select(value => value.GetString()));
        Assert.Equal("tag-1", worksheet.Values[1][0].GetString());
        Assert.Equal("Quick", worksheet.Values[1][1].GetString());
    }

    [Fact]
    public void UpdateRow_ChangesNamedColumnsOnly()
    {
        var snapshot = CreateSnapshot(
            """[["id","displayName"],["tag-1","Quick"]]""",
            "Tag");

        var updated = _service.UpdateRow(
            snapshot,
            SpreadsheetDefinition.Tag,
            2,
            new Dictionary<string, string> { ["displayName"] = "Fast" });

        Assert.Equal("tag-1", updated.Worksheets[0].Values[1][0].GetString());
        Assert.Equal("Fast", updated.Worksheets[0].Values[1][1].GetString());
        Assert.Equal("Quick", snapshot.Worksheets[0].Values[1][1].GetString());
    }

    [Fact]
    public void UpdateRow_ChangesRecipeFieldsAndPreservesUnknownColumns()
    {
        var snapshot = CreateSnapshot(
            """
            [
              ["extra", "tags", "name", "description"],
              ["keep", "tag-old", "Old name", "Old description"]
            ]
            """);

        var updated = _service.UpdateRow(
            snapshot,
            SpreadsheetDefinition.Recipe,
            2,
            new Dictionary<string, string>
            {
                ["name"] = "New name",
                ["description"] = "New description",
                ["tags"] = "tag-new"
            });

        Assert.Equal("keep", updated.Worksheets[0].Values[1][0].GetString());
        Assert.Equal("tag-new", updated.Worksheets[0].Values[1][1].GetString());
        Assert.Equal("New name", updated.Worksheets[0].Values[1][2].GetString());
        Assert.Equal("New description", updated.Worksheets[0].Values[1][3].GetString());
    }

    [Fact]
    public void AppendRows_RefreshesEmptyTemporaryHeaders()
    {
        var snapshot = CreateSnapshot(
            """[["value","name","active"]]""",
            "Tag") with
        {
            Worksheets =
            [
                CreateSnapshot(
                    """[["value","name","active"]]""",
                    "Tag").Worksheets[0] with
                {
                    SheetId = -1
                }
            ]
        };

        var updated = _service.AppendRows(
            snapshot,
            SpreadsheetDefinition.Tag,
            [
                new Dictionary<string, string>
                {
                    ["id"] = "tag-1",
                    ["displayName"] = "Quick"
                }
            ]);

        Assert.Equal(
            ["id", "displayName"],
            updated.Worksheets[0].Values[0].Select(value => value.GetString()));
        Assert.Equal("tag-1", updated.Worksheets[0].Values[1][0].GetString());
    }

    private static SpreadsheetSnapshot CreateSnapshot(
        string valuesJson,
        string worksheetName = "Recipe")
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
                    1,
                    0,
                    worksheetName,
                    "GRID",
                    values.Select(row => (IReadOnlyList<JsonElement>)row).ToList())
            ]);
    }
}
