# Decisions

Owner decisions taken outside `AGENTS.md`. They are defaults until the owner changes them; record a
new entry rather than editing history.

## D1. A lowercase `x` inside an MPN is NeedsReview (accepted, Step 1)

A9.3 applies literally: only a trailing `x`, `xx` or `xxx` is a wildcard prefix. A lowercase `x`
anywhere else (for example `C1608X7R1H104K080xE`, `BAS16HTxG`) sends the row to NeedsReview with a
Remark; no provider call is made for that row.

## D2. Line status is not a column on the client sheet (accepted, Step 1)

The 22 columns stay exactly as A4 defines them. The status is visible in the `Offers` sheet, in
Remarks and through the row/cell highlighting. No extra status column, no extra sheet beyond
`Offers`.

## D3. Output file name is `<name>_<yyyyMMdd-HHmmss>.xlsx` (accepted, Step 1)

`FolderResultSink` appends the UTC timestamp of the run and never overwrites an existing file; if two
runs land in the same second, `-1`, `-2` … is added. The sender's workbook is still opened and edited,
never rebuilt.

## D4. Rule table schemas (accepted, Step 2)

### rules/supplier-terms.json

```json
[
  {
    "provider": "DigiKey",
    "manufacturer": null,
    "cancellationWeeks": 2,
    "rescheduleWeeks": 8,
    "basis": "PB"
  },
  {
    "provider": "DigiKey",
    "manufacturer": "Murata",
    "cancellationWeeks": 4,
    "rescheduleWeeks": 12,
    "basis": "DB"
  }
]
```

| Field | Type | Rule |
| --- | --- | --- |
| `provider` | string, required | Must match `IPriceProvider.Name` (for example `DigiKey`). |
| `manufacturer` | string or null | `null` (or absent) is the default row for that provider. A manufacturer row wins over the default. |
| `cancellationWeeks` | whole number, required, >= 0 | Column 15. |
| `rescheduleWeeks` | whole number, required, >= 0 | Column 16. |
| `basis` | `"DB"` or `"PB"`, required | Column 17. Delivery based or PO based. |

Manufacturer names are compared through `rules/mfg-aliases.json`, so `Murata Electronics` and
`Murata` hit the same row. An empty file (`[]`) means columns 15, 16 and 17 stay empty and the row is
Incomplete — never guessed.

### rules/coo-rules.json

```json
[
  { "manufacturer": "Murata", "series": null, "country": "Japan" },
  { "manufacturer": "Murata", "series": "BLM18", "country": "Taiwan" }
]
```

| Field | Type | Rule |
| --- | --- | --- |
| `manufacturer` | string, required | Matched through `rules/mfg-aliases.json`. |
| `series` | string or null | Optional narrowing; a series row wins over the manufacturer-only row. |
| `country` | string, required | Must appear in `rules/countries.txt` with exactly that spelling. |

The app loads this file at startup. A `country` that is not in `rules/countries.txt` stops the run
with an error naming the manufacturer and the country, instead of writing a value the dropdown
rejects. Matching the rules to a row (API value first, then these rules) is Step 3 work.

## D5. No provider configured means NotFound (accepted, Step 2)

When no provider is enabled in `Providers:*`, every quoteable line is written as NotFound with the
Remark `No price provider configured`. No synthetic or default price is ever written, and the fake
pricer used in Step 1 was moved into the test project.

A6 rule 9 lists the allowed statuses, and it has no `WaitingForProvider` value, so the "no provider"
case reuses NotFound rather than inventing a ninth status. A provider that runs out of its daily
quota keeps the documented `WaitingForQuota`.
