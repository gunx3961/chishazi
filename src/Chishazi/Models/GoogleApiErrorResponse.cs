using System.Text.Json.Serialization;

namespace Chishazi.Models;

public sealed class GoogleApiErrorResponse
{
    [JsonPropertyName("error")]
    public GoogleApiError? Error { get; init; }
}

public sealed class GoogleApiError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
