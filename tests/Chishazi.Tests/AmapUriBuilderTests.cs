using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class AmapUriBuilderTests
{
    [Fact]
    public void BuildRestaurantSearch_EncodesNameAndLocation()
    {
        var uri = AmapUriBuilder.BuildRestaurantSearch(
            "Tea & Rice",
            "Shanghai / Xuhui");

        Assert.Equal(
            "https://uri.amap.com/search" +
            "?keyword=Tea%20%26%20Rice%20Shanghai%20%2F%20Xuhui" +
            "&view=map&callnative=1&src=chishazi",
            uri);
    }

    [Fact]
    public void BuildRestaurantSearch_OmitsWhitespaceOnlyLocation()
    {
        var uri = AmapUriBuilder.BuildRestaurantSearch("Noodle House", "  ");

        Assert.Contains("keyword=Noodle%20House&", uri, StringComparison.Ordinal);
    }
}
