# Setup Guide

## 1. Create the Spreadsheet

Create a private Google Sheet. The application automatically prepares missing
defined worksheets in its local working copy and can create them in Google
Sheets after upload preview and confirmation.

The current `Recipe` contract uses this header row:

```text
name,description,tags
```

Example rows:

```text
Egg sandwich,Eggs on toasted bread,"quick,breakfast"
Fried rice,Uses leftover rice,"quick,dinner"
```

The current `Tag` contract uses this header row:

```text
id,displayName
```

Example rows:

```text
2ec31f236ff44a64bb3b162de87b7398,Quick
fe29d7e50e7d48d78f2d649f98c97e33,Breakfast
6c0d3d2e27cc4da185a612a1f97be622,Dinner
```

The current `Restaurant` contract uses this header row:

```text
name,description,tags,location
```

Example row:

```text
Noodle House,Late-night noodles,"6c0d3d2e27cc4da185a612a1f97be622",Shanghai Xuhui District
```

Restaurant locations are plain text for personal notes and local browsing
search. Map links search by Restaurant name. No Amap API key is required for
the generated URI links.

The application generates and manages Tag IDs. The Tags route exposes only
display names. The worksheet and header row do not need to be created manually.

For an existing worksheet using `value,name,active`, rename `value` to `id`,
rename `name` to `displayName`, and remove `active`. Preserve existing values as
IDs so Recipe references remain valid.

Keep the sheet private. Do not publish it to the web.

## 2. Get the OAuth Client ID

1. Open [Google Cloud Console](https://console.cloud.google.com/).
2. Create a project or select an existing project from the project menu.
3. Open **APIs & Services**, then **Library**.
4. Search for **Google Sheets API** and enable it.
5. Open **Google Auth Platform** and complete the application setup.
6. For a personal Gmail account, select an External audience.
7. During development, keep the application in Testing and add only your
   Google account under **Test users**.
8. Open **Google Auth Platform**, then **Clients**.
9. Select **Create client** and choose **Web application**.
10. Add these Authorized JavaScript origins:
    - `http://localhost`
    - `http://localhost:5180`
    - `https://gunx3961.github.io`
11. Create the client and copy the value labeled **Client ID**. It ends with
    `.apps.googleusercontent.com`.

Authorized JavaScript origins contain only scheme, host, and port. Do not add
the `/chishazi/` path. Google also creates a client secret, but this browser
application must not use or store it.

## 3. Get the Spreadsheet ID

Open the private spreadsheet and inspect its URL:

```text
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit
```

Copy the text between `/d/` and `/edit`. This is the stable Spreadsheet ID.
Do not use the numeric `gid` value; that is a Sheet ID for one worksheet tab.

## 4. Configure the Client

Edit `src/Chishazi/wwwroot/appsettings.json`:

```json
{
  "GoogleSheets": {
    "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
    "SpreadsheetId": "YOUR_SPREADSHEET_ID"
  }
}
```

The Client ID and Spreadsheet ID are public identifiers, not secrets. Never add
a client secret, service account JSON, access token, or refresh token.

Worksheet names and column contracts are defined in
`src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs`, not in client
configuration. The synchronization process reads every worksheet in the
spreadsheet.

## 5. Run Locally

```bash
dotnet restore Chishazi.slnx
dotnet run --project src/Chishazi
```

Open `http://localhost:5180` and select **Authorize and pull**. Complete
the Google authorization flow with the configured test account.

## 6. Verify the Data Path

- The configured account can load the private sheet.
- An account without sheet permission receives a permission error.
- Changing a sheet row appears after selecting **Pull from Google Sheets**.
- Reloading the page displays the last cached snapshot without Google
  authorization.
- **Preview upload** derives changes from the local working copy and baseline,
  then uses a fresh remote snapshot only to check intended upload targets.
- The first Google authorization requests the complete Sheets scope.
- Pull, preview, and upload reuse the same in-memory token until its reported
  expiration.
- If Google rejects the token with HTTP 401, the application requests a new
  token and retries the failed operation once.
- Reloading or reopening the application reuses the localStorage token while it
  remains valid.
- A remote change made after preview blocks the upload until a new preview is
  generated.
- Missing defined worksheets appear in upload preview and are created only
  after confirmation.
- Worksheet deletion, rename, and identity conflicts block upload.
- A cell difference that would overwrite an existing remote formula blocks
  upload.
- Selecting **Clear cache** removes the local spreadsheet snapshot.
- Local storage contains one short-lived access-token record with an absolute
  expiration time.
- IndexedDB and cookies contain no access token.
- Rows without a recipe name are reported and skipped.
- Recipe tag references missing from the Tag worksheet are reported.
- Restaurant tag references missing from the Tag worksheet are reported.
- The Tags route can batch-add Tags and rename their display names. Generated
  IDs are not shown or edited.
- The Recipe route can add multiple recipes to the local working snapshot in
  one action.
- The Restaurant route can add and edit multiple restaurants, search location
  text locally, and open a best-effort Amap app or web search by Restaurant
  name.
- Local additions from any type route accumulate in the same working snapshot.
- Pull is blocked while local changes are pending.
- **Review local changes** compares the working copy with the last synchronized
  snapshot without contacting Google. Changes are grouped by worksheet row.
- **Discard local changes** restores the last synchronized snapshot without
  pulling or changing Google Sheets.
- Return to the home page to preview and upload all pending changes together.

## 7. Deploy to GitHub Pages

One-time GitHub configuration:

1. Open repository **Settings**, then **Pages**.
2. Set **Source** to **GitHub Actions**.
3. Push the `master` branch or manually run **Deploy GitHub Pages** from the
   Actions tab.
4. Open `https://gunx3961.github.io/chishazi/`.

The workflow:

1. Publishes the Blazor WebAssembly application, restoring dependencies when
   needed.
2. Sets the base path to `/chishazi/`.
3. Creates a `404.html` application fallback for direct route access.
4. Adds `.nojekyll`.
5. Uploads and deploys the generated Pages artifact.

Generated production files are not committed to the repository.
