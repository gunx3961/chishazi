# Setup Guide

## 1. Create the Spreadsheet

Create a private Google Sheet with a worksheet named `Foods`. Add this header
row:

```text
name,category,calories_kcal,protein_g,carbs_g,fat_g,serving
```

Example rows:

```text
Egg,Eggs,144,13.3,2.8,8.8,100 g
Cooked rice,Staples,116,2.6,25.9,0.3,100 g
```

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

Open `http://localhost:5180` and select **Authorize and sync**. Complete
the Google authorization flow with the configured test account.

## 6. Verify the Data Path

- The configured account can load the private sheet.
- An account without sheet permission receives a permission error.
- Changing a sheet row appears after selecting **Sync spreadsheet**.
- Reloading the page displays the last cached snapshot without Google
  authorization.
- Selecting **Clear cache** removes the local spreadsheet snapshot.
- Browser local storage and session storage contain no access token.
- IndexedDB contains spreadsheet data but no access token.
- Invalid numeric cells are reported and their rows are skipped.

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
3. Adds `.nojekyll`.
4. Uploads and deploys the generated Pages artifact.

Generated production files are not committed to the repository.
