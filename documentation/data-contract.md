# Spreadsheet Data Contract

Last updated: 2026-06-11

## Source of Truth

The application-level spreadsheet contract is defined in:

```text
src/Chishazi/DataDefinitions/SpreadsheetDefinition.cs
```

Update that file whenever a worksheet name, column name, column type, required
field, or validation constraint changes. Tests and parsers should reference
these definitions instead of duplicating worksheet contracts in configuration.

## Definition Boundary

A `WorksheetDefinition` is the largest unit of one data contract. Each
worksheet owns:

- its exact tab name
- its ordered set of known columns
- column data types
- required-field rules
- column validation flags

The spreadsheet definition is a collection of independent worksheet
definitions. Adding another worksheet does not change the browser cache format.

## Current Worksheets

### Foods

Definition property: `SpreadsheetDefinition.Foods`

| Column | Type | Required | Constraint |
| --- | --- | --- | --- |
| `name` | Text | Yes | Unique by convention |
| `category` | Text | No | None |
| `calories_kcal` | Decimal | No | Non-negative |
| `protein_g` | Decimal | No | Non-negative |
| `carbs_g` | Decimal | No | Non-negative |
| `fat_g` | Decimal | No | Non-negative |
| `serving` | Text | No | None |

The first worksheet row contains column headers. Unknown columns are retained
in the raw cached snapshot even when the current business parser does not use
them.

## Cache Independence

The cache stores a schema-neutral `SpreadsheetSnapshot`:

- spreadsheet ID and title
- synchronization timestamp
- every worksheet's ID, index, name, and type
- all returned cell values for every worksheet

The cache does not validate, rename, select, or discard cells based on
`SpreadsheetDefinition`. Business parsers apply definitions after loading the
snapshot. This allows a new application version to reinterpret an existing
cache after the contract changes.

Google Sheets omits trailing empty rows and columns from value responses. The
snapshot therefore represents all returned values, not the spreadsheet's
allocated empty grid size or formatting.
