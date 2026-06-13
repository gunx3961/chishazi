# Workspace Rules

These rules apply to the entire repository.

## Language

- All repository documentation, source comments, user interface copy, commit
  messages created by agents, and configuration notes must be written in
  English.
- Conversations with the repository owner may use Chinese.
- Before completing a change, search edited text files for accidental
  non-English content.

## Product Boundary

- The product is a single-user random meal decision application.
- Its primary purpose is to help decide what to eat, not to evaluate diet
  quality or provide nutritional assessment.
- The production client is a standalone Blazor WebAssembly application hosted
  on GitHub Pages.
- The data source is a private Google Sheet accessed through the Google Sheets
  API after interactive user authorization.
- Google authorization requests the complete Sheets scope once per token.
  Upload operations still require an explicit preview and confirmation.

## Security

- Never commit OAuth client secrets, service account keys, access tokens,
  refresh tokens, cookies, or exported Google credentials.
- OAuth Client IDs and Spreadsheet IDs are public identifiers and may be kept
  in client configuration, but use placeholders in the repository by default.
- Access tokens may be stored only in the dedicated localStorage authorization
  entry with an absolute expiration time. Do not write them to IndexedDB,
  cookies, URLs, logs, analytics, or exception messages.
- Reuse the access token until its reported expiration. If Google returns 401,
  invalidate it, request a new token, and retry the failed operation once.
- Request `https://www.googleapis.com/auth/spreadsheets` for the shared
  application token.
- Treat all Google Sheet cell values as untrusted text. Do not render them as
  raw HTML.
- Spreadsheet snapshots may be stored in IndexedDB, but OAuth credentials may
  not be included in snapshot data.

## Engineering

- Treat minimalism as a primary product and engineering principle. Do not
  expose fields, controls, states, or abstractions without a current user need.
- Target the .NET version declared by `global.json`.
- Keep Google authorization, Sheets transport, row parsing, and UI state in
  separate components.
- Keep browser caching schema-neutral. Cache raw spreadsheet snapshots and
  apply worksheet definitions after loading.
- Keep upload comparison and transport schema-neutral.
- Keep local row mutation schema-neutral. Type routes may append batches to
  the shared working snapshot through generic worksheet definitions.
- Keep the last synchronized baseline separate from the local working snapshot.
- Do not mutate the working snapshot during ordinary cache reads.
- Do not pull remote data over pending local changes.
- Local change review and discard must use the cached baseline without
  contacting Google.
- Reject uploads when worksheet structure differs between local and remote
  snapshots.
- Recheck remote data after preview and before upload.
- Update only changed cells. Do not overwrite unchanged cells.
- Present synchronization diffs by row in the UI while retaining cell-level
  transport and formula protection internally.
- Derive upload intent only from working-copy changes against the synchronized
  baseline. Never convert unrelated remote differences into upload changes.
- Treat `src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs` as the source
  of truth for worksheet data contracts.
- Define controlled-value worksheet structures in `SpreadsheetDefinition.cs`.
  Store and manage the actual values in the spreadsheet, not in code.
- Tag IDs are internal and automatically managed. User-facing Tag workflows
  interact only with display names.
- Treat `src/Chishazi/Resources/UiText.resx` as the source of truth for all
  user-visible application text.
- Do not place user-visible sentences directly in Razor components, services,
  or JavaScript.
- Prefer platform APIs and small local abstractions over additional packages.
- Add or update focused tests when parsing rules or data contracts change.
- Do not modify unrelated Godot files or generated `.godot` content.

## Documentation Memory

- Update `documentation/project-memory.md` when a durable decision, constraint, data
  contract, or operational fact changes.
- Append a dated entry to `documentation/development-log.md` for every implementation
  session that changes behavior or repository structure.
- Update `documentation/setup.md` when configuration or deployment steps change.
- Update `documentation/architecture.md` when component boundaries, security boundaries,
  or external integrations change.
- Update `documentation/data-contract.md` when worksheet definitions change.
- Update `documentation/localization.md` when localization conventions or
  culture selection behavior changes.

## Completion Checks

Run the checks that apply:

```bash
dotnet build Chishazi.slnx
dotnet test Chishazi.slnx --no-build
git diff --check
```

Also verify that documentation and UI text remain English-only.
