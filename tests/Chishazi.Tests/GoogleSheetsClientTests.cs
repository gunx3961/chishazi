using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Chishazi.Models;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class GoogleSheetsClientTests
{
    [Theory]
    [InlineData("Recipe", "'Recipe'")]
    [InlineData("Meal Plans", "'Meal Plans'")]
    [InlineData("Owner's Recipes", "'Owner''s Recipes'")]
    public void ToWholeWorksheetRange_QuotesWorksheetName(
        string worksheetName,
        string expected)
    {
        Assert.Equal(expected, GoogleSheetsClient.ToWholeWorksheetRange(worksheetName));
    }

    [Fact]
    public void BuildBatchValuesRequestUri_IncludesEveryWorksheet()
    {
        var requestUri = GoogleSheetsClient.BuildBatchValuesRequestUri(
            "spreadsheet-id",
            ["Recipe", "Meal Log"]);

        Assert.Equal(
            "https://sheets.googleapis.com/v4/spreadsheets/spreadsheet-id/values:batchGet" +
            "?ranges=%27Recipe%27&ranges=%27Meal%20Log%27" +
            "&majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE",
            requestUri);
    }

    [Fact]
    public void BuildBatchValuesRequestUri_CanRequestFormulaValues()
    {
        var requestUri = GoogleSheetsClient.BuildBatchValuesRequestUri(
            "spreadsheet-id",
            ["Recipe"],
            "FORMULA");

        Assert.EndsWith(
            "majorDimension=ROWS&valueRenderOption=FORMULA",
            requestUri,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSpreadsheetAsync_ReturnsAllWorksheetsInTabOrder()
    {
        var handler = new SpreadsheetHandler();
        var client = new GoogleSheetsClient(new HttpClient(handler));

        var snapshot = await client.GetSpreadsheetAsync(
            "spreadsheet-id",
            "access-token");

        Assert.Equal("Test data", snapshot.Title);
        Assert.Collection(
            snapshot.Worksheets,
            recipes =>
            {
                Assert.Equal("Recipe", recipes.Name);
                Assert.Equal("Egg sandwich", recipes.Values[1][0].GetString());
            },
            log =>
            {
                Assert.Equal("Meal Log", log.Name);
                Assert.Equal("2026-06-11", log.Values[1][0].GetString());
            });
        Assert.All(
            handler.Authorizations,
            authorization => Assert.Equal(
                new AuthenticationHeaderValue("Bearer", "access-token"),
                authorization));
    }

    [Fact]
    public async Task ApplyChangesAsync_WritesOnlyChangedCells()
    {
        var handler = new SpreadsheetHandler();
        var client = new GoogleSheetsClient(new HttpClient(handler));
        var snapshot = CreateSnapshot(
            """[["name","formula"],["Rice","=A2"]]""");
        var changes = new[]
        {
            new SpreadsheetCellChange(
                1,
                "Recipe",
                2,
                1,
                "name",
                SpreadsheetCellChangeKind.Modified,
                "Old",
                "Rice"),
            new SpreadsheetCellChange(
                1,
                "Recipe",
                2,
                2,
                "formula",
                SpreadsheetCellChangeKind.Modified,
                "2",
                "=A2")
        };

        await client.ApplyChangesAsync(
            "spreadsheet-id",
            "write-token",
            snapshot,
            new SpreadsheetChangeSet(changes, [], []));

        Assert.Equal(HttpMethod.Post, handler.LastWriteMethod);
        Assert.Equal(
            "/v4/spreadsheets/spreadsheet-id:batchUpdate",
            handler.LastWritePath);
        Assert.Contains(
            "\"stringValue\":\"Rice\"",
            handler.LastWriteJson,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"stringValue\":\"=A2\"",
            handler.LastWriteJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyChangesAsync_CreatesWorksheetBeforeWritingCells()
    {
        var handler = new SpreadsheetHandler();
        var client = new GoogleSheetsClient(new HttpClient(handler));
        var snapshot = CreateSnapshot(
            """[["name"]]""") with
        {
            Worksheets =
            [
                new WorksheetSnapshot(
                    -1,
                    0,
                    "Recipe",
                    "GRID",
                    JsonSerializer.Deserialize<List<List<JsonElement>>>(
                        """[["name","description","tags"],["Rice","","quick"]]""")!
                        .Select(row => (IReadOnlyList<JsonElement>)row)
                        .ToList())
            ]
        };
        var changeSet = new SpreadsheetChangeSet(
            [
                new SpreadsheetCellChange(
                    -1,
                    "Recipe",
                    1,
                    1,
                    "name",
                    SpreadsheetCellChangeKind.Added,
                    string.Empty,
                    "name")
            ],
            [new SpreadsheetWorksheetCreation(-1, "Recipe")],
            []);

        await client.ApplyChangesAsync(
            "spreadsheet-id",
            "write-token",
            snapshot,
            changeSet);

        Assert.Contains("\"addSheet\"", handler.LastWriteJson);
        Assert.Contains("\"title\":\"Recipe\"", handler.LastWriteJson);
        Assert.DoesNotContain("\"sheetId\":-1", handler.LastWriteJson);
        Assert.True(
            handler.LastWriteJson.IndexOf("\"addSheet\"", StringComparison.Ordinal) <
            handler.LastWriteJson.IndexOf("\"updateCells\"", StringComparison.Ordinal));
    }

    private static SpreadsheetSnapshot CreateSnapshot(string valuesJson)
    {
        var values = JsonSerializer.Deserialize<List<List<JsonElement>>>(valuesJson)
                     ?? throw new InvalidOperationException("Test JSON did not deserialize.");

        return new SpreadsheetSnapshot(
            SpreadsheetSnapshot.CurrentFormatVersion,
            "spreadsheet-id",
            "Test data",
            DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
            [
                new WorksheetSnapshot(
                    1,
                    0,
                    "Recipe",
                    "GRID",
                    values.Select(row => (IReadOnlyList<JsonElement>)row).ToList())
            ]);
    }

    private sealed class SpreadsheetHandler : HttpMessageHandler
    {
        public List<AuthenticationHeaderValue?> Authorizations { get; } = [];
        public HttpMethod? LastWriteMethod { get; private set; }
        public string? LastWritePath { get; private set; }
        public string LastWriteJson { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization);

            if (request.Method == HttpMethod.Post)
            {
                LastWriteMethod = request.Method;
                LastWritePath = request.RequestUri?.AbsolutePath;
                LastWriteJson = await request.Content!.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                };
            }

            var json = request.RequestUri?.AbsolutePath.EndsWith(
                "/values:batchGet",
                StringComparison.Ordinal) == true
                ? """
                  {
                    "spreadsheetId": "spreadsheet-id",
                    "valueRanges": [
                      {
                        "range": "Recipe!A1:A2",
                        "majorDimension": "ROWS",
                        "values": [["name"], ["Egg sandwich"]]
                      },
                      {
                        "range": "'Meal Log'!A1:A2",
                        "majorDimension": "ROWS",
                        "values": [["date"], ["2026-06-11"]]
                      }
                    ]
                  }
                  """
                : """
                  {
                    "spreadsheetId": "spreadsheet-id",
                    "properties": { "title": "Test data" },
                    "sheets": [
                      {
                        "properties": {
                          "sheetId": 2,
                          "title": "Meal Log",
                          "index": 1,
                          "sheetType": "GRID"
                        }
                      },
                      {
                        "properties": {
                          "sheetId": 1,
                          "title": "Recipe",
                          "index": 0,
                          "sheetType": "GRID"
                        }
                      }
                    ]
                  }
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        }
    }
}
