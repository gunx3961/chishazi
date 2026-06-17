# Development Log

## 2026-06-11

- Confirmed that .NET SDK 10 and the standalone Blazor WebAssembly template are
  available.
- Documented the browser-only OAuth and private Google Sheets architecture.
- Established repository-wide English, security, testing, and memory rules.
- Created the minimum Blazor WebAssembly application structure.
- Added the planned Google Identity Services authorization layer, Sheets API
  client, meal option parser, lookup UI, parser tests, and GitHub Pages workflow.
- Left Google public identifiers as placeholders because owner-specific values
  have not been provided.
- Verified a clean build, four passing parser tests in Debug and Release, a
  successful Release publish, and the unconfigured page at desktop and 390 px
  mobile widths in a local browser.
- Removed the browser's programmatic heading focus outline after visual review.
- Confirmed that the verified browser session produced no console errors.
- Release publishing works without the optional `wasm-tools` workload; install
  that workload later only if ahead-of-time WebAssembly optimization is needed.
- Replaced unsupported dynamic `localhost:0` launch URLs with fixed local ports:
  HTTP 5180 and HTTPS 7180. This keeps Kestrel startup and the Google OAuth
  authorized origin deterministic.
- Replaced the fixed worksheet range configuration with
  `WorksheetName`. The Sheets request now reads the entire worksheet by using
  the quoted worksheet name as the A1 range.
- Expanded setup documentation with exact Client ID and Spreadsheet ID
  discovery steps.
- Added request tests for whole-worksheet A1 references, including worksheet
  names containing spaces and apostrophes. The full test suite now has eight
  passing tests.
- Investigated persistent Google Sheets authorization. Documented that browser
  storage can only reuse an unexpired access token, cannot refresh it, and
  increases XSS impact. Retained memory-only tokens and recorded a backend
  authorization code flow as the requirement for durable renewal.
- Removed `WorksheetName` from public configuration.
- Added `SpreadsheetDefinition.cs` as the worksheet-level data contract source
  of truth and documented its maintenance rules.
- Changed synchronization to retrieve metadata and values for every worksheet
  in the configured spreadsheet.
- Added a schema-neutral `SpreadsheetSnapshot` and generic IndexedDB JSON cache.
  The application loads cached data at startup, supports explicit
  synchronization, and provides a clear-cache action.
- Added tests for arbitrary cache payload round trips and removal. The complete
  test suite now has twelve passing tests.
- Recorded the owner's confirmation that the live Google Sheets connection is
  working.
- Verified the empty-cache startup state in a local browser with no current-page
  console errors and without triggering Google authorization.
- Made cache write and clear failures non-destructive: freshly synchronized data
  remains usable even when IndexedDB is unavailable.
- Updated the product description: Chishazi is a random meal decision
  application, not a diet assessment system. Existing features were retained.
- Configured GitHub Actions to publish, prepare, upload, and deploy the GitHub
  Pages site after pushes to `master`.
- Generated production output is not committed to the repository.
- Removed tests from the deployment workflow to keep CI usage minimal. Tests
  remain available for local development.
- Centralized user-visible application text in `Resources/UiText.resx` with a
  resource-based localization entry point.
- Migrated Razor copy, service validation messages, API errors, and runtime
  error controls to the shared resource. Google authorization JavaScript now
  returns stable error codes instead of display text.

## 2026-06-12

- Replaced the initial business contract with the `Recipe` worksheet and its
  `name`, `description`, and comma-separated `tags` fields.
- Replaced nutrition parsing and presentation with Recipe parsing, validation,
  tag normalization, and a complete Recipe list.
- Removed the home page promotional headline and introduction.
- Added a clickable Recipe count to the synchronization module. The count
  toggles the complete Recipe list.
- Moved the synchronization module to the bottom of the home page so future
  application module entries can be placed above it.
- Removed the obsolete terminology from code, tests, and current
  documentation. Recipe terminology is now used consistently.
- Added schema-neutral cell comparison between the local cached snapshot and a
  fresh Google Sheets snapshot.
- Added upload preview with added, modified, and cleared cell details.
- Added confirmed Google Sheets writes using the write scope only at upload
  time.
- Added worksheet structure checks and a second remote comparison immediately
  before upload to prevent unexpected overwrites.
- Added formula-view checks that block changes to existing remote formula
  cells and detect formula changes made after preview.
- Added `/data` for global raw data browsing and `/data/recipes` for Recipe
  browsing and simple queries.
- Added a GitHub Pages `404.html` application fallback for direct access to
  client routes.
- Changed the synchronization module from raw row totals to defined-type
  counts. Recipe now links directly to its route.
- Added controlled Recipe tag values and validation for unknown existing tags.
- Added multi-Recipe creation with controlled tag selection.
- Added `SpreadsheetMutationService` so future type routes can append arbitrary
  batches to the shared schema-neutral working snapshot.
- Separated the synchronized baseline from the working snapshot. Pull is
  blocked while local changes are pending, and all type changes can be
  previewed and uploaded together.
- Replaced source-code Recipe Tag constants with a `Tag` worksheet containing
  stable values, display names, and active states.
- Added `/data/tags` for batch Tag creation, display-name editing, and
  activation management.
- Changed Recipe parsing and creation to resolve Tags from the local
  spreadsheet snapshot and offer only active Tags for new Recipes.
- Added automatic local initialization for missing defined worksheets,
  including contract header rows and temporary local IDs.
- Added worksheet creation to upload preview and confirmed Google Sheets batch
  updates, allowing Recipe and Tag data to be added before their remote
  worksheets exist.
- Kept unknown worksheets untouched and retained blocking checks for deletion,
  rename, and worksheet identity conflicts.
- Reduced the Tag contract to `id` and `displayName`.
- Changed Tag creation to generate opaque IDs automatically and removed ID and
  active-state controls from the user interface.
- Changed Recipe Tag references to use `Tag.id` while displaying only
  `Tag.displayName`.
- Recorded minimalism as a primary product and engineering principle.
- Added offline review of pending local changes against the cached synchronized
  baseline.
- Added confirmed discard of all local changes without contacting Google
  Sheets.
- Moved missing-worksheet initialization from cache reads to the first mutation
  for that data type, ensuring a discarded working copy stays clean.
- Replaced cell-by-cell diff presentation with compact worksheet-row groups.
- Kept cell-level formula protection and Google Sheets writes unchanged.
- Changed upload preview to a three-way comparison using working, baseline, and
  remote snapshots.
- Prevented unrelated remote worksheet value changes from appearing in or being
  included in an upload.
- Aligned the upload button summary with the worksheet-creation and row-change
  counts shown in the preview heading.
- Added application-session reuse for unexpired Google access tokens.
- Reused write tokens for later read operations and serialized token requests
  to prevent duplicate authorization dialogs.
- Initially kept OAuth tokens in memory only; this was later replaced by the
  localStorage persistence decision recorded below.
- Replaced incremental read/write authorization with one complete Sheets-scope
  token request.
- Changed token reuse to continue until reported expiration rather than using
  an early expiration buffer.
- Added one automatic reauthorization and retry after a Google Sheets 401
  response.
- Persisted the short-lived Google access token in localStorage with an absolute
  expiration time so reloads and later browser sessions can reuse it.
- Scoped the localStorage entry by OAuth Client ID and clear it on expiration or
  a matching Google Sheets 401 response.
- Kept access tokens out of IndexedDB, cookies, URLs, logs, and spreadsheet
  snapshot data.

## 2026-06-13

- Added inline editing for existing Recipes, including name, description, and
  controlled Tag selection.
- Preserved source row numbers in parsed Recipe models so edits update the
  correct worksheet row through the shared schema-neutral mutation service.
- Kept Recipe edits local until they are reviewed and uploaded through the
  existing synchronization flow.
- Preserved unknown existing Tag references when editing other Recipe fields
  so an edit does not silently remove unrelated source data.
- Updated three-way upload comparison to omit intended cell changes that are
  already present remotely, allowing remaining changes in the same batch to
  upload without a false conflict.
- Changed spreadsheet string comparison to use decoded values, preventing
  equivalent Unicode JSON encodings from being reported as remote edits.
- Changed the primary UI language to Chinese.
- Rewrote the complete UI resource set with concise, conversational, playful
  copy and replaced implementation terminology with action-oriented guidance.
- Updated repository language rules and localization documentation so Chinese
  is allowed only in user interface resources.
- Replaced the mixed Latin serif and sans-serif typography with a system UI
  font stack that provides consistent Chinese and English rendering across
  macOS, Windows, Android, and Linux.
- Replaced the remaining English navigation brand and startup page title with
  the Chinese product name.
- Removed the source repository link from the application footer.
- Removed the raw worksheet count from the home status text and unified cached
  and freshly loaded states under one last-loaded timestamp message.
- Added restrained, semantic emoji to primary navigation, page titles, major
  actions, data counts, and key status messages.

## 2026-06-15

- Added Restaurant as an equal-level meal choice type with name, description,
  shared Tag references, and free-text location fields.
- Added Restaurant parsing, validation, batch creation, editing, browsing, and
  search through `/data/restaurants`.
- Added the parsed Restaurant count to the home synchronization module.
- Added fixed-domain Amap URI search links that combine and encode Restaurant
  names and locations, attempt native app launch, and retain web fallback.
- Kept Restaurant changes inside the existing schema-neutral local mutation,
  preview, and confirmed upload flow.

## 2026-06-17

- Changed Restaurant map links to search by Restaurant name only, leaving
  location as local note and browsing-search text.
- Added platform-specific Amap application scheme navigation for Android and
  iOS with the web URI retained as fallback.
- Made existing Tag editing more compact by replacing full-width edit rows with
  dense editable Tag cards.
- Tightened small-screen Recipe and Restaurant entry forms by reducing nested
  spacing, input height, textarea height, and Tag chip padding.
- Changed the Amap web fallback to open in a new window and treat mobile page
  blur as a successful application handoff, preserving the current application
  page after map attempts.
