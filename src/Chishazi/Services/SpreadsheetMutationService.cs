using System.Text.Json;
using Chishazi.DataDefinitions;
using Chishazi.Localization;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class SpreadsheetMutationService
{
    private static IReadOnlyList<JsonElement> CreateHeaderRow(
        WorksheetDefinition definition) =>
        definition.Columns
            .Select(column => JsonSerializer.SerializeToElement(column.Name))
            .ToList();

    private static bool HeaderMatchesDefinition(
        IReadOnlyList<JsonElement> headerRow,
        WorksheetDefinition definition) =>
        headerRow.Count == definition.Columns.Count &&
        headerRow
            .Select(value => SpreadsheetDiffService.ToDisplayText(value))
            .SequenceEqual(
                definition.Columns.Select(column => column.Name),
                StringComparer.OrdinalIgnoreCase);

    public SpreadsheetSnapshot AppendRows(
        SpreadsheetSnapshot snapshot,
        WorksheetDefinition definition,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        if (rows.Count == 0)
        {
            return snapshot;
        }

        snapshot = EnsureWorksheet(snapshot, definition);
        var worksheetIndex = snapshot.Worksheets
            .Select((worksheet, index) => (worksheet, index))
            .FirstOrDefault(candidate => candidate.worksheet.Name.Equals(
                definition.Name,
                StringComparison.OrdinalIgnoreCase));

        if (worksheetIndex.worksheet is null)
        {
            throw new InvalidOperationException(
                UiText.Get("WorksheetMissing", definition.Name));
        }

        if (worksheetIndex.worksheet.Values.Count == 0)
        {
            throw new InvalidOperationException(
                UiText.Get("WorksheetEmpty", definition.Name));
        }

        var headers = worksheetIndex.worksheet.Values[0]
            .Select((value, index) => new
            {
                Name = SpreadsheetDiffService.ToDisplayText(value).Trim(),
                Index = index
            })
            .Where(header => !string.IsNullOrWhiteSpace(header.Name))
            .ToDictionary(
                header => header.Name,
                header => header.Index,
                StringComparer.OrdinalIgnoreCase);
        var missingColumns = definition.Columns
            .Where(column => !headers.ContainsKey(column.Name))
            .Select(column => column.Name)
            .ToList();

        if (missingColumns.Count > 0)
        {
            throw new InvalidOperationException(
                UiText.Get("RequiredColumnsMissing", string.Join(", ", missingColumns)));
        }

        var columnCount = headers.Values.Max() + 1;
        var appendedRows = rows
            .Select(row => CreateRow(row, headers, columnCount))
            .ToList();
        var updatedValues = worksheetIndex.worksheet.Values
            .Concat(appendedRows)
            .ToList();
        var updatedWorksheets = snapshot.Worksheets.ToList();
        updatedWorksheets[worksheetIndex.index] =
            worksheetIndex.worksheet with { Values = updatedValues };

        return snapshot with { Worksheets = updatedWorksheets };
    }

    private static SpreadsheetSnapshot EnsureWorksheet(
        SpreadsheetSnapshot snapshot,
        WorksheetDefinition definition)
    {
        var worksheets = snapshot.Worksheets.ToList();
        var existingIndex = worksheets.FindIndex(worksheet => worksheet.Name.Equals(
            definition.Name,
            StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = worksheets[existingIndex];
            if (existing.SheetId < 0 &&
                existing.Values.Count == 1 &&
                !HeaderMatchesDefinition(existing.Values[0], definition))
            {
                worksheets[existingIndex] = existing with
                {
                    Values = [CreateHeaderRow(definition)]
                };
                return snapshot with { Worksheets = worksheets };
            }

            return snapshot;
        }

        var temporarySheetId = Math.Min(
            0,
            worksheets.Select(worksheet => worksheet.SheetId).DefaultIfEmpty(0).Min()) - 1;
        var index = worksheets.Select(worksheet => worksheet.Index)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        worksheets.Add(new WorksheetSnapshot(
            temporarySheetId,
            index,
            definition.Name,
            "GRID",
            [CreateHeaderRow(definition)]));

        return snapshot with { Worksheets = worksheets };
    }

    public SpreadsheetSnapshot UpdateRow(
        SpreadsheetSnapshot snapshot,
        WorksheetDefinition definition,
        int rowNumber,
        IReadOnlyDictionary<string, string> values)
    {
        var worksheetIndex = FindWorksheet(snapshot, definition);
        var worksheet = worksheetIndex.worksheet;

        if (rowNumber < 2 || rowNumber > worksheet.Values.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber));
        }

        var headers = BuildHeaderMap(worksheet);
        var updatedRow = worksheet.Values[rowNumber - 1].ToList();
        var requiredLength = headers.Values.Max() + 1;

        while (updatedRow.Count < requiredLength)
        {
            updatedRow.Add(JsonSerializer.SerializeToElement(string.Empty));
        }

        foreach (var (columnName, value) in values)
        {
            if (headers.TryGetValue(columnName, out var columnIndex))
            {
                updatedRow[columnIndex] = JsonSerializer.SerializeToElement(value);
            }
        }

        var updatedValues = worksheet.Values.ToList();
        updatedValues[rowNumber - 1] = updatedRow;
        var updatedWorksheets = snapshot.Worksheets.ToList();
        updatedWorksheets[worksheetIndex.index] =
            worksheet with { Values = updatedValues };

        return snapshot with { Worksheets = updatedWorksheets };
    }

    private static (WorksheetSnapshot worksheet, int index) FindWorksheet(
        SpreadsheetSnapshot snapshot,
        WorksheetDefinition definition)
    {
        var worksheetIndex = snapshot.Worksheets
            .Select((worksheet, index) => (worksheet, index))
            .FirstOrDefault(candidate => candidate.worksheet.Name.Equals(
                definition.Name,
                StringComparison.OrdinalIgnoreCase));

        if (worksheetIndex.worksheet is null)
        {
            throw new InvalidOperationException(
                UiText.Get("WorksheetMissing", definition.Name));
        }

        if (worksheetIndex.worksheet.Values.Count == 0)
        {
            throw new InvalidOperationException(
                UiText.Get("WorksheetEmpty", definition.Name));
        }

        return worksheetIndex;
    }

    private static Dictionary<string, int> BuildHeaderMap(
        WorksheetSnapshot worksheet)
    {
        var headers = worksheet.Values[0]
            .Select((value, index) => new
            {
                Name = SpreadsheetDiffService.ToDisplayText(value).Trim(),
                Index = index
            })
            .Where(header => !string.IsNullOrWhiteSpace(header.Name))
            .ToDictionary(
                header => header.Name,
                header => header.Index,
                StringComparer.OrdinalIgnoreCase);

        return headers;
    }

    private static IReadOnlyList<JsonElement> CreateRow(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, int> headers,
        int columnCount)
    {
        var row = Enumerable.Range(0, columnCount)
            .Select(_ => JsonSerializer.SerializeToElement(string.Empty))
            .ToArray();

        foreach (var (columnName, value) in values)
        {
            if (headers.TryGetValue(columnName, out var columnIndex))
            {
                row[columnIndex] = JsonSerializer.SerializeToElement(value);
            }
        }

        return row;
    }
}
