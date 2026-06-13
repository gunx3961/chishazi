namespace Chishazi.Models;

public sealed record RecipeItem(
    int RowNumber,
    string Name,
    string Description,
    IReadOnlyList<string> Tags);
