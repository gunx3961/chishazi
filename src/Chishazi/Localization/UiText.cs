using System.Globalization;
using System.Resources;

namespace Chishazi.Localization;

public static class UiText
{
    private static readonly ResourceManager ResourceManager =
        new("Chishazi.Resources.UiText", typeof(UiText).Assembly);

    public static string Get(string key, params object?[] arguments)
    {
        var value = ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        return arguments.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, arguments);
    }
}
