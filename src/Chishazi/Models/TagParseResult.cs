namespace Chishazi.Models;

public sealed record TagParseResult(
    IReadOnlyList<TagItem> Tags,
    IReadOnlyList<string> Errors);
