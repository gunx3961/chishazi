using System.Text.Json;

namespace Chishazi.Models;

public sealed record SpreadsheetSnapshot(
    int FormatVersion,
    string SpreadsheetId,
    string Title,
    DateTimeOffset FetchedAtUtc,
    IReadOnlyList<WorksheetSnapshot> Worksheets)
{
    public const int CurrentFormatVersion = 1;

    public WorksheetSnapshot? FindWorksheet(string name) =>
        Worksheets.FirstOrDefault(
            worksheet => worksheet.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public sealed record WorksheetSnapshot(
    int SheetId,
    int Index,
    string Name,
    string SheetType,
    IReadOnlyList<IReadOnlyList<JsonElement>> Values);
