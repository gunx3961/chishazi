using System.Text.Json.Serialization;

namespace Chishazi.Models;

public sealed class GoogleSpreadsheetMetadata
{
    [JsonPropertyName("spreadsheetId")]
    public string SpreadsheetId { get; init; } = string.Empty;

    [JsonPropertyName("properties")]
    public GoogleSpreadsheetProperties Properties { get; init; } = new();

    [JsonPropertyName("sheets")]
    public List<GoogleSheetMetadata> Sheets { get; init; } = [];
}

public sealed class GoogleSpreadsheetProperties
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}

public sealed class GoogleSheetMetadata
{
    [JsonPropertyName("properties")]
    public GoogleSheetProperties Properties { get; init; } = new();
}

public sealed class GoogleSheetProperties
{
    [JsonPropertyName("sheetId")]
    public int SheetId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("sheetType")]
    public string SheetType { get; init; } = string.Empty;
}
