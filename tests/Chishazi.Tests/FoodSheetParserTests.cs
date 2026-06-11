using System.Text.Json;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class FoodSheetParserTests
{
    private readonly FoodSheetParser _parser = new();

    [Fact]
    public void Parse_MapsHeadersAndNumericValues()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["category", "name", "protein_g", "calories_kcal", "serving"],
              ["Eggs", "Egg", 13.3, 144, "100 g"],
              ["Staples", "Cooked rice", "2.6", "116", "100 g"]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Errors);
        Assert.Collection(
            result.Foods,
            egg =>
            {
                Assert.Equal("Egg", egg.Name);
                Assert.Equal("Eggs", egg.Category);
                Assert.Equal(144m, egg.CaloriesKcal);
                Assert.Equal(13.3m, egg.ProteinG);
                Assert.Null(egg.CarbsG);
            },
            rice =>
            {
                Assert.Equal("Cooked rice", rice.Name);
                Assert.Equal(116m, rice.CaloriesKcal);
                Assert.Equal(2.6m, rice.ProteinG);
            });
    }

    [Fact]
    public void Parse_SkipsInvalidRowsAndReportsSpecificErrors()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["name", "calories_kcal", "protein_g"],
              ["", 50, 1],
              ["Invalid number", "many", 1],
              ["Negative value", 50, -1],
              ["Valid food", 80, 4]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        var food = Assert.Single(result.Foods);
        Assert.Equal("Valid food", food.Name);
        Assert.Equal(
            [
                "Row 2: name is required.",
                "Row 3: calories_kcal must be a number.",
                "Row 4: protein_g cannot be negative."
            ],
            result.Errors);
    }

    [Fact]
    public void Parse_RequiresNameHeader()
    {
        var spreadsheet = CreateSpreadsheet(
            """
            [
              ["category", "calories_kcal"],
              ["Fruit", 52]
            ]
            """);

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Foods);
        Assert.Equal(
            "The header row is missing required column(s): name.",
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

        Assert.Equal(2, result.Foods.Count);
        Assert.Equal("Duplicate food name: Apple.", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ReportsMissingDefinedWorksheet()
    {
        var spreadsheet = CreateSpreadsheet("""[["name"],["Egg"]]""", "Other");

        var result = _parser.Parse(spreadsheet);

        Assert.Empty(result.Foods);
        Assert.Equal(
            "The spreadsheet does not contain the 'Foods' worksheet.",
            Assert.Single(result.Errors));
    }

    private static SpreadsheetSnapshot CreateSpreadsheet(
        string valuesJson,
        string worksheetName = "Foods")
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
                    values.Select(row => (IReadOnlyList<JsonElement>)row).ToList())
            ]);
    }
}
