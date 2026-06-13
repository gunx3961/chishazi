# Project Memory

Last updated: 2026-06-12

This file records durable facts that future implementation sessions must retain.
It is not a backlog.

## Product

- Repository: `gunx3961/chishazi`
- Production URL: `https://gunx3961.github.io/chishazi/`
- Product type: personal random meal decision application
- Product purpose: help the owner decide what to eat
- The product is not a diet quality or nutritional assessment system.
- Minimalism is a primary principle: expose only fields, controls, and states
  that serve a current user need.
- Intended user count: one
- Client: standalone Blazor WebAssembly
- Hosting: GitHub Pages
- Data source: one private Google Sheet
- Current data access: read and confirmed write
- The owner confirmed that live Google Sheets connectivity works.

## Technical Baseline

- SDK version is pinned in `global.json`.
- Application project: `src/Chishazi/Chishazi.csproj`
- Test project: `tests/Chishazi.Tests/Chishazi.Tests.csproj`
- Google API calls are direct browser-to-Google REST requests.
- OAuth uses Google Identity Services token mode.
- The short-lived Google access token is persisted in a dedicated localStorage
  entry keyed by OAuth Client ID.
- `GoogleAuthorizationService` requests the complete Sheets scope once and
  caches the token in application memory.
- The token is reused until its reported expiration.
- Reloading or reopening the application reuses the localStorage token when it
  has not expired.
- A Google Sheets 401 response invalidates the token and retries the failed
  operation once with a newly authorized token. Invalidation clears both memory
  and the matching localStorage entry.
- Concurrent token requests are serialized.
- Google consent persists independently, but a new short-lived access token is
  still required after token expiration, a 401 rejection, or local site-data
  removal.
- Durable token renewal requires a backend authorization code flow and secure
  refresh-token storage.
- Public configuration lives in `src/Chishazi/wwwroot/appsettings.json`.
- Public configuration contains only `ClientId` and `SpreadsheetId`.
- All user-visible runtime text is defined in
  `src/Chishazi/Resources/UiText.resx`.
- Culture-specific translations use sibling `UiText.<culture>.resx` files.
- The default local HTTP origin is `http://localhost:5180`.
- The optional local HTTPS origin is `https://localhost:7180`.
- A complete value snapshot of every worksheet is cached in IndexedDB.
- `BrowserCacheService` is generic and must remain independent of spreadsheet
  schemas.
- `SpreadsheetStore` owns access to the schema-neutral cached working snapshot.
- `SpreadsheetStore` separately caches the last synchronized baseline.
- `SpreadsheetDiffService` compares local and remote snapshots by worksheet,
  row, and column without business-type knowledge.
- `SpreadsheetMutationService` appends batches using worksheet definitions and
  column-value dictionaries without business-type knowledge.
- A missing defined worksheet is initialized with contract headers and a
  temporary negative ID only when data of that type is first added.
- Ordinary cache reads do not mutate the working snapshot.
- GitHub Pages production files are built by
  `.github/workflows/pages.yml`.
- GitHub Pages is configured to use the GitHub Actions source.
- Generated Pages files are not committed to `/docs`.
- The deployment workflow does not run tests.

## Data Contract

- Worksheet definitions live in
  `src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs`.
- `WorksheetDefinition` is the largest contract unit.
- Current defined worksheets: `Recipe` and `Tag`
- Required header: `name`
- Optional headers: `description` and `tags`
- The Recipe `tags` cell uses comma-separated references to `Tag.id`.
- Tag rows contain only `id` and `displayName`.
- Tag IDs are opaque, automatically generated, hidden from the user, and
  preserved when display names change.
- The Tag management interface exposes only display names.
- Unknown Recipe tag references remain visible and produce validation issues.
- Editing a Recipe preserves unknown Tag references that are not exposed as
  selectable controlled values.
- Invalid rows are skipped and surfaced as validation messages.
- The synchronization module lists defined data types and their parsed counts.
- Recipe and Tag are defined types linking to `/data/recipes` and `/data/tags`.
- `/data` provides global raw worksheet browsing and simple text search.
- `/data/recipes` provides Recipe browsing, search, controlled tag selection,
  multi-Recipe creation, and local editing of existing Recipes.
- `/data/tags` provides Tag creation and display-name editing.

## Durable Decisions

- No backend is used for the minimum version.
- All Sheets operations use the
  `https://www.googleapis.com/auth/spreadsheets` scope.
- No service account is used in the browser.
- Access tokens may be stored only in the dedicated localStorage authorization
  entry with an absolute expiration time.
- Access tokens must not be stored in IndexedDB, cookies, URLs, logs, or
  spreadsheet snapshots.
- Reuse the token until reported expiration to avoid repeated account and
  consent flows.
- Raw spreadsheet values may be stored in IndexedDB, but Google OAuth
  credentials may not.
- Cache all returned worksheets and cell values before applying business
  definitions.
- Unknown worksheets and columns must survive the cache round trip.
- Upload comparison and transport must remain independent of worksheet
  definitions.
- Local row mutation must remain independent of business models.
- All type routes append changes to one shared working snapshot so future types
  can be synchronized together.
- The last synchronized baseline is stored separately from the working
  snapshot.
- Pull is blocked while the working snapshot differs from the synchronized
  baseline.
- Pending local changes can be reviewed against the cached baseline without
  Google authorization or a network request.
- Discarding local changes restores the working snapshot from the cached
  baseline without contacting Google.
- Controlled values are stored in dedicated worksheets and managed through the
  application. They must not be hard-coded in source.
- Avoid adding user-visible fields or states before a concrete requirement
  needs them.
- Upload intent is derived only from the working snapshot compared with the
  synchronized baseline.
- Fresh remote data is used only to detect conflicts on intended upload targets.
- An intended cell change already matching the remote value is treated as
  satisfied and omitted from the upload.
- Spreadsheet string equality uses decoded values rather than JSON source
  encoding so cached Unicode escapes do not create false remote conflicts.
- Unrelated remote differences must never appear in or be written by an upload.
- Local and upload previews group changed cells by worksheet row and display
  only changed fields.
- Formula checks and confirmed writes remain cell-level internally.
- Reviewed creation of missing defined worksheets is allowed.
- Worksheet deletion, rename, identity conflicts, and unreviewed structure
  changes block upload.
- Upload rechecks the remote snapshot after preview and aborts on conflicts.
- Confirmed uploads update only changed cells in one Sheets batch request.
- The same confirmed batch creates reviewed missing worksheets before writing
  their cells.
- Unchanged cells and formatting are not rewritten.
- Upload preview reads a formula view and blocks any change that would
  overwrite an existing remote formula.
- Upload writes strings as literal values and does not create or edit formulas.
- The owner has configured the public OAuth Client ID and Spreadsheet ID.
- The application uses English-only repository text and UI text.
- JavaScript returns stable error codes; C# maps them to localized UI text.
- The home page has no promotional headline or introductory copy.
- The synchronization module remains the last module on the home page so
  future application module entries can be placed above it.
- Data browsing does not expand on the home page.
- Unrelated Godot files and `.godot` generated content are outside this
  project's ownership.

## Operational Configuration

- A Google Cloud project with the Sheets API enabled
- An OAuth Web Client ID
- Authorized JavaScript origins for local development and GitHub Pages
- A private spreadsheet matching the documented contract
- Public `ClientId` and `SpreadsheetId` values in `wwwroot/appsettings.json`
