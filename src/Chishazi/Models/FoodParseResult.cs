namespace Chishazi.Models;

public sealed record FoodParseResult(
    IReadOnlyList<FoodItem> Foods,
    IReadOnlyList<string> Errors);
