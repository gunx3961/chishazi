using System.Net;

namespace Chishazi.Services;

public sealed class GoogleSheetsException(
    HttpStatusCode statusCode,
    string userMessage,
    string? apiStatus = null)
    : Exception(userMessage)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string UserMessage { get; } = userMessage;

    public string? ApiStatus { get; } = apiStatus;
}
