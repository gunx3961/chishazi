using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chishazi.Models;

namespace Chishazi.Services;

public sealed class GoogleSheetsClient(HttpClient httpClient)
{
    private const string SheetsApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";

    public async Task<SpreadsheetSnapshot> GetSpreadsheetAsync(
        string spreadsheetId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(
            spreadsheetId,
            accessToken,
            cancellationToken);
        var orderedSheets = metadata.Sheets
            .Select(sheet => sheet.Properties)
            .OrderBy(properties => properties.Index)
            .ToList();

        if (orderedSheets.Count == 0)
        {
            return new SpreadsheetSnapshot(
                SpreadsheetSnapshot.CurrentFormatVersion,
                metadata.SpreadsheetId,
                metadata.Properties.Title,
                DateTimeOffset.UtcNow,
                []);
        }

        var values = await GetAllValuesAsync(
            spreadsheetId,
            orderedSheets,
            accessToken,
            cancellationToken);
        var worksheets = orderedSheets
            .Select((sheet, index) => new WorksheetSnapshot(
                sheet.SheetId,
                sheet.Index,
                sheet.Title,
                sheet.SheetType,
                index < values.ValueRanges.Count
                    ? values.ValueRanges[index].Values
                    : []))
            .ToList();

        return new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            metadata.SpreadsheetId,
            metadata.Properties.Title,
            DateTimeOffset.UtcNow,
            worksheets);
    }

    internal static string ToWholeWorksheetRange(string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        return $"'{worksheetName.Trim().Replace("'", "''", StringComparison.Ordinal)}'";
    }

    internal static string BuildBatchValuesRequestUri(
        string spreadsheetId,
        IEnumerable<string> worksheetNames)
    {
        var encodedSpreadsheetId = Uri.EscapeDataString(spreadsheetId);
        var ranges = worksheetNames
            .Select(ToWholeWorksheetRange)
            .Select(range => $"ranges={Uri.EscapeDataString(range)}");
        var query = string.Join("&", ranges);

        return $"{SheetsApiBaseUrl}/{encodedSpreadsheetId}/values:batchGet" +
               $"?{query}&majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE";
    }

    private async Task<GoogleSpreadsheetMetadata> GetMetadataAsync(
        string spreadsheetId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var encodedSpreadsheetId = Uri.EscapeDataString(spreadsheetId);
        var requestUri =
            $"{SheetsApiBaseUrl}/{encodedSpreadsheetId}" +
            "?fields=spreadsheetId,properties(title)," +
            "sheets(properties(sheetId,title,index,sheetType))";

        return await SendAsync<GoogleSpreadsheetMetadata>(
            requestUri,
            accessToken,
            cancellationToken);
    }

    private async Task<GoogleBatchValueResponse> GetAllValuesAsync(
        string spreadsheetId,
        IReadOnlyList<GoogleSheetProperties> sheets,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildBatchValuesRequestUri(
            spreadsheetId,
            sheets.Select(sheet => sheet.Title));

        return await SendAsync<GoogleBatchValueResponse>(
            requestUri,
            accessToken,
            cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        string requestUri,
        string accessToken,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(
                       cancellationToken: cancellationToken)
                   ?? new T();
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        throw CreateException(response.StatusCode, error?.Error);
    }

    private static async Task<GoogleApiErrorResponse?> TryReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<GoogleApiErrorResponse>(
                cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static GoogleSheetsException CreateException(
        HttpStatusCode statusCode,
        GoogleApiError? error)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Google authorization expired or was rejected. Authorize again.",
            HttpStatusCode.Forbidden =>
                "This Google account cannot read the spreadsheet. Check the sheet sharing settings and OAuth scope.",
            HttpStatusCode.NotFound =>
                "The spreadsheet was not found. Check the Spreadsheet ID and sharing settings.",
            HttpStatusCode.BadRequest =>
                "Google rejected the spreadsheet request. Check the spreadsheet structure.",
            (HttpStatusCode)429 =>
                "The Google Sheets request limit was reached. Wait briefly and try again.",
            _ =>
                $"Google Sheets returned an unexpected error ({(int)statusCode}). Try again."
        };

        return new GoogleSheetsException(statusCode, message, error?.Status);
    }
}
