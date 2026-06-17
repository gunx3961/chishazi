namespace Chishazi.Services;

public static class AmapUriBuilder
{
    private const string SearchEndpoint = "https://uri.amap.com/search";

    public static string BuildRestaurantSearch(string name)
    {
        var keyword = name.Trim();

        return $"{SearchEndpoint}?keyword={Uri.EscapeDataString(keyword)}" +
               "&view=map&callnative=1&src=chishazi";
    }

    public static string BuildAndroidPoiSearch(string name) =>
        "androidamap://poi?sourceApplication=chishazi" +
        $"&keywords={Uri.EscapeDataString(name.Trim())}&dev=0";

    public static string BuildIosPoiSearch(string name) =>
        "iosamap://poi?sourceApplication=chishazi" +
        $"&name={Uri.EscapeDataString(name.Trim())}&dev=0";
}
