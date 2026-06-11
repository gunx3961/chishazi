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
- The minimum version is read-only. Do not request write scopes unless the
  product requirement changes.

## Security

- Never commit OAuth client secrets, service account keys, access tokens,
  refresh tokens, cookies, or exported Google credentials.
- OAuth Client IDs and Spreadsheet IDs are public identifiers and may be kept
  in client configuration, but use placeholders in the repository by default.
- Keep access tokens in memory only. Do not write them to browser storage,
  URLs, logs, analytics, or exception messages.
- Request only the
  `https://www.googleapis.com/auth/spreadsheets.readonly` scope.
- Treat all Google Sheet cell values as untrusted text. Do not render them as
  raw HTML.
- Spreadsheet snapshots may be stored in IndexedDB, but OAuth credentials may
  not be included in cached data.

## Engineering

- Target the .NET version declared by `global.json`.
- Keep Google authorization, Sheets transport, row parsing, and UI state in
  separate components.
- Keep browser caching schema-neutral. Cache raw spreadsheet snapshots and
  apply worksheet definitions after loading.
- Treat `src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs` as the source
  of truth for worksheet data contracts.
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

## Completion Checks

Run the checks that apply:

```bash
dotnet build Chishazi.slnx
dotnet test Chishazi.slnx --no-build
git diff --check
```

Also verify that documentation and UI text remain English-only.
