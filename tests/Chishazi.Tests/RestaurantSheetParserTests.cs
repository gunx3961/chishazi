using System.Text.Json;
using Chishazi.Localization;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class RestaurantSheetParserTests
{
    private readonly RestaurantSheetParser _parser = new(new TagSheetParser());

    [Fact]
    public void Parse_MapsRestaurantFieldsAndTags()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["location", "tags", "name", "description"],
              ["Xuhui", "tag-dinner, tag-quick, TAG-DINNER", "Noodle House", "Late hours"]
            ]
            """);

        var restaurant = Assert.Single(_parser.Parse(spreadsheet).Restaurants);

        Assert.Equal(2, restaurant.RowNumber);
        Assert.Equal("Noodle House", restaurant.Name);
        Assert.Equal("Late hours", restaurant.Description);
        Assert.Equal(["tag-dinner", "tag-quick"], restaurant.Tags);
        Assert.Equal("Xuhui", restaurant.Location);
    }

    [Fact]
    public void Parse_ReportsInvalidNamesAndUnknownTags()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["name", "tags", "description"],
              ["", "", "Missing name"],
              ["Cafe", "missing", ""],
              ["cafe", "", ""]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Equal(2, result.Restaurants.Count);
        Assert.Contains(UiText.Get("RestaurantRowNameRequired", 2), result.Errors);
        Assert.Contains(UiText.Get("UnknownRestaurantTag", 3, "missing"), result.Errors);
        Assert.Contains(UiText.Get("DuplicateRestaurantName", "Cafe"), result.Errors);
    }

    [Fact]
    public void Parse_RequiresNameHeader()
    {
        var spreadsheet = CreateSpreadsheet(
            """[["description","location"],["Late hours","Xuhui"]]""");

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Restaurants);
        Assert.Equal(
            UiText.Get("RequiredColumnsMissing", "name"),
            Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_TreatsMissingDefinedWorksheetAsEmpty()
    {
        var result = _parser.Parse(
            CreateSpreadsheet("""[["name"],["Cafe"]]""", "Other"));

        Assert.Empty(result.Restaurants);
        Assert.Empty(result.Errors);
    }

    private static SpreadsheetSnapshot CreateSpreadsheet(
        string valuesJson,
        string worksheetName = "Restaurant")
    {
        var values = DeserializeValues(valuesJson);

        return new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            [
                new WorksheetSnapshot(1, 0, worksheetName, "GRID", values),
                new WorksheetSnapshot(
                    2,
                    1,
                    "Tag",
                    "GRID",
                    DeserializeValues(
                        """
                        [
                          ["id", "displayName"],
                          ["tag-dinner", "Dinner"],
                          ["tag-quick", "Quick"]
                        ]
                        """))
            ]);
    }

    private static IReadOnlyList<IReadOnlyList<JsonElement>> DeserializeValues(
        string valuesJson) =>
        JsonSerializer.Deserialize<List<List<JsonElement>>>(valuesJson)?
            .Select(row => (IReadOnlyList<JsonElement>)row)
            .ToList()
        ?? throw new InvalidOperationException("Test JSON did not deserialize.");
}
