namespace Chishazi.Models;

public sealed record RestaurantItem(
    int RowNumber,
    string Name,
    string Description,
    IReadOnlyList<string> Tags,
    string Location);
