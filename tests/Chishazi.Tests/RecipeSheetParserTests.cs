using System.Text.Json;
using Chishazi.Localization;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class RecipeSheetParserTests
{
    private readonly RecipeSheetParser _parser = new(new TagSheetParser());

    [Fact]
    public void Parse_MapsRecipeFieldsAndTags()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["tags", "name", "description"],
              ["tag-quick, tag-breakfast, TAG-QUICK", "Egg sandwich", "Eggs on toasted bread"],
              ["tag-dinner, tag-quick", "Fried rice", "Uses leftover rice"]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Errors);
        Assert.Collection(
            result.Recipes,
            sandwich =>
            {
                Assert.Equal(2, sandwich.RowNumber);
                Assert.Equal("Egg sandwich", sandwich.Name);
                Assert.Equal("Eggs on toasted bread", sandwich.Description);
                Assert.Equal(["tag-quick", "tag-breakfast"], sandwich.Tags);
            },
            friedRice =>
            {
                Assert.Equal(3, friedRice.RowNumber);
                Assert.Equal("Fried rice", friedRice.Name);
                Assert.Equal(["tag-dinner", "tag-quick"], friedRice.Tags);
            });
    }

    [Fact]
    public void Parse_SkipsRowsWithoutNames()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["name", "description", "tags"],
              ["", "Missing name", "invalid"],
              ["Valid recipe", "", ""]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        var recipe = Assert.Single(result.Recipes);
        Assert.Equal("Valid recipe", recipe.Name);
        Assert.Equal(UiText.Get("RowNameRequired", 2), Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_RequiresNameHeader()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["description", "tags"],
              ["Something", "quick"]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Recipes);
        Assert.Equal(
            UiText.Get("RequiredColumnsMissing", "name"),
            Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ReportsDuplicateNamesCaseInsensitively()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["name"],
              ["Apple"],
              ["apple"]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Equal(2, result.Recipes.Count);
        Assert.Equal(
            UiText.Get("DuplicateRecipeName", "Apple"),
            Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ReportsTagsOutsideTheDefinition()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["name", "tags"],
              ["Rice bowl", "tag-dinner, custom"]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Equal(["tag-dinner", "custom"], Assert.Single(result.Recipes).Tags);
        Assert.Equal(
            UiText.Get("UnknownRecipeTag", 2, "custom"),
            Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_TreatsMissingDefinedWorksheetAsEmpty()
    {
        var spreadsheet = CreateSpreadsheet("""[["name"],["Egg"]]""", "Other");

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Recipes);
        Assert.Empty(result.Errors);
    }

    private static SpreadsheetSnapshot CreateSpreadsheet(
        string valuesJson,
        string worksheetName = "Recipe")
    {
        var values = JsonSerializer.Deserialize<List<List<JsonElement>>>(valuesJson)
                     ?? throw new InvalidOperationException("Test JSON did not deserialize.");

        return new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test spreadsheet",
            DateTimeOffset.Parse("2026-06-11T00:00:00Z"),
            [
                new WorksheetSnapshot(
                    1,
                    0,
                    worksheetName,
                    "GRID",
                    values.Select(row => (IReadOnlyList<JsonElement>)row).ToList()),
                new WorksheetSnapshot(
                    2,
                    1,
                    "Tag",
                    "GRID",
                    DeserializeValues(
                        """
                        [
                          ["id", "displayName"],
                          ["tag-breakfast", "Breakfast"],
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
