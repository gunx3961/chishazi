namespace Chishazi.Models;

public sealed record FoodItem(
    string Name,
    string Category,
    decimal? CaloriesKcal,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    string Serving);
