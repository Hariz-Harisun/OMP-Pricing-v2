# Reference test data: DigiKey, BLM18HE601SN1, site US, currency USD

Use this for pricing tests in Phases 3 and 4. Never call the live API in tests.

Build a JSON fixture in the DigiKey v4 KeywordSearch shape for one product: manufacturer Murata Electronics (ID 490), ManufacturerProductNumber BLM18HE601SN1D, QuantityAvailable 168243, ManufacturerLeadWeeks 24, ProductStatus.Status Active, Ncnr false, Discontinued false, EndOfLife false, ExactMatches empty, Classifications.RohsStatus ROHS3 Compliant, ReachStatus REACH Unaffected.

Variations (all MarketPlace false, TariffActive true, MyPricing empty):

| Variation | DigiKey P/N | MOQ | Stock | StandardPackage | DigiReelFee | Price breaks (qty: unit price) |
| --- | --- | --- | --- | --- | --- | --- |
| Tape & Reel (TR) | 490-5214-2-ND | 4000 | 168000 | missing | 0 | 4000: 0.08825 |
| Cut Tape (CT) | 490-5214-1-ND | 1 | 168243 | 1 | 0 | 1: 0.35, 10: 0.25, 25: 0.216, 50: 0.1932, 100: 0.1721, 250: 0.14656, 500: 0.12888, 1000: 0.11248 |
| Digi-Reel | 490-5214-6-ND | 1 | 168243 | 1 | 7.00 | same breaks as Cut Tape |

## Expected selector results (basis Qty, A6 rules 3 and 4)

Effective quantity is the larger of Qty and the variation's MOQ; the lowest effective unit price wins.

| Qty | Chosen variation | Effective qty | Unit price | MOQ and SPQ |
| --- | --- | --- | --- | --- |
| 1 | Tape & Reel (Cut Tape would be 0.35) | 4000 | 0.08825 | 4000 and 4000 |
| 10 | Tape & Reel | 4000 | 0.08825 | 4000 and 4000 |
| 1000 | Tape & Reel (Cut Tape would be 0.11248) | 4000 | 0.08825 | 4000 and 4000 |
| 4000 | Tape & Reel | 4000 | 0.08825 | 4000 and 4000 |
| 127500 | Tape & Reel (stock 168000 covers it) | 127500 | 0.08825 | 4000 and 4000 |
| 200000 | None eligible (stock below 200000); status NeedsReview | 200000 | none | none |

With the option `--packaging CT`, only Cut Tape and Digi-Reel compete:
- Qty 1: Cut Tape at 0.35 (Digi-Reel's effective price is 7.35 because of the 7.00 fee).
- Qty 1000: Cut Tape at 0.11248 (Digi-Reel's effective price is 0.11948).
- Qty 4000: Cut Tape at 0.11248.

## Expected match

Requested BLM18HE601SN1 against candidate BLM18HE601SN1D is PackagingVariant.

## Expected output row (Qty 1, EAU 127500, no supplier-terms and no coo-rules entries)

Supplier DigiKey; MFG Murata Electronics; MPN BLM18HE601SN1D; Currency USD; Unit Price 0.08825; SPQ 4000; MOQ 4000; Lead time 24; Packaging T&R; Authorised Yes; RoHS/REACH Yes.

Columns 15, 16, 17 and 21 are empty, so the status is Incomplete, those four cells are highlighted, and Remarks names them and notes: packaging variant of the requested MPN; MOQ 4000 is above the requested Qty 1; tariff active.

## Same row after test-only rule entries are added

`rules/supplier-terms.json` for DigiKey: 2, 8, PB. `rules/coo-rules.json` for Murata: Taiwan. Columns 7 to 21 are all filled, the status is Priced, and Remarks keeps only the notes above. These rule values are test data, not facts about DigiKey or Murata.

## Duplicate-manufacturer case

Use a stub that returns HTTP 404 with a detail text beginning "Duplicate Products found" for the MPN BAT54CW,115, and succeeds when manufacturerId is supplied.

## Real client file cases (tests/samples/LT2026100009.xlsx)

- Row with MPN `SMT;SMD0402;100n;+10%;-10%;50V;X7R` and MFG Common: no API call, status NeedsReview, Remarks "Generic part: no real MPN".
- Row with MPN `BLM15GG221SZ1x`: wildcard detected before uppercasing; stub candidate BLM15GG221SZ1D is accepted as PackagingVariant.
- Row with the five-MPN cell starting `BAT54CW,115 / BAT54CWT1G (at) ...`: only BAT54CW,115 is looked up; the others appear in Remarks as alternates.
- Output file reloaded after writing still has the Yes/No validations on columns S and T, the country validation on column U, and the sheet "Sheet1" with 257 countries.

## Rules files the agent creates in Step 1 (seed content)

The agent creates the `rules/` folder itself. Nobody supplies these files. Content:

- `rules/countries.txt`: one country name per line, taken by code from Sheet1 of `tests/samples/LT2026100009.xlsx` (rows 2 to 258, header "Countries" excluded, trimmed, original order). 257 names, including Malawi and the 7 names that start with "Obsolete". Never type the names by hand. A test compares the file with Sheet1.
- `rules/suffix-rules.json`: `[{"manufacturer":"Murata","suffix":"D","description":"Murata packaging suffix D"}]`
- `rules/mfg-aliases.json`: `[{"canonical":"Murata","aliases":["Murata","Murata Electronics","Murata Manufacturing"]}]`
- `rules/supplier-terms.json`: `[]` (purchasing fills it later; an empty file means columns 15, 16 and 17 stay missing and the row is Incomplete)
- `rules/coo-rules.json`: `[]` (same idea for column 21 when the API gives no country)

Tests that need cancellation, reschedule, DB or PB or COO values use their own test-only rule entries (see "Same row after test-only rule entries are added") and never change the real files in `rules/`.
