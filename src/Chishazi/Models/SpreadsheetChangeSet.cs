namespace Chishazi.Models;

public enum SpreadsheetCellChangeKind
{
    Added,
    Modified,
    Cleared
}

public sealed record SpreadsheetCellChange(
    int SheetId,
    string WorksheetName,
    int RowNumber,
    int ColumnNumber,
    string ColumnName,
    SpreadsheetCellChangeKind Kind,
    string Before,
    string After);

public sealed record SpreadsheetRowChange(
    int SheetId,
    string WorksheetName,
    int RowNumber,
    SpreadsheetCellChangeKind Kind,
    IReadOnlyList<SpreadsheetCellChange> Fields);

public sealed record SpreadsheetWorksheetCreation(
    int TemporarySheetId,
    string WorksheetName);

public sealed record SpreadsheetChangeSet(
    IReadOnlyList<SpreadsheetCellChange> Changes,
    IReadOnlyList<SpreadsheetWorksheetCreation> WorksheetCreations,
    IReadOnlyList<string> BlockingIssues)
{
    public bool CanUpload => BlockingIssues.Count == 0;
    public bool HasChanges => Changes.Count > 0 || WorksheetCreations.Count > 0;
    public int OperationCount => Changes.Count + WorksheetCreations.Count;

    public IReadOnlyList<SpreadsheetRowChange> RowChanges =>
        Changes
            .Where(change => !WorksheetCreations.Any(creation =>
                creation.TemporarySheetId == change.SheetId &&
                change.RowNumber == 1))
            .GroupBy(change => new
            {
                change.SheetId,
                change.WorksheetName,
                change.RowNumber
            })
            .Select(group => new SpreadsheetRowChange(
                group.Key.SheetId,
                group.Key.WorksheetName,
                group.Key.RowNumber,
                GetRowKind(group),
                group.OrderBy(change => change.ColumnNumber).ToList()))
            .OrderBy(change => change.WorksheetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.RowNumber)
            .ToList();

    public int DisplayChangeCount => WorksheetCreations.Count + RowChanges.Count;

    private static SpreadsheetCellChangeKind GetRowKind(
        IEnumerable<SpreadsheetCellChange> changes)
    {
        var kinds = changes.Select(change => change.Kind).Distinct().ToList();
        return kinds.Count == 1
            ? kinds[0]
            : SpreadsheetCellChangeKind.Modified;
    }
}
