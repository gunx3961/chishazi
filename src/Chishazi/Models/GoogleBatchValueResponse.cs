using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chishazi.Models;

public sealed class GoogleBatchValueResponse
{
    [JsonPropertyName("spreadsheetId")]
    public string SpreadsheetId { get; init; } = string.Empty;

    [JsonPropertyName("valueRanges")]
    public List<GoogleValueRange> ValueRanges { get; init; } = [];
}

public sealed class GoogleValueRange
{
    [JsonPropertyName("range")]
    public string Range { get; init; } = string.Empty;

    [JsonPropertyName("majorDimension")]
    public string MajorDimension { get; init; } = string.Empty;

    [JsonPropertyName("values")]
    public List<List<JsonElement>> Values { get; init; } = [];
}
