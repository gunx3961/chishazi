# Project Memory

Last updated: 2026-06-11

This file records durable facts that future implementation sessions must retain.
It is not a backlog.

## Product

- Repository: `gunx3961/chishazi`
- Production URL: `https://gunx3961.github.io/chishazi/`
- Product type: personal random meal decision application
- Product purpose: help the owner decide what to eat
- The product is not a diet quality or nutritional assessment system.
- Intended user count: one
- Client: standalone Blazor WebAssembly
- Hosting: GitHub Pages
- Data source: one private Google Sheet
- Current data access: read-only
- The owner confirmed that live Google Sheets connectivity works.

## Technical Baseline

- SDK version is pinned in `global.json`.
- Application project: `src/Chishazi/Chishazi.csproj`
- Test project: `tests/Chishazi.Tests/Chishazi.Tests.csproj`
- Google API calls are direct browser-to-Google REST requests.
- OAuth uses Google Identity Services token mode.
- The browser stores no token persistently.
- Google consent persists independently, but a new short-lived access token is
  still required after page reload or token expiration.
- Durable token renewal requires a backend authorization code flow and secure
  refresh-token storage.
- Public configuration lives in `src/Chishazi/wwwroot/appsettings.json`.
- Public configuration contains only `ClientId` and `SpreadsheetId`.
- The default local HTTP origin is `http://localhost:5180`.
- The optional local HTTPS origin is `https://localhost:7180`.
- A complete value snapshot of every worksheet is cached in IndexedDB.
- `BrowserCacheService` is generic and must remain independent of spreadsheet
  schemas.
- GitHub Pages production files are built by
  `.github/workflows/pages.yml`.
- GitHub Pages is configured to use the GitHub Actions source.
- Generated Pages files are not committed to `/docs`.
- The deployment workflow does not run tests.

## Data Contract

- Worksheet definitions live in
  `src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs`.
- `WorksheetDefinition` is the largest contract unit.
- Current defined worksheet: `Foods`
- Required header: `name`
- Optional headers: `category`, `calories_kcal`, `protein_g`, `carbs_g`,
  `fat_g`, and `serving`
- Invalid rows are skipped and surfaced as validation messages.
- Search is case-insensitive and matches name or category.

## Durable Decisions

- No backend is used for the minimum version.
- No write scope is requested.
- No service account is used in the browser.
- Access tokens must not be stored in `localStorage`, `sessionStorage`, or
  IndexedDB.
- Raw spreadsheet values may be stored in IndexedDB, but Google OAuth
  credentials may not.
- Cache all returned worksheets and cell values before applying business
  definitions.
- Unknown worksheets and columns must survive the cache round trip.
- The owner has configured the public OAuth Client ID and Spreadsheet ID.
- The application uses English-only repository text and UI text.
- Existing search and nutrition display features remain until explicitly
  changed by a future product implementation request.
- Unrelated Godot files and `.godot` generated content are outside this
  project's ownership.

## Operational Configuration

- A Google Cloud project with the Sheets API enabled
- An OAuth Web Client ID
- Authorized JavaScript origins for local development and GitHub Pages
- A private spreadsheet matching the documented contract
- Public `ClientId` and `SpreadsheetId` values in `wwwroot/appsettings.json`
