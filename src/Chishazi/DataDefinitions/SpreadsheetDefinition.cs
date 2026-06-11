namespace Chishazi.DataDefinitions;

public enum WorksheetColumnType
{
    Text,
    Decimal
}

public sealed record WorksheetColumnDefinition(
    string Name,
    WorksheetColumnType Type,
    bool Required = false,
    bool NonNegative = false);

public sealed record WorksheetDefinition(
    string Name,
    IReadOnlyList<WorksheetColumnDefinition> Columns);

public static class SpreadsheetDefinition
{
    public static WorksheetDefinition Foods { get; } = new(
        "Foods",
        [
            new("name", WorksheetColumnType.Text, Required: true),
            new("category", WorksheetColumnType.Text),
            new("calories_kcal", WorksheetColumnType.Decimal, NonNegative: true),
            new("protein_g", WorksheetColumnType.Decimal, NonNegative: true),
            new("carbs_g", WorksheetColumnType.Decimal, NonNegative: true),
            new("fat_g", WorksheetColumnType.Decimal, NonNegative: true),
            new("serving", WorksheetColumnType.Text)
        ]);

    public static IReadOnlyList<WorksheetDefinition> Worksheets { get; } = [Foods];
}
