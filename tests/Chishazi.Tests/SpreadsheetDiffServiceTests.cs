using System.Text.Json;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class SpreadsheetDiffServiceTests
{
    private readonly SpreadsheetDiffService _service = new();

    [Fact]
    public void Compare_ReportsAddedModifiedAndClearedCells()
    {
        var local = CreateSnapshot(
            """[["name","description"],["Rice","New"],["Soup"]]""");
        var remote = CreateSnapshot(
            """[["name","description"],["Rice","Old"],["Soup","Remove"]]""");

        var result = _service.Compare(local, remote);

        Assert.True(result.CanUpload);
        Assert.Collection(
            result.Changes,
            change =>
            {
                Assert.Equal(SpreadsheetCellChangeKind.Modified, change.Kind);
                Assert.Equal(2, change.RowNumber);
                Assert.Equal(2, change.ColumnNumber);
                Assert.Equal("description", change.ColumnName);
                Assert.Equal("Old", change.Before);
                Assert.Equal("New", change.After);
            },
            change =>
            {
                Assert.Equal(SpreadsheetCellChangeKind.Cleared, change.Kind);
                Assert.Equal(3, change.RowNumber);
                Assert.Equal(2, change.ColumnNumber);
            });
    }

    [Fact]
    public void Compare_BlocksWorksheetStructureChanges()
    {
        var local = CreateSnapshot("""[["name"],["Rice"]]""");
        var remote = CreateSnapshot(
            """[["name"],["Rice"]]""",
            worksheetName: "Renamed");

        var result = _service.Compare(local, remote);

        Assert.False(result.CanUpload);
        Assert.Empty(result.Changes);
        Assert.Equal(
            "Worksheet ID 1 was renamed from 'Recipe' to 'Renamed'.",
            Assert.Single(result.BlockingIssues));
    }

    [Fact]
    public void Compare_TreatsTemporaryWorksheetAsCreation()
    {
        var local = CreateSnapshot(
            """[["name","description","tags"],["Rice","","quick"]]""") with
        {
            Worksheets =
            [
                CreateSnapshot(
                    """[["name","description","tags"],["Rice","","quick"]]""")
                    .Worksheets[0] with
                {
                    SheetId = -1
                }
            ]
        };
        var remote = local with { Worksheets = [] };

        var result = _service.Compare(local, remote);

        Assert.True(result.CanUpload);
        Assert.Equal(
            new SpreadsheetWorksheetCreation(-1, "Recipe"),
            Assert.Single(result.WorksheetCreations));
        Assert.Equal(5, result.Changes.Count);
        var rowChange = Assert.Single(result.RowChanges);
        Assert.Equal(2, rowChange.RowNumber);
        Assert.Equal(2, rowChange.Fields.Count);
        Assert.Equal(SpreadsheetCellChangeKind.Added, rowChange.Kind);
    }

    [Fact]
    public void Compare_GroupsChangedFieldsByRow()
    {
        var local = CreateSnapshot(
            """[["name","description","tags"],["Rice","New","quick"]]""");
        var remote = CreateSnapshot(
            """[["name","description","tags"],["Rice","Old","dinner"]]""");

        var result = _service.Compare(local, remote);

        var row = Assert.Single(result.RowChanges);
        Assert.Equal("Recipe", row.WorksheetName);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(SpreadsheetCellChangeKind.Modified, row.Kind);
        Assert.Collection(
            row.Fields,
            field =>
            {
                Assert.Equal("description", field.ColumnName);
                Assert.Equal("Old", field.Before);
                Assert.Equal("New", field.After);
            },
            field =>
            {
                Assert.Equal("tags", field.ColumnName);
                Assert.Equal("dinner", field.Before);
                Assert.Equal("quick", field.After);
            });
    }

    [Fact]
    public void PrepareUpload_IgnoresUnrelatedRemoteWorksheetChanges()
    {
        var baseline = CreateSnapshot(
            """[["name","description"],["Pork","Original"]]""",
            worksheetName: "Foods");
        var local = baseline with
        {
            Worksheets =
            [
                baseline.Worksheets[0],
                CreateWorksheet(
                    -1,
                    1,
                    "Tag",
                    """
                    [
                      ["id", "displayName"],
                      ["tag-1", "Meat"],
                      ["tag-2", "Vegetable"],
                      ["tag-3", "Pork"]
                    ]
                    """)
            ]
        };
        var remote = baseline with
        {
            Worksheets =
            [
                CreateWorksheet(
                    1,
                    0,
                    "Foods",
                    """[["name","description"],["Pork","Changed remotely"]]""")
            ]
        };

        var result = _service.PrepareUpload(local, baseline, remote);

        Assert.True(result.CanUpload);
        Assert.Equal("Tag", Assert.Single(result.WorksheetCreations).WorksheetName);
        Assert.Equal(3, result.RowChanges.Count);
        Assert.All(result.Changes, change => Assert.Equal("Tag", change.WorksheetName));
        Assert.Equal(4, result.DisplayChangeCount);
    }

    [Fact]
    public void PrepareUpload_BlocksRemoteChangesToIntendedCells()
    {
        var baseline = CreateSnapshot(
            """[["name","description"],["Rice","Original"]]""");
        var local = CreateSnapshot(
            """[["name","description"],["Rice","Local edit"]]""");
        var remote = CreateSnapshot(
            """[["name","description"],["Rice","Remote edit"]]""");

        var result = _service.PrepareUpload(local, baseline, remote);

        Assert.False(result.CanUpload);
        Assert.Equal(
            "The upload target 'Recipe', row 2, field 'description' changed remotely " +
            "after the last synchronization.",
            Assert.Single(result.BlockingIssues));
    }

    [Fact]
    public void PrepareUpload_SkipsChangesAlreadyAppliedRemotely()
    {
        var baseline = CreateSnapshot(
            """[["name","description"],["Rice","Original"]]""");
        var local = CreateSnapshot(
            """
            [
              ["name","description"],
              ["Rice","Edited"],
              ["Soup","Added"]
            ]
            """);
        var remote = CreateSnapshot(
            """[["name","description"],["Rice","Edited"]]""");

        var result = _service.PrepareUpload(local, baseline, remote);

        Assert.True(result.CanUpload);
        Assert.Empty(result.BlockingIssues);
        var row = Assert.Single(result.RowChanges);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal(SpreadsheetCellChangeKind.Added, row.Kind);
        Assert.Equal(2, row.Fields.Count);
    }

    [Fact]
    public void PrepareUpload_TreatsEquivalentJsonStringEncodingsAsEqual()
    {
        var baseline = CreateSnapshotWithDescription(
            JsonDocument.Parse("\"\\u539f\\u59cb\"").RootElement.Clone());
        var local = CreateSnapshotWithDescription(
            JsonSerializer.SerializeToElement("Local edit"));
        var remote = CreateSnapshotWithDescription(
            JsonDocument.Parse("\"" + "\u539f\u59cb" + "\"").RootElement.Clone());

        var result = _service.PrepareUpload(local, baseline, remote);

        Assert.True(result.CanUpload);
        Assert.Empty(result.BlockingIssues);
        var change = Assert.Single(result.Changes);
        Assert.Equal("description", change.ColumnName);
        Assert.Equal("Local edit", change.After);
    }

    [Fact]
    public void Compare_BlocksTemporaryWorksheetNameConflict()
    {
        var local = CreateSnapshot("""[["name"]]""") with
        {
            Worksheets =
            [
                CreateSnapshot("""[["name"]]""").Worksheets[0] with
                {
                    SheetId = -1
                }
            ]
        };
        var remote = CreateSnapshot("""[["name"]]""") with
        {
            Worksheets =
            [
                CreateSnapshot("""[["name"]]""").Worksheets[0] with
                {
                    SheetId = 7
                }
            ]
        };

        var result = _service.Compare(local, remote);

        Assert.False(result.CanUpload);
        Assert.Contains(
            "A remote worksheet named 'Recipe' now exists with a different identity.",
            Assert.Single(result.BlockingIssues));
    }

    [Fact]
    public void BlockRemoteFormulaOverwrites_RejectsChangedFormulaCells()
    {
        var local = CreateSnapshot("""[["value"],[2]]""");
        var remote = CreateSnapshot("""[["value"],[3]]""");
        var formulaView = CreateSnapshot("""[["value"],["=1+2"]]""");

        var result = _service.BlockRemoteFormulaOverwrites(
            _service.Compare(local, remote),
            formulaView);

        Assert.False(result.CanUpload);
        Assert.Equal(
            "Upload would overwrite a formula in 'Recipe' at row 2, column 1. " +
            "Edit that cell in Google Sheets or pull the current result instead.",
            Assert.Single(result.BlockingIssues));
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
                CreateWorksheet(1, 0, worksheetName, values)
            ]);
    }

    private static SpreadsheetSnapshot CreateSnapshotWithDescription(
        JsonElement description) =>
        new(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
            [
                new WorksheetSnapshot(
                    1,
                    0,
                    "Recipe",
                    "GRID",
                    [
                        [
                            JsonSerializer.SerializeToElement("name"),
                            JsonSerializer.SerializeToElement("description")
                        ],
                        [
                            JsonSerializer.SerializeToElement("Rice"),
                            description
                        ]
                    ])
            ]);

    private static WorksheetSnapshot CreateWorksheet(
        int sheetId,
        int index,
        string name,
        string valuesJson) =>
        CreateWorksheet(
            sheetId,
            index,
            name,
            JsonSerializer.Deserialize<List<List<JsonElement>>>(valuesJson)
            ?? throw new InvalidOperationException("Test JSON did not deserialize."));

    private static WorksheetSnapshot CreateWorksheet(
        int sheetId,
        int index,
        string name,
        IReadOnlyList<List<JsonElement>> values) =>
        new(
            sheetId,
            index,
            name,
            "GRID",
            values.Select(row => (IReadOnlyList<JsonElement>)row).ToList());
}
