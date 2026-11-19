# OmpPricing

Console app that reads a client Excel parts list, looks up open-market prices at distributors, and
writes the filled workbook into `output/`.

Steps 1 and 2 of `docs/plan.md` are done: the Excel input/output path with the A4/A6/A9 input rules,
the provider framework, the part matcher, and DigiKey as the first provider. Offer selection and the
compulsory columns 15–17 and 21 (rules/supplier-terms.json, rules/coo-rules.json) are Step 3.

The app never invents a price. With no provider enabled, or when a part is not found, the row is
written as NotFound and highlighted; the fake pricer used in Step 1 lives only in the test project.

## Requirements

- .NET SDK 10.0.x (`dotnet --version` must print 10.x)

## Run

From the repository root:

```
dotnet run --project src/OmpPricing.App -- --file tests/samples/LT2026100009.xlsx
```

The result is written to `output/<name>_<yyyyMMdd-HHmmss>.xlsx` (UTC timestamp, an existing file is
never overwritten). Logs go to the console and to
`src/OmpPricing.App/bin/<Configuration>/net10.0/logs/omp-pricing-<date>.log`. The match decisions are
kept in `state/match-cache.json`.

Options:

| Option | Meaning |
| --- | --- |
| `--file <path.xlsx>` | parts list to process (required) |
| `--packaging <code>` | restrict offers to a packaging short code; accepted now, applied in Step 3 |
| `--export-countries` | regenerate `rules/countries.txt` from Sheet1 of the `--file` workbook |
| `--help` | usage |

Relative paths (`rules/`, `output/`, `state/`) resolve against the current directory, so run from the
repository root.

## Providers and credentials

`appsettings.json` holds the provider structure only:

```json
"Providers": {
  "DigiKey": {
    "Enabled": false,
    "IsAuthorised": true,
    "BaseUrl": "https://api.digikey.com",
    "Site": "US",
    "Language": "en",
    "Currency": "USD",
    "RateLimits": { "PerSecond": 0, "PerMinute": 120, "PerDay": 1000 }
  }
}
```

Credentials are never in `appsettings.json`, tests, logs or commits. Use user-secrets:

```
dotnet user-secrets --project src/OmpPricing.App set Providers:DigiKey:ClientId "<value>"
dotnet user-secrets --project src/OmpPricing.App set Providers:DigiKey:ClientSecret "<value>"
dotnet user-secrets --project src/OmpPricing.App set Providers:DigiKey:AccountId "<value>"
dotnet user-secrets --project src/OmpPricing.App set Providers:DigiKey:Enabled true
```

or environment variables (`OMP_Providers__DigiKey__ClientId`, `…__ClientSecret`, `…__AccountId`,
`OMP_Providers__DigiKey__Enabled=true`). Enabling DigiKey without these stops the run at startup with
an error that names the missing settings and never prints their values.

## Build and test

```
dotnet build
dotnet test
```

No test calls a distributor API: DigiKey is exercised through saved JSON fixtures in
`tests/OmpPricing.Tests/Providers/DigiKey/fixtures/` and a stub `HttpMessageHandler`, and the pipeline
tests use a scripted `FakeProvider` from the test project.

## Layout

```
src/OmpPricing.App/
  Program.cs              argument parsing, configuration, logging, wiring, rule validation
  CommandLine.cs
  Excel/                  reader, writer, Offers sheet, highlights, validation guard, countries export
  Pricing/                input rules, column filler, compulsory checks, part matcher, match cache
  Providers/              IPriceProvider, PartQuery, ProviderResult, Offer, ProviderRegistry, Throttle
  Providers/DigiKey/      options, models, client, mapper, provider, factory — nothing else
  Pipeline/               IInputSource/FileInputSource, IResultSink/FolderResultSink, PricingPipeline
  Rules/                  countries, suffix rules, manufacturer aliases, supplier terms, COO rules
  appsettings.json        Excel positions, sheet name, folders, packaging map, provider sections
tests/OmpPricing.Tests/   Excel/, Pricing/, Pipeline/, Providers/, Rules/, Support/
tests/samples/            the client's real workbook
rules/                    countries.txt, suffix-rules.json, mfg-aliases.json,
                          supplier-terms.json, coo-rules.json (schemas in docs/decisions.md)
docs/                     plan.md, test-data.md, decisions.md
```

## Extension points

- `IInputSource` / `IResultSink`: today `FileInputSource` (a path from `--file`) and
  `FolderResultSink` (writes into `output/`). Email receiving or sending plugs in as new classes.
- `PricingPipeline.RunAsync(IInputSource, IResultSink)` knows nothing about files, mail or a
  distributor.
- Providers: one folder per distributor under `Providers/`, one `IPriceProvider`, one
  `IPriceProviderFactory` that the `ProviderRegistry` discovers by reflection. Code outside a
  provider folder never references its types (there is a test for that).

## Rule files

| File | Purpose |
| --- | --- |
| `rules/countries.txt` | allowed COO spellings, generated from `Sheet1` (257 names) |
| `rules/suffix-rules.json` | known packaging suffixes (seeded with Murata `D`) |
| `rules/mfg-aliases.json` | manufacturer aliases (seeded with Murata) |
| `rules/supplier-terms.json` | cancellation/reschedule window and DB/PB per provider; filled by purchasing, empty today |
| `rules/coo-rules.json` | COO per manufacturer (optional series); empty today |

All rule files are loaded and validated at startup. A `coo-rules.json` country that is not in
`rules/countries.txt` stops the run with an error naming the manufacturer and the country, so a value
the dropdown rejects is never written. While `supplier-terms.json` and `coo-rules.json` are empty,
columns 15, 16, 17 and 21 stay empty, so a priced row is `Incomplete`, never `Priced` — values are
never guessed.
