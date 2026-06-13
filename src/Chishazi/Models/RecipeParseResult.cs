namespace Chishazi.Models;

public sealed record RecipeParseResult(
    IReadOnlyList<RecipeItem> Recipes,
    IReadOnlyList<string> Errors);
