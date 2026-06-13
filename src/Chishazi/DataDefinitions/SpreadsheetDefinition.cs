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
    bool NonNegative = false,
    bool MultipleValues = false,
    string ValueSeparator = ",");

public sealed record WorksheetDefinition(
    string Name,
    IReadOnlyList<WorksheetColumnDefinition> Columns);

public static class SpreadsheetDefinition
{
    public static WorksheetDefinition Recipe { get; } = new(
        "Recipe",
        [
            new("name", WorksheetColumnType.Text, Required: true),
            new("description", WorksheetColumnType.Text),
            new(
                "tags",
                WorksheetColumnType.Text,
                MultipleValues: true)
        ]);

    public static WorksheetDefinition Tag { get; } = new(
        "Tag",
        [
            new("id", WorksheetColumnType.Text, Required: true),
            new("displayName", WorksheetColumnType.Text, Required: true)
        ]);

    public static IReadOnlyList<WorksheetDefinition> Worksheets { get; } =
        [Recipe, Tag];
}
