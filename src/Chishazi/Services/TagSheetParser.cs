using System.Text.Json;
using Chishazi.DataDefinitions;
using Chishazi.Localization;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class TagSheetParser
{
    public TagParseResult Parse(SpreadsheetSnapshot spreadsheet)
    {
        var definition = SpreadsheetDefinition.Tag;
        var worksheet = spreadsheet.FindWorksheet(definition.Name);

        if (worksheet is null)
        {
            return new TagParseResult([], []);
        }

        if (worksheet.Values.Count == 0)
        {
            return new TagParseResult(
                [],
                [UiText.Get("WorksheetEmpty", definition.Name)]);
        }

        var headers = BuildHeaderMap(worksheet.Values[0]);
        var missingHeaders = definition.Columns
            .Where(column => column.Required && !headers.ContainsKey(column.Name))
            .Select(column => column.Name)
            .ToList();

        if (missingHeaders.Count > 0)
        {
            return new TagParseResult(
                [],
                [UiText.Get("RequiredColumnsMissing", string.Join(", ", missingHeaders))]);
        }

        var tags = new List<TagItem>();
        var errors = new List<string>();

        for (var rowIndex = 1; rowIndex < worksheet.Values.Count; rowIndex++)
        {
            var row = worksheet.Values[rowIndex];
            var rowNumber = rowIndex + 1;

            if (row.All(value => string.IsNullOrWhiteSpace(ToText(value))))
            {
                continue;
            }

            var id = GetText(row, headers, "id").Trim();
            var displayName = GetText(row, headers, "displayName").Trim();

            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add(UiText.Get("TagRequiredValues", rowNumber));
                continue;
            }

            tags.Add(new TagItem(rowNumber, id, displayName));
        }

        var duplicateIds = tags
            .GroupBy(tag => tag.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        errors.AddRange(duplicateIds.Select(id =>
            UiText.Get("DuplicateTagId", id)));

        return new TagParseResult(tags, errors);
    }

    private static Dictionary<string, int> BuildHeaderMap(
        IReadOnlyList<JsonElement> headerRow) =>
        headerRow
            .Select((value, index) => (Name: ToText(value).Trim(), Index: index))
            .Where(header => !string.IsNullOrWhiteSpace(header.Name))
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Index,
                StringComparer.OrdinalIgnoreCase);

    private static string GetText(
        IReadOnlyList<JsonElement> row,
        IReadOnlyDictionary<string, int> headers,
        string header) =>
        headers.TryGetValue(header, out var index) && index < row.Count
            ? ToText(row[index])
            : string.Empty;

    private static string ToText(JsonElement value) =>
        SpreadsheetDiffService.ToDisplayText(value);
}
