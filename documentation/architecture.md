# Architecture and Feasibility

Last updated: 2026-06-11

## Product Purpose

Chishazi is a personal random meal decision application. Its purpose is to
help choose what to eat from privately maintained options. It is not a diet
assessment system and does not evaluate whether a meal is healthy.

Existing food fields and lookup views support the current data exploration
workflow. They do not define the long-term product purpose.

## Architecture Decision

The minimum product uses a browser-only architecture:

```text
GitHub Pages
  -> Blazor WebAssembly
  -> Google Identity Services
  -> short-lived OAuth access token
  -> Google Sheets API v4
  -> private Google Sheet
```

This architecture is feasible for a personal, interactive, read-only
application. GitHub Pages serves only public static assets, so authorization
must be performed by the user in the browser. Google Sheets checks both the
OAuth token and the spreadsheet access control list before returning data.

## Security Boundary

The deployed application shell, JavaScript, WebAssembly, OAuth Client ID, and
Spreadsheet ID are public. None of them grants access to private sheet data.

The application must never contain:

- an OAuth client secret
- a service account private key
- an access token or refresh token checked into source control
- a long-lived credential embedded in a static asset

The access token is requested through Google Identity Services after a user
gesture and retained only long enough to perform the API request.

## OAuth Operating Modes

For a personal Gmail account, configure an External OAuth audience.

- During development, keep the application in Testing and add only the owner as
  a test user. Authorization grants for Sheets scopes expire after seven days.
- For long-term use, the application can be moved to In production. A personal
  application with fewer than 100 users may remain unverified, but Google shows
  an unverified application warning.

In production, another Google account can start the OAuth flow, but it still
cannot read the private spreadsheet unless that account has sheet permission.
The spreadsheet access control list is the durable data authorization boundary.

For a Google Workspace account, an Internal audience may be used when the Cloud
project and user belong to the same organization.

## Authorization Persistence

The browser token model does not provide a durable Google Sheets connection.
Google issues a short-lived access token and does not store it for the
application. After expiration, the application must call
`requestAccessToken()` again from a user-driven event.

Persisting the access token in `localStorage`, `sessionStorage`, or IndexedDB is
not the selected design:

- It only avoids authorization while the current short-lived token remains
  valid.
- It cannot renew an expired token because the browser token model does not
  issue a refresh token.
- Any script running in the same origin can read or use the stored token.
- The current scope can read every spreadsheet available to the authorized
  account, so token theft has a wider impact than this application's configured
  spreadsheet.
- Client-side encryption does not create a meaningful security boundary because
  the application must also possess or access the decryption capability.

Google records the user's consent separately from the access token. With an
existing Google session and previously granted consent, a later authorization
request should normally skip the consent screen. The application already uses
an empty `prompt` value for this behavior. Google still requires a token request
and browser privacy controls may cause an account or sign-in dialog to appear.
An automatic `prompt: "none"` request on page load is not considered reliable
for this application because the token flow is dialog-based and token renewal
is documented as a user-driven operation.

The selected browser-only experience is:

1. Keep access tokens in memory only.
2. Require one authorization button action after a new page load or token
   expiration.
3. Load the last schema-neutral spreadsheet snapshot from IndexedDB at startup.
4. Clearly label cached data with its last successful synchronization time.
5. Replace the cached snapshot only after a successful full synchronization.

True long-lived authorization requires an authorization code flow and a
backend. The backend exchanges the authorization code, stores the refresh token
encrypted at rest, refreshes access tokens server-side, and exposes only the
required food data to the browser through an authenticated session. A refresh
token must never be placed in GitHub Pages assets or browser storage.

## Scope Choice

The minimum product requests:

```text
https://www.googleapis.com/auth/spreadsheets.readonly
```

This scope can read spreadsheets the authorized account can access, not only
the configured spreadsheet. The application reduces exposure by using a fixed
Spreadsheet ID, avoiding unnecessary third-party scripts, and keeping the token
in memory. A future version may evaluate `drive.file` with Google Picker for a
narrower per-file grant, at the cost of additional implementation complexity.

## Data Flow

1. The application loads public configuration from `wwwroot/appsettings.json`.
2. The application loads a cached `SpreadsheetSnapshot` from IndexedDB when one
   exists.
3. Business parsers apply worksheet definitions to the cached snapshot.
4. The user selects the synchronization button when fresh data is needed.
5. JavaScript calls the Google Identity Services token client.
6. Google returns a short-lived access token to the browser.
7. Blazor calls `spreadsheets.get` for metadata and worksheet titles.
8. Blazor calls `spreadsheets.values.batchGet` with every worksheet title.
9. The application normalizes all returned values into one schema-neutral
   `SpreadsheetSnapshot` and atomically replaces the IndexedDB entry.
10. Business parsers apply current definitions and the UI displays valid rows
    and validation messages.

## Sheet Contract

Worksheet contracts are not client configuration. Their source of truth is
`src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs`. A
`WorksheetDefinition` is the largest contract unit and owns its tab name,
columns, types, and validation flags.

| Header | Required | Type |
| --- | --- | --- |
| `name` | Yes | Text, unique by convention |
| `category` | No | Text |
| `calories_kcal` | No | Non-negative decimal |
| `protein_g` | No | Non-negative decimal |
| `carbs_g` | No | Non-negative decimal |
| `fat_g` | No | Non-negative decimal |
| `serving` | No | Text |

The first row contains headers. Numeric cells must contain plain values without
units. The batch values request uses `UNFORMATTED_VALUE` to avoid
locale-dependent display formatting.

See `documentation/data-contract.md` for the maintained contract.

## Local Data Cache

`BrowserCacheService` is a generic JSON cache backed by IndexedDB. It exposes
typed get, set, and remove operations but has no knowledge of Google Sheets,
worksheets, headers, or food records.

The current cache value is a `SpreadsheetSnapshot` containing every worksheet
and all returned cell values. Unknown worksheets and columns remain in the
snapshot. Definitions are applied only after loading, so schema evolution does
not require changing the cache service.

The cache intentionally contains private spreadsheet data and persists until
the browser evicts site data or the user selects **Clear cache**. Any script
running under the same origin can access it. The cache must never contain
access tokens, refresh tokens, cookies, or authorization responses.

The Google Values API omits trailing empty rows and columns. Formatting,
comments, charts, formulas as source expressions, and allocated empty grid size
are not part of the current snapshot. The cache covers worksheet values rather
than every spreadsheet presentation property.

## Component Boundaries

- `GoogleAuthorizationService` owns JavaScript interop for token acquisition.
- `GoogleSheetsClient` owns metadata and batch value requests and produces a
  complete `SpreadsheetSnapshot`.
- `BrowserCacheService` owns generic IndexedDB JSON persistence.
- `SpreadsheetDefinition` owns worksheet-level business contracts.
- `FoodSheetParser` applies the Foods definition to a snapshot.
- `Home.razor` owns interaction state, filtering, and presentation.
- `google-auth.js` is the only direct caller of Google Identity Services.
- `browser-cache.js` is the only direct caller of IndexedDB.

## Deployment

- The GitHub Pages base path is `/chishazi/`.
- `.github/workflows/pages.yml` publishes the application after a push to
  `master`.
- GitHub Pages uses the GitHub Actions source.
- The workflow rewrites the published `<base href>` instead of changing local
  development behavior.
- The workflow removes stale compressed `index.html` variants after the base
  path rewrite.
- A `.nojekyll` file is included in the published artifact so `_framework`
  assets are served.
- The minimum application uses only the root route. Additional routes require a
  Pages-compatible fallback strategy.

## Rejected Designs

| Design | Reason |
| --- | --- |
| API key for private data | An API key does not represent the user's sheet permission |
| Service account key in Blazor | Static assets expose the private key |
| OAuth client secret in Blazor | Browser clients cannot keep a secret |
| Published or public sheet | Violates the private data requirement |
| Backend in the minimum version | Adds hosting and credential operations before they are needed |
| Apps Script proxy in the minimum version | Adds deployment and authorization complexity |

## Official References

- [GitHub Pages overview](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages)
- [GitHub Pages custom workflows](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)
- [Blazor WebAssembly deployment](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)
- [Google Identity Services token model](https://developers.google.com/identity/oauth2/web/guides/use-token-model)
- [Google OAuth web client setup](https://developers.google.com/identity/oauth2/web/guides/get-google-api-clientid)
- [Google Sheets API spreadsheets.get](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/get)
- [Google Sheets API values.batchGet](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets.values/batchGet)
