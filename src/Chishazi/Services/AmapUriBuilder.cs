namespace Chishazi.Services;

public static class AmapUriBuilder
{
    private const string SearchEndpoint = "https://uri.amap.com/search";

    public static string BuildRestaurantSearch(string name, string location)
    {
        var keyword = string.Join(
            " ",
            new[] { name.Trim(), location.Trim() }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return $"{SearchEndpoint}?keyword={Uri.EscapeDataString(keyword)}" +
               "&view=map&callnative=1&src=chishazi";
    }
}
