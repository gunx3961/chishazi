# UI Text and Localization

Last updated: 2026-06-12

## Source of Truth

All user-visible application text is defined in:

```text
src/Chishazi/Resources/UiText.resx
```

Razor components and C# services access text through:

```text
src/Chishazi/Localization/UiText.cs
```

Do not add user-visible sentences directly to Razor components, services, or
JavaScript. Stable technical identifiers, resource keys, CSS names, worksheet
column names, and the `CHISHAZI_*` JavaScript error codes are not UI text.

## Editing English Text

Edit the `<value>` associated with a resource key in `UiText.resx`. Keep the
key unchanged unless all code references are updated at the same time.

Formatted resources use zero-based placeholders:

```text
{0} recipes
```

Upload preview, conflict, and route browser text follows the same resource
convention. Services may format resource messages, but must not contain
user-visible fallback sentences.

Tag display names come from the `Tag` worksheet and are not UI resources.
Resource entries cover only the surrounding management controls and validation
messages.

The arguments are supplied by `UiText.Get`.

## Adding a Language

Create a culture-specific resource next to the default resource:

```text
UiText.zh-CN.resx
UiText.ja-JP.resx
```

Copy every key from `UiText.resx` and translate only the values. The default
resource remains the fallback when a culture-specific value is missing.

Culture selection is not exposed in the current UI. A future language selector
should set `CurrentCulture` and `CurrentUICulture`, persist the selected culture,
and reload the application so the matching satellite resource is loaded.

## Browser Startup Boundary

The pre-Blazor HTML shell contains only the non-localized product name
`Chishazi` and a graphical loading indicator. Runtime error controls are
rendered by `App.razor` and therefore use the resource file.

Google authorization JavaScript returns stable error codes. C# maps those codes
to localized resource values before displaying them.
