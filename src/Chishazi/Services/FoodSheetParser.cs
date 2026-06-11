using System.Globalization;
using System.Text.Json;
using Chishazi.DataDefinitions;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class FoodSheetParser
{
    public FoodParseResult Parse(SpreadsheetSnapshot spreadsheet)
    {
        var definition = SpreadsheetDefinition.Foods;
        var worksheet = spreadsheet.FindWorksheet(definition.Name);

        if (worksheet is null)
        {
            return new FoodParseResult(
                [],
                [$"The spreadsheet does not contain the '{definition.Name}' worksheet."]);
        }

        if (worksheet.Values.Count == 0)
        {
            return new FoodParseResult(
                [],
                [$"The '{definition.Name}' worksheet returned no rows."]);
        }

        var headers = BuildHeaderMap(worksheet.Values[0]);
        var missingHeaders = definition.Columns
            .Where(column => column.Required && !headers.ContainsKey(column.Name))
            .Select(column => column.Name)
            .ToList();

        if (missingHeaders.Count > 0)
        {
            return new FoodParseResult(
                [],
                [$"The header row is missing required column(s): {string.Join(", ", missingHeaders)}."]);
        }

        var foods = new List<FoodItem>();
        var errors = new List<string>();

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
                errors.Add($"Row {sheetRowNumber}: name is required.");
                continue;
            }

            if (!TryReadNumbers(
                    row,
                    headers,
                    definition,
                    sheetRowNumber,
                    errors,
                    out var numbers))
            {
                continue;
            }

            foods.Add(new FoodItem(
                name.Trim(),
                GetText(row, headers, "category").Trim(),
                numbers["calories_kcal"],
                numbers["protein_g"],
                numbers["carbs_g"],
                numbers["fat_g"],
                GetText(row, headers, "serving").Trim()));
        }

        var duplicateNames = foods
            .GroupBy(food => food.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase);

        errors.AddRange(duplicateNames.Select(name => $"Duplicate food name: {name}."));

        return new FoodParseResult(foods, errors);
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

    private static bool TryReadNumbers(
        IReadOnlyList<JsonElement> row,
        IReadOnlyDictionary<string, int> headers,
        WorksheetDefinition definition,
        int sheetRowNumber,
        ICollection<string> errors,
        out Dictionary<string, decimal?> numbers)
    {
        numbers = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in definition.Columns.Where(
                     column => column.Type == WorksheetColumnType.Decimal))
        {
            var text = GetText(row, headers, column.Name).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                numbers[column.Name] = null;
                continue;
            }

            if (!decimal.TryParse(
                    text,
                    NumberStyles.Number | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                errors.Add($"Row {sheetRowNumber}: {column.Name} must be a number.");
                return false;
            }

            if (column.NonNegative && value < 0)
            {
                errors.Add($"Row {sheetRowNumber}: {column.Name} cannot be negative.");
                return false;
            }

            numbers[column.Name] = value;
        }

        return true;
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
