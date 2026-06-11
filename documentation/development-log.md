# Development Log

## 2026-06-11

- Confirmed that .NET SDK 10 and the standalone Blazor WebAssembly template are
  available.
- Documented the browser-only OAuth and private Google Sheets architecture.
- Established repository-wide English, security, testing, and memory rules.
- Created the minimum Blazor WebAssembly application structure.
- Added the planned Google Identity Services authorization layer, Sheets API
  client, food row parser, lookup UI, parser tests, and GitHub Pages workflow.
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
- Replaced the fixed `Foods!A1:G1000` range configuration with
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
