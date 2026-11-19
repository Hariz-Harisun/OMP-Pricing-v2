# AGENTS.md: OMP Pricing (simple version, .NET 10)

Standing instructions for the AI agent. Read this file and `docs/plan.md` at the start of every session. Test data is in `docs/test-data.md`. The client's real sample file is `tests/samples/LT2026100009.xlsx`.

## What we are building

A small .NET 10 console app that works on a located Excel file:
1. **Reads the file:** you give it the path to an `.xlsx` parts list (the client file).
2. **Processes it:** reads the parts list, finds the open-market price on distributor APIs (DigiKey first, more later), picks the best offer.
3. **Produces the filled Excel:** writes the results into a copy of the same workbook in `output/`.

Receiving files by email, sending the result back by email, a database, a web GUI and a Windows Service are separate later topics. Do not build them unless the plan says so.

Open-market price means the public list price with quantity breaks. Contract or customer-specific pricing (for example DigiKey MyPricing) must never be used. Never invent prices, API fields or endpoints.

## Working rules

- Do only the step you are given in `docs/plan.md`. Never start the next step.
- Before coding, give your plan in at most 10 lines and list your assumptions.
- Keep it simple: no database, no background-service framework, no dependency-injection layers beyond what the step needs. Prefer plain classes and small files.
- Secrets (DigiKey client id, secret and account id, and later other provider keys) go in user-secrets or environment variables only. Never in appsettings.json, code, tests, logs, reports or commits. Never repeat a password in a report.
- Automated tests never call live distributor APIs. Use saved JSON fixtures and a stub `HttpMessageHandler`. Live calls only in tests marked with the trait `Live`, excluded by default.
- Mark anything not confirmed by documentation or a sample here with `// UNVERIFIED` and list it in the report.
- Small commits. Run `dotnet build` and `dotnet test` before reporting.
- End every step with a report: (1) what was built, (2) files changed, (3) how to run it, (4) real build and test output with counts (skipped is not passed), (5) deviations from this file, (6) UNVERIFIED items, (7) questions.

## Structure

```
OmpPricing/
  OmpPricing.slnx
  src/OmpPricing.App/
    Program.cs
    Excel/                      read and write the workbook
    Pricing/                    part matcher, offer selector, column filler (provider-neutral)
    Pipeline/                   PricingPipeline, IInputSource + FileInputSource, IResultSink + FolderResultSink
    Providers/
      IPriceProvider.cs         the one contract every distributor implements
      Offer.cs                  the normalized offer model (the only thing the pipeline sees)
      ProviderRegistry.cs       builds the list of enabled providers from config
      Throttle.cs               shared rate limiter, configured per provider
      DigiKey/                  everything about DigiKey, and nothing else
        DigiKeyProvider.cs      implements IPriceProvider
        DigiKeyClient.cs        HTTP calls, token, headers
        DigiKeyModels.cs        response types
        DigiKeyMapper.cs        DigiKey response -> Offer
        DigiKeyOptions.cs       settings class
      Mouser/                   later: same five files
      Element14/                later: same five files
  tests/OmpPricing.Tests/
    Providers/DigiKey/          fixtures/*.json and tests for this provider only
    Pricing/  Excel/
  tests/samples/LT2026100009.xlsx
  rules/                        created by the agent in Step 1: supplier-terms.json, coo-rules.json, mfg-aliases.json, suffix-rules.json, countries.txt (seed content in docs/test-data.md)
  output/  state/               created at run time, in .gitignore
  docs/                         plan.md, test-data.md, decisions.md
```

## Extension points (so later features plug in without rewrites)

- **Input is an interface:** `IInputSource` gives the pipeline a workbook to process. Today only `FileInputSource` exists (a path from `--file`). Email receiving, a watched folder or an upload page can be added later as new implementations.
- **Output is an interface:** `IResultSink` receives the finished workbook. Today only `FolderResultSink` exists (writes to `output/`). Sending by email can be added later as a new implementation.
- **Providers are folders:** see the next section.
- `PricingPipeline.RunAsync(IInputSource, IResultSink)` is the only entry point. It knows nothing about files, mail or any distributor. Do not add other abstractions, base classes or layers unless a step asks for them.

## Providers (built to scale)

- **One folder per distributor** under `Providers/`. Everything specific to that distributor (auth, endpoints, response types, mapping, options) stays inside its folder. Code outside a provider folder never references a provider's types and never contains `if (provider == "DigiKey")`.
- **One contract:** `IPriceProvider` with a name and `FindAsync(PartQuery, CancellationToken)`, returning a `ProviderResult` (status Found, NotFound, Ambiguous, Throttled or Error, plus a list of `Offer`). `PartQuery` holds MPN, MFG, wildcard pattern and Qty.
- **One normalized `Offer`:** provider name, provider part number, MFG, MPN, currency, price breaks (quantity and unit price), MOQ, standard package, packaging, stock, lead weeks, marketplace flag, fixed fee, RoHS and REACH status, raw country of origin, tariff, NCNR, discontinued or end of life, and price type (list or contract). Each provider's mapper converts its own response into this model. The matcher, selector and Excel writer work only on `Offer`.
- **Config decides what runs:** `Providers:<Name>:Enabled`, rate limits (per second, per minute, per day), an `IsAuthorised` flag, and the credentials (user-secrets only). The registry builds the list from config. Adding a provider means a new folder, one registration line and one config section. It must not require changes to the Pricing or Excel code.
- **Throttle per provider:** each provider has its own limits (A5). When one is throttled or errors, the others still run and the row notes it in Remarks.
- **Merge and choose:** for each line the pipeline asks every enabled provider, merges all offers, and the shared selector picks the best one (A6). All offers go to the `Offers` sheet with the provider name.
- **Adding a provider, checklist:** copy the `DigiKey/` folder shape; write the five files; add fixtures and tests under `tests/.../Providers/<Name>/`; add the config section and the registry line; add its limits from A5 to config; list any unconfirmed field as `// UNVERIFIED`. Every provider must pass the same shared contract tests (an unknown part returns NotFound, HTTP 429 returns Throttled, a marketplace offer is flagged, contract prices are never returned as list prices).

Packages: ClosedXML (no EPPlus), Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.Configuration (json, user-secrets), Serilog with console and file sinks, xUnit. Use stable 10.0.x versions only, no preview packages.

Run: `OmpPricing.App --file <path.xlsx>` processes that file and writes the result to `output/`. Optional: `--packaging CT` to restrict to Cut Tape and Digi-Reel offers.

Settings in `appsettings.json` (structure only): Excel column positions and sheet name, output folder, and one `Providers:<Name>` section per distributor.


All times UTC. Pass `CancellationToken` through async methods. Use `decimal` for money.

---

## A4. Excel template (22 columns, 1-based positions)

The client fills columns 1 to 6. Columns 7 to 21 are compulsory: every one must hold a valid value before a row counts as priced. Only Remarks (column 22) may stay blank. Columns are read **by position, never by header name**, because the headers MFG and MPN appear twice. Positions come from configuration (Omp:Excel).

| Pos | Header | Filled by |
| --- | --- | --- |
| 1 | PN# | Sender |
| 2 | Description | Sender |
| 3 | MFG | Sender |
| 4 | MPN | Sender |
| 5 | Qty | Sender |
| 6 | EAU (Estimated Annual Usage) | Sender |
| 7 | Supplier | System |
| 8 | MFG (supplier side) | System |
| 9 | MPN (supplier side) | System |
| 10 | Currency | System |
| 11 | Unit Price | System |
| 12 | SPQ | System |
| 13 | MOQ | System |
| 14 | Lead time (WKS) | System |
| 15 | Cancellation window (WKS) | Rule table |
| 16 | Reschedule window (WKS) | Rule table |
| 17 | CXL & RES window DB/PB | Rule table |
| 18 | Packaging | System |
| 19 | Authorised Distributor (Yes/No) | System |
| 20 | RoHS/REACH (Yes/No) | System |
| 21 | COO | API, else rule table |
| 22 | Remarks | System (optional) |

Example input row: xxxxxxxxxx | Ferrite Bead 600ohm 0603 | Murata | BLM18HE601SN1 | 1 | 127500.

Target output: this is how a real supplier replied for the input row R100 | Thick Film Resistors - SMD General Purpose Chip Resistor 0201, 27kOhms, 1%, 1/20W | Yageo | RC0201FR-0727KL | 1 | 400 (Qty 1, EAU 400). Columns 7 to 22 of that reply:

| Pos | Column | Example value | Format |
| --- | --- | --- | --- |
| 7 | Supplier | WINCAP | Distributor name |
| 8 | MFG | YAGEO | Manufacturer name as the supplier lists it |
| 9 | MPN | RC0201FR-0727KL | Supplier-side MPN |
| 10 | Currency | USD | ISO 4217 code |
| 11 | Unit Price | 0.00052 | Decimal, full precision, no thousands separator |
| 12 | SPQ | 10000 | Whole number |
| 13 | MOQ | 10000 | Whole number |
| 14 | Lead time (WKS) | 22 | Whole weeks |
| 15 | Cancellation window (WKS) | 2 | Whole weeks |
| 16 | Reschedule window (WKS) | 8 | Whole weeks |
| 17 | CXL & RES window | PB | DB (delivery based) or PB (PO based) |
| 18 | Packaging | T&R | Short code: T&R, CT, Tray, Tube, Bulk |
| 19 | Authorised Distributor | Yes | Yes or No |
| 20 | RoHS/REACH | Yes | Yes or No |
| 21 | COO | Taiwan | Country name in English |
| 22 | Remarks | (blank) | Optional |

The supplier quoted tape and reel with MOQ 10000 and SPQ 10000 for a Qty of 1, and priced at the MOQ, not at Qty 1. The system must do the same (A6 rules 3 and 4).

Distributor APIs rarely return columns 15, 16, 17 and 21 (cancellation window, reschedule window, DB or PB, country of origin). They are still compulsory, so the system fills them from rule tables maintained by purchasing (A6 rule 6) and never invents a value. A row with any compulsory value missing is Incomplete, not Priced.

## A5. Distributor facts

All limits below come from the owner's notes and must live in configuration, not code.

| Provider | Rate rules | Parts per call | Access state |
| --- | --- | --- | --- |
| DigiKey v4 | 120 calls/min, 1,000 calls/day | 1 (ProductDetails) | Production app ready |
| Mouser | 30 calls/min, 1,000 calls/day | 10 (pricing lookup), 50 (keyword) | Key to request |
| element14 / Farnell / Newark | 2 calls/sec, 1,000 calls/day | 50 (the 100-item paging note is unconfirmed) | Key to request |
| LCSC (official) | Not stated; use a conservative default | 100 | Needs approval and company details |
| TrustedParts | 150 parts/min soft, 1,000 parts/min hard (counted in parts, not calls) | 50 | Needs Inventory API approval |
| Arrow | 3/sec and 120 per 3 minutes | 100 | Needs company details and a sales contact |
| Nexar (Octopart) | Monthly part quota (counts parts returned) | Batch of exact matches | Self-serve; plan to decide |
| Future, Avnet | Unknown | Unknown | Sales-gated; Avnet returns contract prices only, so it is not an OMP source |

**DigiKey details (confirmed):**

- Token: POST https://api.digikey.com/v1/oauth2/token, body application/x-www-form-urlencoded with client_id, client_secret, grant_type=client_credentials. The token lives about 600 seconds. Form-data or JSON bodies fail with client_id is null or undefined.
- Search: POST https://api.digikey.com/products/v4/search/keyword with JSON { Keywords, Limit, Offset }. A GET returns 404 Invalid resource path.
- Details: GET https://api.digikey.com/products/v4/search/{productNumber}/productdetails. The part number can be an MPN or a DigiKey part number. When an MPN matches several manufacturers it returns 404 Duplicate Products found; retry with the query parameter manufacturerId or use the DigiKey part number. URL-encode the comma in MPNs such as BAT54CW,115 as %2C.
- Required headers: Authorization: Bearer <token>, X-DIGIKEY-Client-Id, X-DIGIKEY-Account-Id (required for 2-legged OAuth), plus X-DIGIKEY-Locale-Site, X-DIGIKEY-Locale-Language, X-DIGIKEY-Locale-Currency. Read SearchLocaleUsed in the response to see what was applied. Response headers X-RateLimit-Limit and X-RateLimit-Remaining must be read and stored.
- Response shape: Products[] with ManufacturerProductNumber, Manufacturer{Id,Name}, ProductVariations[] (one per packaging: DigiKeyProductNumber, PackageType{Id,Name}, StandardPricing[]{BreakQuantity,UnitPrice,TotalPrice}, MyPricing[], MarketPlace, TariffActive, QuantityAvailableforPackageType, MaxQuantityForDistribution, MinimumOrderQuantity, StandardPackage, DigiReelFee), Classifications{RohsStatus,ReachStatus}, ManufacturerLeadWeeks (string), Ncnr, Discontinued, EndOfLife, ProductStatus. ExactMatches can be empty even when Products contains a valid packaging variant, so do not rely on it. MOQ exists only per variation. StandardPackage may be missing on the Tape and Reel variation.

Other providers: implement from their official documentation when credentials are available. Known notes: Mouser uses an API key in the query string, returns HTTP 200 even on errors (check an Errors array) and returns prices as localized text that must be parsed into decimals. element14 uses a key in the query string, term=manuPartNum:<MPN>, a storeInfo.id per regional store, and a basic key returns only list prices. Nexar is a GraphQL API at https://api.nexar.com/graphql with OAuth 2.0 client credentials; an exact-match multi-part query (supMultiMatch) counts one part per query. LCSC signs each request with key, secret, nonce and timestamp. TrustedParts uses a company ID plus API key.

## A6. Business rules

1. **Normalize input:** trim and collapse whitespace, detect wildcards and multi-MPN cells (A9) first, then uppercase the MPN, validate Qty and EAU as positive integers (EAU may be empty). Invalid rows get status InvalidInput; processing continues for other rows. Deduplicate identical MPN + MFG pairs inside a file so one lookup serves many rows.

2. **Matching:** classify each candidate MPN against the requested MPN as Exact, PackagingVariant (requested + a known suffix, for example BLM18HE601SN1 and BLM18HE601SN1D), Prefix (unknown tail) or None. Auto-accept Exact and PackagingVariant. Prefix goes to NeedsReview. Known suffixes live in `rules/suffix-rules.json`, seeded with Murata D only; every other suffix needs human approval. Check the manufacturer through a `rules/mfg-aliases.json` (for example Murata, Murata Electronics, Murata Manufacturing). Cache the decision in `state/match-cache.json` (input MPN + MFG to provider part number).

3. **Effective order quantity and eligible offers:** for each variation, effectiveQty = max(Qty, MOQ), rounded up to the provider's order multiple when one is stated. Do not exclude an offer because its MOQ is above the requested Qty: suppliers quote at the MOQ or reel quantity (see the example in A4: Qty 1, MOQ 10000). Exclude marketplace offers and offers whose stock is below effectiveQty.

4. **Price and ranking:** unit price = the break with the highest BreakQuantity not above effectiveQty. Effective unit price = (UnitPrice * effectiveQty + FixedFee) / effectiveQty, where FixedFee is the DigiKey DigiReelFee. Choose the lowest effective unit price; ties go to the lower effectiveQty, then the higher stock. Use decimal for money. Unit price, not total spend, is the ranking metric.

5. **Price basis:** Omp:Pricing:Basis is Qty (default) or EAU. With Qty, rule 3 uses the Qty column; with EAU it uses the EAU column. When EAU is present and a different offer wins at EAU, add that price to Remarks.

6. **Compulsory columns 7 to 21:** fill every one for the chosen offer.
   - Supplier: provider or distributor name. MFG and MPN: the supplier's values.
   - Currency: ISO code. Unit Price: decimal at full precision, no thousands separator (for example 0.00052).
   - MOQ: the chosen variation's MOQ. SPQ: the Tape and Reel MOQ when a reel variation exists, otherwise StandardPackage.
   - Lead time (WKS): whole weeks parsed from the API value.
   - Cancellation window (WKS), Reschedule window (WKS) and CXL & RES basis (DB or PB): not in distributor APIs. Read them from the `rules/supplier-terms.json` (per provider, with an optional manufacturer-specific row that wins).
   - Packaging: short code from a PackagingMap setting (Tape & Reel (TR) becomes T&R, Cut Tape (CT) becomes CT; other names pass through).
   - Authorised Distributor: Yes when the offer is not a marketplace offer and the provider is flagged authorised in configuration, otherwise No.
   - RoHS/REACH: Yes only when both statuses map to compliant through a ComplianceMap setting (for example ROHS3 Compliant and REACH Unaffected); No when the API states non-compliance; missing when unknown.
   - COO: country name in English. Order: the API value (convert ISO codes to names), then the `rules/coo-rules.json` (manufacturer and optional series), else missing.

7. **Remarks (optional):** leave it blank when the row has no exception, as in the supplier example. Remarks is the only optional column and never affects the Priced status. Use it only for exceptions: packaging variant of the requested MPN, MOQ above the requested Qty, price at EAU, tariff active, NCNR, discontinued or end of life, EAU above half of listed stock, needs review, not found, waiting for quota, and the names of missing compulsory columns.

8. **Output file:** keep the sender's columns 1 to 6 unchanged. Write results into a copy of the same workbook. Add a second sheet Offers listing every offer found per line, with the selected one marked. Highlight rows that are not Priced, and highlight each missing compulsory cell.

9. **Line statuses:** Queued, Priced, PricedReview, Incomplete, NotFound, NeedsReview, WaitingForQuota, InvalidInput. A line is Priced only when columns 7 to 21 all hold valid values in the A4 formats (a blank, a placeholder or a wrongly formatted value counts as missing). If an offer was chosen but any compulsory value is missing, the line is Incomplete: write the known values, highlight the gaps and name them in Remarks. The output file is always written, with incomplete rows highlighted.

## A9. Rules from the client's real file (tests/samples/LT2026100009.xlsx)

The real file has 145 rows on the sheet "LT Quote", the 22 columns from A4, and a second sheet "Sheet1" with 257 country names. It showed these cases. Apply these rules. They are defaults until the owner changes them (record them in docs/decisions.md).

1. **Line identity:** PN# is "xxxxxxxxxx" on every row, so never use it as a key. Identify a line by its Excel row number.
2. **Generic parametric rows:** when the MPN looks like a spec string (starts with `SMT;`, or the MFG is "Common" or "UnDefined"), it is not a real MPN. Make no API call. Set status NeedsReview and Remarks "Generic part: no real MPN". Do not count these rows against any provider quota.
3. **Wildcards:** a lowercase `x` (or `xx`, `xxx`) in an MPN marks an unknown packaging or variant suffix, for example BLM15GG221SZ1x, TPS70950DBVx, PESD5V0S1UA-xxx. Detect the wildcard BEFORE uppercasing. At the end of the MPN, treat the fixed part as a prefix and accept a candidate that starts with it and has at most 3 extra characters (match level PackagingVariant). A wildcard in the middle, or any other unknown tail, goes to NeedsReview.
4. **Multi-MPN cells:** a cell can hold several MPNs separated by "/" or line breaks with notes such as "(at)" or "(alt)". Look up the first MPN only, list the others in Remarks as alternates, and trim trailing spaces. Commas inside an MPN (BAT54CW,115) are part of the MPN. URL-encode them as %2C.
5. **Keep the client's workbook:** open the sender's file and write into it. Never rebuild the workbook. Preserve both sheets, the table on Sheet1, and the data validations: Yes/No lists on columns S and T, and the country list on column U (source Sheet1!A2:A258). After writing, reload the output and check that these validations still exist. ClosedXML can drop the column U validation, which is stored as an extension. If it does, restore it.
6. **COO values:** write the country exactly as spelled in Sheet1 (for example Taiwan, "Korea, Republic of", "Viet Nam"). A COO that is not in that list counts as missing.
7. **Yes/No columns:** write exactly `Yes` or `No` (the dropdown rejects other spellings).
8. **Headers:** header texts in the real file differ from A4 (for example "Supplier " with a trailing space). Never match columns by header text.
9. **EAU:** in this file EAU is usually Qty x 2550. It is never empty, but the system must still accept an empty EAU.
10. **Duplicates:** identical MFG + MPN pairs appear more than once (one pair in this file). One lookup serves all of them, but each row is still filled.

## Defaults

| Decision | Default |
| --- | --- |
| Quantity that drives Unit Price | Qty; the EAU-level price goes to Remarks |
| Rows per part in the output | One best offer per line, plus an `Offers` sheet listing all offers found |
| Packaging choice | Lowest effective unit price (as in the supplier example); optional `--packaging CT` command-line option |
| Quota exhausted | Mark remaining rows WaitingForQuota in the output and continue |
| Currency | No conversion; flag mixed currencies |
| Cancellation window, reschedule window, DB or PB | `rules/supplier-terms.json`, filled by purchasing; never guessed |
| COO | API value, then `rules/coo-rules.json`, else the row is Incomplete |
| Incomplete rows | The output is always written with them highlighted |
