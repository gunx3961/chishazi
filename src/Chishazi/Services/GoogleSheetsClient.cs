using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chishazi.Models;
using Chishazi.Localization;

namespace Chishazi.Services;

public sealed class GoogleSheetsClient(HttpClient httpClient)
{
    private const string SheetsApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";

    public async Task<SpreadsheetSnapshot> GetSpreadsheetAsync(
        string spreadsheetId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return await GetSpreadsheetAsync(
            spreadsheetId,
            accessToken,
            "UNFORMATTED_VALUE",
            cancellationToken);
    }

    public async Task<SpreadsheetSnapshot> GetSpreadsheetFormulaViewAsync(
        string spreadsheetId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return await GetSpreadsheetAsync(
            spreadsheetId,
            accessToken,
            "FORMULA",
            cancellationToken);
    }

    private async Task<SpreadsheetSnapshot> GetSpreadsheetAsync(
        string spreadsheetId,
        string accessToken,
        string valueRenderOption,
        CancellationToken cancellationToken)
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
            valueRenderOption,
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

    public async Task ApplyChangesAsync(
        string spreadsheetId,
        string accessToken,
        SpreadsheetSnapshot localSnapshot,
        SpreadsheetChangeSet changeSet,
        CancellationToken cancellationToken = default)
    {
        if (!changeSet.HasChanges)
        {
            return;
        }

        var encodedSpreadsheetId = Uri.EscapeDataString(spreadsheetId);
        var requestUri = $"{SheetsApiBaseUrl}/{encodedSpreadsheetId}:batchUpdate";
        var existingSheetIds = localSnapshot.Worksheets
            .Where(worksheet => worksheet.SheetId >= 0)
            .Select(worksheet => worksheet.SheetId)
            .ToHashSet();
        var nextSheetId = existingSheetIds.DefaultIfEmpty(0).Max() + 1;
        var createdSheetIds = new Dictionary<int, int>();
        var requests = new List<object>();

        foreach (var creation in changeSet.WorksheetCreations)
        {
            while (existingSheetIds.Contains(nextSheetId))
            {
                nextSheetId++;
            }

            var sheetId = nextSheetId++;
            existingSheetIds.Add(sheetId);
            createdSheetIds[creation.TemporarySheetId] = sheetId;
            requests.Add(new
            {
                addSheet = new
                {
                    properties = new
                    {
                        sheetId,
                        title = creation.WorksheetName
                    }
                }
            });
        }

        foreach (var change in changeSet.Changes)
        {
            var remoteSheetId = createdSheetIds.GetValueOrDefault(
                change.SheetId,
                change.SheetId);
            requests.Add(new
            {
                updateCells = new
                {
                    rows = new[]
                    {
                        new
                        {
                            values = new[]
                            {
                                CreateCellData(GetCellValue(localSnapshot, change))
                            }
                        }
                    },
                    fields = "userEnteredValue",
                    range = new
                    {
                        sheetId = remoteSheetId,
                        startRowIndex = change.RowNumber - 1,
                        endRowIndex = change.RowNumber,
                        startColumnIndex = change.ColumnNumber - 1,
                        endColumnIndex = change.ColumnNumber
                    }
                }
            });
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(new { requests })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        throw CreateException(response.StatusCode, error?.Error);
    }

    internal static string ToWholeWorksheetRange(string worksheetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        return $"'{worksheetName.Trim().Replace("'", "''", StringComparison.Ordinal)}'";
    }

    internal static string BuildBatchValuesRequestUri(
        string spreadsheetId,
        IEnumerable<string> worksheetNames,
        string valueRenderOption = "UNFORMATTED_VALUE")
    {
        var encodedSpreadsheetId = Uri.EscapeDataString(spreadsheetId);
        var ranges = worksheetNames
            .Select(ToWholeWorksheetRange)
            .Select(range => $"ranges={Uri.EscapeDataString(range)}");
        var query = string.Join("&", ranges);

        return $"{SheetsApiBaseUrl}/{encodedSpreadsheetId}/values:batchGet" +
               $"?{query}&majorDimension=ROWS&valueRenderOption={valueRenderOption}";
    }

    private static object CreateCellData(JsonElement? value)
    {
        if (value is null ||
            value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            value.Value.ValueKind == JsonValueKind.String &&
            string.IsNullOrEmpty(value.Value.GetString()))
        {
            return new { };
        }

        object userEnteredValue = value.Value.ValueKind switch
        {
            JsonValueKind.String =>
                new { stringValue = value.Value.GetString() },
            JsonValueKind.Number =>
                new { numberValue = value.Value.GetDouble() },
            JsonValueKind.True =>
                new { boolValue = true },
            JsonValueKind.False =>
                new { boolValue = false },
            _ =>
                new { stringValue = value.Value.ToString() }
        };

        return new { userEnteredValue };
    }

    private static JsonElement? GetCellValue(
        SpreadsheetSnapshot snapshot,
        SpreadsheetCellChange change)
    {
        var worksheet = snapshot.Worksheets.Single(
            candidate => candidate.SheetId == change.SheetId);
        var rowIndex = change.RowNumber - 1;
        var columnIndex = change.ColumnNumber - 1;

        if (rowIndex >= worksheet.Values.Count ||
            columnIndex >= worksheet.Values[rowIndex].Count)
        {
            return null;
        }

        return worksheet.Values[rowIndex][columnIndex];
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
        string valueRenderOption,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildBatchValuesRequestUri(
            spreadsheetId,
            sheets.Select(sheet => sheet.Title),
            valueRenderOption);

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
                UiText.Get("GoogleAuthorizationExpired"),
            HttpStatusCode.Forbidden =>
                UiText.Get("GoogleSpreadsheetForbidden"),
            HttpStatusCode.NotFound =>
                UiText.Get("GoogleSpreadsheetNotFound"),
            HttpStatusCode.BadRequest =>
                UiText.Get("GoogleSpreadsheetBadRequest"),
            (HttpStatusCode)429 =>
                UiText.Get("GoogleSheetsRateLimited"),
            _ =>
                UiText.Get("GoogleSheetsUnexpectedError", (int)statusCode)
        };

        return new GoogleSheetsException(statusCode, message, error?.Status);
    }
}
