using System.Net;
using System.Net.Http.Headers;
using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class GoogleSheetsClientTests
{
    [Theory]
    [InlineData("Foods", "'Foods'")]
    [InlineData("My Foods", "'My Foods'")]
    [InlineData("Owner's Foods", "'Owner''s Foods'")]
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
            ["Foods", "Meal Log"]);

        Assert.Equal(
            "https://sheets.googleapis.com/v4/spreadsheets/spreadsheet-id/values:batchGet" +
            "?ranges=%27Foods%27&ranges=%27Meal%20Log%27" +
            "&majorDimension=ROWS&valueRenderOption=UNFORMATTED_VALUE",
            requestUri);
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
            foods =>
            {
                Assert.Equal("Foods", foods.Name);
                Assert.Equal("Egg", foods.Values[1][0].GetString());
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

    private sealed class SpreadsheetHandler : HttpMessageHandler
    {
        public List<AuthenticationHeaderValue?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization);

            var json = request.RequestUri?.AbsolutePath.EndsWith(
                "/values:batchGet",
                StringComparison.Ordinal) == true
                ? """
                  {
                    "spreadsheetId": "spreadsheet-id",
                    "valueRanges": [
                      {
                        "range": "Foods!A1:A2",
                        "majorDimension": "ROWS",
                        "values": [["name"], ["Egg"]]
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
                          "title": "Foods",
                          "index": 0,
                          "sheetType": "GRID"
                        }
                      }
                    ]
                  }
                  """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
