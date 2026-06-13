using System.Text.Json;
using Chishazi.DataDefinitions;
using Chishazi.Localization;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class RecipeSheetParser(TagSheetParser tagParser)
{
    public RecipeParseResult Parse(SpreadsheetSnapshot spreadsheet)
    {
        var definition = SpreadsheetDefinition.Recipe;
        var worksheet = spreadsheet.FindWorksheet(definition.Name);

        if (worksheet is null)
        {
            return new RecipeParseResult([], []);
        }

        if (worksheet.Values.Count == 0)
        {
            return new RecipeParseResult(
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
            return new RecipeParseResult(
                [],
                [UiText.Get("RequiredColumnsMissing", string.Join(", ", missingHeaders))]);
        }

        var tagResult = tagParser.Parse(spreadsheet);
        var knownTags = tagResult.Tags
            .Select(tag => tag.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recipes = new List<RecipeItem>();
        var errors = new List<string>();
        errors.AddRange(tagResult.Errors);

        for (var rowIndex = 1; rowIndex < worksheet.Values.Count; rowIndex++)
        {
            var row = worksheet.Values[rowIndex];
            var sheetRowNumber = rowIndex + 1;

            if (IsEmpty(row))
            {
                continue;
            }

            var name = GetText(row, headers, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(UiText.Get("RowNameRequired", sheetRowNumber));
                continue;
            }

            var tags = ParseTags(GetText(row, headers, "tags"));
            var unknownTags = tags
                .Where(tag => !knownTags.Contains(tag))
                .ToList();

            errors.AddRange(unknownTags.Select(tag =>
                UiText.Get("UnknownRecipeTag", sheetRowNumber, tag)));

            recipes.Add(new RecipeItem(
                sheetRowNumber,
                name.Trim(),
                GetText(row, headers, "description").Trim(),
                tags));
        }

        var duplicateNames = recipes
            .GroupBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase);

        errors.AddRange(duplicateNames.Select(name => UiText.Get("DuplicateRecipeName", name)));

        return new RecipeParseResult(recipes, errors);
    }

    private static Dictionary<string, int> BuildHeaderMap(
        IReadOnlyList<JsonElement> headerRow)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headerRow.Count; index++)
        {
            var header = ElementToText(headerRow[index]).Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers.TryAdd(header, index);
            }
        }

        return headers;
    }

    private static string GetText(
        IReadOnlyList<JsonElement> row,
        IReadOnlyDictionary<string, int> headers,
        string header)
    {
        if (!headers.TryGetValue(header, out var index) || index >= row.Count)
        {
            return string.Empty;
        }

        return ElementToText(row[index]);
    }

    private static IReadOnlyList<string> ParseTags(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsEmpty(IEnumerable<JsonElement> row) =>
        row.All(element => string.IsNullOrWhiteSpace(ElementToText(element)));

    private static string ElementToText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.ToString()
        };
}
