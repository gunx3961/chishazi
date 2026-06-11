# Chishazi

Chishazi is a personal random meal decision application built as a standalone
Blazor WebAssembly site. It uses private Google Sheet data as the source for
deciding what to eat.

## Current Status

The repository contains the minimum viable application:

- Google Identity Services token authorization
- Read-only Google Sheets API integration
- Full-spreadsheet synchronization across all worksheets
- Schema-neutral IndexedDB snapshot cache
- Food row parsing and validation
- Name and category search
- GitHub Actions deployment workflow
- Unit tests for the sheet parser

Follow [the setup guide](documentation/setup.md) to connect or replace the private
spreadsheet configuration.

## Local Development

```bash
dotnet restore Chishazi.slnx
dotnet run --project src/Chishazi
```

Then open `http://localhost:5180`.

## Deploy GitHub Pages

Push the `master` branch. The `Deploy GitHub Pages` workflow runs the Release
publish and deploys the generated static site.

## Documentation

- [Workspace rules](AGENTS.md)
- [Architecture and feasibility](documentation/architecture.md)
- [Spreadsheet data contract](documentation/data-contract.md)
- [Setup guide](documentation/setup.md)
- [Project memory](documentation/project-memory.md)
- [Development log](documentation/development-log.md)
