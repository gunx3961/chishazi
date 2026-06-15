# Architecture and Feasibility

Last updated: 2026-06-15

## Product Purpose

Chishazi is a personal random meal decision application. Its purpose is to
help choose what to eat from privately maintained options. It is not a diet
assessment system and does not evaluate whether a meal is healthy.

Recipe and Restaurant are equal-level business data contracts used to display
the owner's available meal options.

Minimalism is a primary product principle. User workflows should expose only
information and controls needed for the current decision task. Internal
identifiers and unused lifecycle states remain outside the interface.

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

This architecture is feasible for a personal, interactive application.
GitHub Pages serves only public static assets, so authorization must be
performed by the user in the browser. Google Sheets checks both the OAuth token
and the spreadsheet access control list before reading or writing data.

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

The selected design persists the short-lived access token in `localStorage` so
page reloads and later browser sessions can reuse it until expiration. The
stored record contains only the access token and its absolute expiration time,
under a key scoped by OAuth Client ID.

This is an explicit convenience-over-isolation decision:

- It avoids authorization only while the current short-lived token remains
  valid.
- It cannot renew an expired token because the browser token model does not
  issue a refresh token.
- Any script running in the same origin can read or use the stored token.
- The current scope can read and write every spreadsheet available to the
  authorized account, so token theft has wider impact than this application's
  configured spreadsheet.
- Client-side encryption would not create a meaningful security boundary
  because the application must also possess the decryption capability.
- `localStorage` persists after the page and browser close until expiration,
  explicit invalidation, or site-data removal.

The token is not stored in cookies or IndexedDB and is never included in the
spreadsheet snapshot cache.

Google records the user's consent separately from the access token. With an
existing Google session and previously granted consent, a later authorization
request should normally skip the consent screen. The application already uses
an empty `prompt` value for this behavior. Google still requires a token request
and browser privacy controls may cause an account or sign-in dialog to appear.
An automatic `prompt: "none"` request on page load is not considered reliable
for this application because the token flow is dialog-based and token renewal
is documented as a user-driven operation.

The selected browser-only experience is:

1. Keep the active token in memory and persist it in the dedicated
   `localStorage` authorization entry.
2. Request the complete Sheets scope in the first authorization flow.
3. Cache the single token in the application-scoped authorization service until
   its reported expiration.
4. Reuse that token for pull, preview, conflict checks, upload, and refresh.
5. If Google returns HTTP 401, invalidate the token, request a replacement, and
   retry the failed operation once.
6. Require a new token request only after reported expiration, an actual 401
   response, or local site-data removal.
7. Load the last schema-neutral spreadsheet snapshot from IndexedDB at startup.
8. Clearly label cached data with its last successful synchronization time.
9. Replace the cached snapshot only after a successful full synchronization.

The authorization service serializes token requests so concurrent operations do
not open multiple token dialogs. JavaScript validates the stored absolute
expiration before returning a persisted token. A 401 response removes matching
memory and localStorage entries before one reauthorization attempt.
The normal interaction count in one application session is one complete
authorization. All later operations reuse that token until Google reports or
rejects it as expired.

True long-lived authorization requires an authorization code flow and a
backend. The backend exchanges the authorization code, stores the refresh token
encrypted at rest, refreshes access tokens server-side, and exposes only the
required recipe data to the browser through an authenticated session. A refresh
token must never be placed in GitHub Pages assets or browser storage.

## Scope Choice

All Google Sheets operations request:

```text
https://www.googleapis.com/auth/spreadsheets
```

This scope applies to spreadsheets the authorized account can access, not only
the configured spreadsheet. The application reduces exposure by using a fixed
Spreadsheet ID, requiring preview and explicit confirmation before writes,
avoiding unnecessary third-party scripts, and keeping tokens in memory.

## Data Flow

1. The application loads public configuration from `wwwroot/appsettings.json`.
2. The application loads a cached `SpreadsheetSnapshot` from IndexedDB when one
   exists.
3. Business parsers apply worksheet definitions to the cached snapshot.
4. The user selects the pull button when fresh data is needed.
5. JavaScript calls the Google Identity Services token client.
6. Google returns a short-lived access token to the browser.
7. Blazor calls `spreadsheets.get` for metadata and worksheet titles.
8. Blazor calls `spreadsheets.values.batchGet` with every worksheet title.
9. The application normalizes all returned values into one schema-neutral
   `SpreadsheetSnapshot` and atomically replaces the IndexedDB entry.
10. Business parsers apply current definitions and route-specific browsers
    display valid rows and validation messages.

## Upload Flow

1. The cached `SpreadsheetSnapshot` is the local working copy.
2. Upload preview derives intended changes from the working snapshot compared
   with the synchronized baseline.
3. Read-only authorization pulls a fresh remote snapshot. The remote snapshot
   is used only to check conflicts on intended target worksheets and cells.
4. Missing worksheets declared by `SpreadsheetDefinition` are represented as
   reviewed creation operations. Other additions, removals, identity conflicts,
   and renames remain blocking structural changes.
5. A separate formula-rendered snapshot detects remote formula cells. Any
   change that would overwrite an existing formula is blocked.
6. Cell differences are grouped by worksheet row for a compact preview. Each
   row lists only the fields that changed.
7. Confirmation reuses the shared token and pulls both remote views again.
8. Confirmation repeats the three-way check. A remote change to an intended
   target value or formula aborts the upload. Unrelated remote value changes are
   not added to or written by the upload.
9. `GoogleSheetsClient` sends one `spreadsheets.batchUpdate` request containing
   `addSheet` operations for reviewed worksheet creations followed by one-cell
   `updateCells` operations for changed cells.
10. Unchanged cells are not written, so existing formulas and formatting remain
   untouched.
11. The application pulls the committed remote state and replaces the local
    cached snapshot.

The upload layer does not know about Recipe or any future worksheet data type.
Business modules may update the cached working snapshot through a separate
feature, while synchronization continues to compare raw cell values. String
values are written as literal strings; this upload path does not create or edit
formulas.

Row-level diff presentation is separate from write granularity.
`SpreadsheetDiffService` retains individual cell changes for formula checks and
`updateCells` requests, while `SpreadsheetChangeSet.RowChanges` groups those
cells only for display. This preserves narrow writes without producing a large
cell-by-cell interface.

The upload comparison is three-way:

- `working - baseline` defines the complete upload intent.
- `remote - baseline` is inspected only at intended target cells and worksheet
  identities.
- unrelated remote value differences are ignored by the upload change set and
  remain untouched.

## Local Working Copy

`SpreadsheetStore` maintains two schema-neutral snapshots:

- the working snapshot used by all data routes
- the baseline from the last successful pull or upload

`SpreadsheetMutationService` appends batches to the working snapshot by using a
`WorksheetDefinition` and column-value dictionaries. It does not reference
Recipe models. Multiple routes can therefore add any number of records for
different future types before one combined upload.

When data is first added for an absent defined worksheet, the mutation service
creates a local-only worksheet with a temporary negative ID and the
definition's ordered header row. Ordinary cache reads do not create worksheets,
so the working snapshot can remain exactly equal to the synchronized baseline.
Worksheet creation remains visible in local review and upload preview.

Pull is blocked when the working snapshot differs from the baseline. This
prevents pending local additions from being silently replaced by remote data.
A successful pull or upload updates both snapshots.

Local review compares the working snapshot with the cached baseline and does
not contact Google. Discarding local changes replaces the working snapshot with
that baseline, also without contacting Google. Upload preview remains a
separate operation because it must fetch current remote values and formulas
before write confirmation.

Recipe and Restaurant creation consume this mechanism. Both routes support
multiple drafts in one action and serialize Tag references using the separator
declared by the `tags` column definition.

## Sheet Contract

Worksheet contracts are not client configuration. Their source of truth is
`src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs`. A
`WorksheetDefinition` is the largest contract unit and owns its tab name,
columns, types, and validation flags.

| Header | Required | Type |
| --- | --- | --- |
| `name` | Yes | Text, unique by convention |
| `description` | No | Text |
| `tags` | No | Comma-separated controlled text |

Restaurant adds one optional `location` text column. It is stored as entered
and combined with the Restaurant name only when building an Amap search URI.

The `Tag` worksheet is the data-source-backed controlled-value catalog:

| Header | Required | Type |
| --- | --- | --- |
| `id` | Yes | Automatically generated opaque identifier |
| `displayName` | Yes | User-managed display text |

Recipe and Restaurant rows reference `Tag.id`. The application generates IDs
when Tags are created, preserves them when display names change, and never
exposes them in the Tag management interface. All Tags are available to both
types of draft.

## Map Integration

`AmapUriBuilder` creates links to the fixed
`https://uri.amap.com/search` endpoint. It combines the Restaurant name and
free-text location, URL-encodes the complete keyword, requests map view, and
sets `callnative=1`.

On a supported mobile browser with Amap installed, the URI service can attempt
to open the native application. Otherwise it falls back to Amap's web
experience. Native application launch is therefore best-effort and depends on
the browser, operating system, and installed applications. The application
does not call a geocoding API and does not require an Amap API key for this URI
link.

The first row contains headers. The batch values request uses
`UNFORMATTED_VALUE` so the cached snapshot remains independent of display
formatting.

See `documentation/data-contract.md` for the maintained contract.

## Local Data Cache

`BrowserCacheService` is a generic JSON cache backed by IndexedDB. It exposes
typed get, set, and remove operations but has no knowledge of Google Sheets,
worksheets, headers, or recipe records.

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
  It also owns in-memory reuse, 401 retry behavior, and persisted-token
  invalidation.
- `GoogleSheetsClient` owns metadata and batch value requests and produces a
  complete `SpreadsheetSnapshot`.
- `BrowserCacheService` owns generic IndexedDB JSON persistence.
- `SpreadsheetStore` owns the cached working snapshot key and validation.
- `SpreadsheetDiffService` owns schema-neutral structural and cell comparison.
- `SpreadsheetMutationService` owns schema-neutral worksheet initialization and
  batch row additions.
- `SpreadsheetDefinition` owns worksheet-level business contracts.
- `RecipeSheetParser` applies the Recipe definition to a snapshot.
- `RestaurantSheetParser` applies the Restaurant definition to a snapshot.
- `TagSheetParser` applies the Tag definition and validates required values and
  unique IDs.
- `AmapUriBuilder` owns safe construction of fixed-domain Restaurant search
  links.
- `Home.razor` owns synchronization, upload preview, and defined-type counts.
- `DataBrowser.razor` owns global raw worksheet browsing and search.
- `Recipes.razor` owns Recipe browsing, search, and batch creation.
- `Restaurants.razor` owns Restaurant browsing, search, batch creation, editing,
  and map links.
- `Tags.razor` owns Tag creation and display-name editing while generating and
  preserving internal IDs.
- `google-auth.js` is the only direct caller of Google Identity Services.
  It is also the only direct owner of the localStorage token entry.
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
- The workflow copies the rewritten application shell to `404.html` so direct
  GitHub Pages route access starts Blazor routing.
- A `.nojekyll` file is included in the published artifact so `_framework`
  assets are served.
- Application browsing routes are `/data`, `/data/recipes`,
  `/data/restaurants`, and `/data/tags`.

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
- [Google Sheets API spreadsheets.batchUpdate](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/batchUpdate)
- [Google Sheets API UpdateCellsRequest](https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/request#UpdateCellsRequest)
- [Amap URI API overview](https://lbs.amap.com/api/uri-api/summary)
- [Amap URI search](https://lbs.amap.com/api/uri-api/guide/search/search)
