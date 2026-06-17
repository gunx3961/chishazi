using Chishazi.Services;

namespace Chishazi.Tests;

public sealed class AmapUriBuilderTests
{
    [Fact]
    public void BuildRestaurantSearch_EncodesNameOnly()
    {
        var uri = AmapUriBuilder.BuildRestaurantSearch("Tea & Rice");

        Assert.Equal(
            "https://uri.amap.com/search" +
            "?keyword=Tea%20%26%20Rice" +
            "&view=map&callnative=1&src=chishazi",
            uri);
    }

    [Fact]
    public void BuildDirectPoiSearch_EncodesPlatformSpecificUrls()
    {
        var androidUri = AmapUriBuilder.BuildAndroidPoiSearch("Tea & Rice");
        var iosUri = AmapUriBuilder.BuildIosPoiSearch("Tea & Rice");

        Assert.Equal(
            "androidamap://poi?sourceApplication=chishazi" +
            "&keywords=Tea%20%26%20Rice&dev=0",
            androidUri);
        Assert.Equal(
            "iosamap://poi?sourceApplication=chishazi" +
            "&name=Tea%20%26%20Rice&dev=0",
            iosUri);
    }
}
