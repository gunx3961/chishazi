namespace Chishazi.Options;

public sealed class GoogleSheetsOptions
{
    private const string ClientIdPlaceholder = "YOUR_CLIENT_ID.apps.googleusercontent.com";
    private const string SpreadsheetIdPlaceholder = "YOUR_SPREADSHEET_ID";

    public required string ClientId { get; init; }

    public required string SpreadsheetId { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(SpreadsheetId) &&
        !ClientId.Equals(ClientIdPlaceholder, StringComparison.Ordinal) &&
        !SpreadsheetId.Equals(SpreadsheetIdPlaceholder, StringComparison.Ordinal);
}
