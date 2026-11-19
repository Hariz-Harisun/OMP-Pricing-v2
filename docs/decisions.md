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

## D6. Match cache keeps only Exact and PackagingVariant, and expires in 30 days (accepted, Step 2)

`state/match-cache.json` stores one entry per MFG + MPN + provider, with `CachedAtUtc`, and only for
decisions the matcher accepts. Prefix and None are recomputed every run because they say nothing
about the part. Entries older than 30 days, entries without a timestamp (written before this rule) and
entries with a non-accepted level are dropped on load. `--no-cache` runs with a disabled cache: it
neither reads nor writes the file.

## D7. `--probe <provider> <MPN> [--qty N]` verifies A5 against the live API (accepted, Step 2)

One real lookup, a readable summary on stdout, and the raw exchanges saved to
`state/probe/<provider>_<MPN>_<yyyyMMdd-HHmmss>.json`. The file holds the request method and URL, the
non-credential request headers, the full response headers (including `X-RateLimit-*`) and the
untouched response body. The token request is never recorded, and every header whose name matches
`authorization|cookie|api[-_]?key|secret|token|client[-_]?id|account[-_]?id` is dropped from both
sides. Nothing is redacted from the body. The probe does not write a workbook and does not touch the
match cache. The owner runs it on their own PC; the agent never calls the live API.

## D8. An unresolved duplicate stays Ambiguous and names the candidates (accepted, Step 2)

When DigiKey answers `Duplicate Products found` and the retry with `manufacturerId` does not resolve
it, the line is NeedsReview and Remarks lists the manufacturers the keyword search reported, so
purchasing can pick one. It is never downgraded to NotFound.

## D9. Every packaging variation goes to the Offers sheet (accepted, Step 2)

An accepted match contributes all its variations as offers, each with its packaging, MOQ, SPQ, stock
and provider part number; exactly one is marked selected in column C.

## D10. Max quantity is used in Step 3, not now (accepted, Step 2)

`MaxQuantityForDistribution` becomes an exclusion rule in Step 3: an offer is dropped when the
effective quantity is above it, and 0 or a missing value means no limit. Recorded here so the number
is not applied twice. UNVERIFIED: the field is only known from the A5 notes, never seen in a live
response.

