using System.Globalization;
using System.Text.Json;
using Chishazi.Localization;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class SpreadsheetDiffService
{
    public SpreadsheetChangeSet PrepareUpload(
        SpreadsheetSnapshot local,
        SpreadsheetSnapshot baseline,
        SpreadsheetSnapshot remote)
    {
        var intendedChanges = Compare(local, baseline);
        if (!intendedChanges.CanUpload || !intendedChanges.HasChanges)
        {
            return intendedChanges;
        }

        var issues = intendedChanges.BlockingIssues.ToList();
        var pendingChanges = new List<SpreadsheetCellChange>();

        foreach (var creation in intendedChanges.WorksheetCreations)
        {
            if (remote.Worksheets.Any(worksheet => worksheet.Name.Equals(
                    creation.WorksheetName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(UiText.Get(
                    "WorksheetNameConflict",
                    creation.WorksheetName));
            }
        }

        foreach (var change in intendedChanges.Changes.Where(
                     change => change.SheetId >= 0))
        {
            var baselineWorksheet = baseline.Worksheets.FirstOrDefault(
                worksheet => worksheet.SheetId == change.SheetId);
            var remoteWorksheet = remote.Worksheets.FirstOrDefault(
                worksheet => worksheet.SheetId == change.SheetId);

            if (baselineWorksheet is null || remoteWorksheet is null)
            {
                issues.Add(UiText.Get(
                    "UploadTargetWorksheetChanged",
                    change.WorksheetName));
                continue;
            }

            if (!baselineWorksheet.Name.Equals(
                    remoteWorksheet.Name,
                    StringComparison.Ordinal))
            {
                issues.Add(UiText.Get(
                    "WorksheetRenamedRemotely",
                    change.SheetId,
                    baselineWorksheet.Name,
                    remoteWorksheet.Name));
                continue;
            }

            var baselineValue = GetCellValue(
                baseline,
                change.SheetId,
                change.RowNumber - 1,
                change.ColumnNumber - 1);
            var remoteValue = GetCellValue(
                remote,
                change.SheetId,
                change.RowNumber - 1,
                change.ColumnNumber - 1);
            var localValue = GetCellValue(
                local,
                change.SheetId,
                change.RowNumber - 1,
                change.ColumnNumber - 1);

            if (ValuesEqual(baselineValue, remoteValue))
            {
                pendingChanges.Add(change);
            }
            else if (!ValuesEqual(localValue, remoteValue))
            {
                issues.Add(UiText.Get(
                    "UploadTargetCellChanged",
                    change.WorksheetName,
                    change.RowNumber,
                    change.ColumnName));
            }
        }

        return new SpreadsheetChangeSet(
            intendedChanges.Changes
                .Where(change => change.SheetId < 0)
                .Concat(pendingChanges)
                .ToList(),
            intendedChanges.WorksheetCreations,
            issues.Distinct(StringComparer.Ordinal).ToList());
    }

    public SpreadsheetChangeSet Compare(
        SpreadsheetSnapshot local,
        SpreadsheetSnapshot remote)
    {
        var (worksheetCreations, issues) = CompareStructure(local, remote);
        if (issues.Count > 0)
        {
            return new SpreadsheetChangeSet([], worksheetCreations, issues);
        }

        var changes = new List<SpreadsheetCellChange>();

        foreach (var localWorksheet in local.Worksheets)
        {
            var remoteWorksheet = remote.Worksheets.FirstOrDefault(
                worksheet => worksheet.SheetId == localWorksheet.SheetId);
            var rowCount = Math.Max(
                localWorksheet.Values.Count,
                remoteWorksheet?.Values.Count ?? 0);

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var localRow = GetRow(localWorksheet, rowIndex);
                var remoteRow = remoteWorksheet is null
                    ? []
                    : GetRow(remoteWorksheet, rowIndex);
                var columnCount = Math.Max(localRow.Count, remoteRow.Count);

                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var localValue = GetValue(localRow, columnIndex);
                    var remoteValue = GetValue(remoteRow, columnIndex);

                    if (ValuesEqual(localValue, remoteValue))
                    {
                        continue;
                    }

                    changes.Add(new SpreadsheetCellChange(
                        localWorksheet.SheetId,
                        localWorksheet.Name,
                        rowIndex + 1,
                        columnIndex + 1,
                        GetColumnName(
                            localWorksheet,
                            remoteWorksheet,
                            columnIndex),
                        GetChangeKind(localValue, remoteValue),
                        ToDisplayText(remoteValue),
                        ToDisplayText(localValue)));
                }
            }
        }

        return new SpreadsheetChangeSet(changes, worksheetCreations, []);
    }

    public SpreadsheetChangeSet BlockRemoteFormulaOverwrites(
        SpreadsheetChangeSet changeSet,
        SpreadsheetSnapshot remoteFormulaView)
    {
        if (!changeSet.CanUpload || !changeSet.HasChanges)
        {
            return changeSet;
        }

        var issues = changeSet.BlockingIssues.ToList();

        foreach (var change in changeSet.Changes)
        {
            var formula = GetCellValue(
                remoteFormulaView,
                change.SheetId,
                change.RowNumber - 1,
                change.ColumnNumber - 1);

            if (formula?.ValueKind == JsonValueKind.String &&
                formula.Value.GetString()?.StartsWith(
                    "=",
                    StringComparison.Ordinal) == true)
            {
                issues.Add(UiText.Get(
                    "FormulaOverwriteBlocked",
                    change.WorksheetName,
                    change.RowNumber,
                    change.ColumnNumber));
            }
        }

        return new SpreadsheetChangeSet(
            changeSet.Changes,
            changeSet.WorksheetCreations,
            issues);
    }

    private static (
        List<SpreadsheetWorksheetCreation> creations,
        List<string> issues) CompareStructure(
        SpreadsheetSnapshot local,
        SpreadsheetSnapshot remote)
    {
        var creations = new List<SpreadsheetWorksheetCreation>();
        var issues = new List<string>();
        var localById = local.Worksheets.ToDictionary(worksheet => worksheet.SheetId);
        var remoteById = remote.Worksheets.ToDictionary(worksheet => worksheet.SheetId);

        foreach (var worksheet in local.Worksheets)
        {
            if (!remoteById.TryGetValue(worksheet.SheetId, out var remoteWorksheet))
            {
                if (worksheet.SheetId < 0 &&
                    !remote.Worksheets.Any(candidate => candidate.Name.Equals(
                        worksheet.Name,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    creations.Add(new SpreadsheetWorksheetCreation(
                        worksheet.SheetId,
                        worksheet.Name));
                }
                else if (worksheet.SheetId < 0)
                {
                    issues.Add(UiText.Get(
                        "WorksheetNameConflict",
                        worksheet.Name));
                }
                else
                {
                    issues.Add(UiText.Get(
                        "WorksheetMissingRemotely",
                        worksheet.Name));
                }

                continue;
            }

            if (!worksheet.Name.Equals(
                    remoteWorksheet.Name,
                    StringComparison.Ordinal))
            {
                issues.Add(UiText.Get(
                    "WorksheetRenamedRemotely",
                    worksheet.SheetId,
                    worksheet.Name,
                    remoteWorksheet.Name));
            }
        }

        foreach (var worksheet in remote.Worksheets.Where(
                     worksheet =>
                         !localById.ContainsKey(worksheet.SheetId) &&
                         !local.Worksheets.Any(candidate =>
                             candidate.SheetId < 0 &&
                             candidate.Name.Equals(
                                 worksheet.Name,
                                 StringComparison.OrdinalIgnoreCase))))
        {
            issues.Add(UiText.Get("WorksheetMissingLocally", worksheet.Name));
        }

        return (creations, issues);
    }

    private static IReadOnlyList<JsonElement> GetRow(
        WorksheetSnapshot worksheet,
        int rowIndex) =>
        rowIndex < worksheet.Values.Count ? worksheet.Values[rowIndex] : [];

    private static string GetColumnName(
        WorksheetSnapshot localWorksheet,
        WorksheetSnapshot? remoteWorksheet,
        int columnIndex)
    {
        var localHeader = GetRow(localWorksheet, 0);
        var remoteHeader = remoteWorksheet is null
            ? []
            : GetRow(remoteWorksheet, 0);
        var name = ToDisplayText(GetValue(localHeader, columnIndex));

        if (string.IsNullOrWhiteSpace(name))
        {
            name = ToDisplayText(GetValue(remoteHeader, columnIndex));
        }

        return string.IsNullOrWhiteSpace(name)
            ? UiText.Get("ColumnNumber", columnIndex + 1)
            : name;
    }

    private static JsonElement? GetValue(
        IReadOnlyList<JsonElement> row,
        int columnIndex) =>
        columnIndex < row.Count && !IsEmpty(row[columnIndex])
            ? row[columnIndex]
            : null;

    private static JsonElement? GetCellValue(
        SpreadsheetSnapshot snapshot,
        int sheetId,
        int rowIndex,
        int columnIndex)
    {
        var worksheet = snapshot.Worksheets.FirstOrDefault(
            candidate => candidate.SheetId == sheetId);
        if (worksheet is null || rowIndex >= worksheet.Values.Count)
        {
            return null;
        }

        return GetValue(worksheet.Values[rowIndex], columnIndex);
    }

    private static bool ValuesEqual(JsonElement? left, JsonElement? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Value.ValueKind == JsonValueKind.Number &&
            right.Value.ValueKind == JsonValueKind.Number)
        {
            return left.Value.GetRawText() == right.Value.GetRawText() ||
                   left.Value.TryGetDecimal(out var leftDecimal) &&
                   right.Value.TryGetDecimal(out var rightDecimal) &&
                   leftDecimal == rightDecimal;
        }

        if (left.Value.ValueKind == JsonValueKind.String &&
            right.Value.ValueKind == JsonValueKind.String)
        {
            return string.Equals(
                left.Value.GetString(),
                right.Value.GetString(),
                StringComparison.Ordinal);
        }

        return left.Value.ValueKind == right.Value.ValueKind &&
               left.Value.GetRawText() == right.Value.GetRawText();
    }

    private static SpreadsheetCellChangeKind GetChangeKind(
        JsonElement? local,
        JsonElement? remote) =>
        remote is null
            ? SpreadsheetCellChangeKind.Added
            : local is null
                ? SpreadsheetCellChangeKind.Cleared
                : SpreadsheetCellChangeKind.Modified;

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        value.ValueKind == JsonValueKind.String &&
        string.IsNullOrEmpty(value.GetString());

    public static string ToDisplayText(JsonElement? value) =>
        value?.ValueKind switch
        {
            null or JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.Value.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => value.Value.TryGetDecimal(out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : value.Value.GetRawText(),
            _ => value.Value.ToString()
        };
}
