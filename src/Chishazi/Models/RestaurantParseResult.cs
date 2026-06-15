namespace Chishazi.Models;

public sealed record RestaurantParseResult(
    IReadOnlyList<RestaurantItem> Restaurants,
    IReadOnlyList<string> Errors);
