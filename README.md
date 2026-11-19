# OmpPricing

Console app that reads a client Excel parts list, looks up open-market prices at distributors
(DigiKey first; more providers later), and writes the filled workbook into `output/`.

Step 1 of `docs/plan.md` is done: the Excel input/output path, the input rules from `AGENTS.md`,
the rule files, and the pipeline skeleton. **Prices in Step 1 are fake on purpose** — the
`FakePricer` stand-in is replaced by the provider framework in Step 2. No distributor is called.

## Requirements

- .NET SDK 10.0.x (`dotnet --version` must print 10.x)
- Windows, macOS or Linux; the sample workbook is at `tests/samples/LT2026100009.xlsx`

## Run

From the repository root:

```
dotnet run --project src/OmpPricing.App -- --file tests/samples/LT2026100009.xlsx
```

The result is written to `output/LT2026100009.xlsx` (same file name as the input). Logs go to the
console and to `src/OmpPricing.App/bin/<Configuration>/net10.0/logs/omp-pricing-<date>.log`.

Options:

| Option | Meaning |
| --- | --- |
| `--file <path.xlsx>` | parts list to process (required) |
| `--packaging <code>` | restrict offers to a packaging short code; accepted now, applied in Step 3 |
| `--export-countries` | regenerate `rules/countries.txt` from Sheet1 of the `--file` workbook |
| `--help` | usage |

Relative paths (`rules/`, `output/`) resolve against the current directory, so run from the
repository root.

## Build and test

```
dotnet build
dotnet test
```

Tests never call a distributor API. They run the pipeline on the client's real sample file and
assert the results (145 rows, 67 generic rows, wildcard detection, multi-MPN cells, preserved
`Sheet1` and dropdowns, unchanged sender columns).

## Layout

```
src/OmpPricing.App/
  Program.cs            argument parsing, configuration, logging, wiring
  CommandLine.cs
  Excel/                read and write the workbook, countries exporter, validation guard
  Pricing/              input rules, column filler, compulsory-column checks, fake pricer
  Providers/            Offer: the normalized model every provider maps into
  Pipeline/             IInputSource/FileInputSource, IResultSink/FolderResultSink, PricingPipeline
  Rules/                CountryList (the allowed COO spellings)
  appsettings.json      Excel column positions, sheet name, folders, packaging map
tests/OmpPricing.Tests/ Excel/, Pricing/, Pipeline/, Support/
tests/samples/          the client's real workbook
rules/                  countries.txt, suffix-rules.json, mfg-aliases.json,
                        supplier-terms.json, coo-rules.json (seed content in docs/test-data.md)
```

## Extension points

- `IInputSource` / `IResultSink`: today `FileInputSource` (a path from `--file`) and
  `FolderResultSink` (writes to `output/`). Email receiving or sending plugs in as new classes.
- `PricingPipeline.RunAsync(IInputSource, IResultSink)` knows nothing about files, mail or a
  distributor.
- Providers: one folder per distributor under `Providers/` (Step 2), one normalized `Offer`.

## Configuration and secrets

`appsettings.json` holds structure only: sheet name, the 22 column positions, output/rules folders,
packaging map. Provider credentials (DigiKey client id, secret, account id) belong in user-secrets
(`dotnet user-secrets --project src/OmpPricing.App set DigiKey:ClientId "<value>"`) or environment
variables (`OMP_` prefix). Never in `appsettings.json`, code, tests or logs.

## Rule files

| File | Purpose |
| --- | --- |
| `rules/countries.txt` | allowed COO spellings, generated from `Sheet1` (257 names) |
| `rules/suffix-rules.json` | known packaging suffixes (seeded with Murata `D`) |
| `rules/mfg-aliases.json` | manufacturer aliases (seeded with Murata) |
| `rules/supplier-terms.json` | cancellation/reschedule window and DB/PB per provider; filled by purchasing, empty today |
| `rules/coo-rules.json` | COO per manufacturer when the API gives none; empty today |

While `supplier-terms.json` and `coo-rules.json` are empty, columns 15, 16, 17 and 21 stay empty,
so every priced row is `Incomplete`, never `Priced`. That is intended: values are never guessed.
