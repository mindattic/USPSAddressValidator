# USPSAddressValidator

Windows Forms tool that loads a CSV of US mailing addresses, validates every row against the USPS address verification API with up to 100 concurrent requests, and saves the standardized results as CSV.

![C#](https://img.shields.io/badge/C%23-WinForms-512BD4) ![.NET](https://img.shields.io/badge/.NET-6.0--windows-512BD4) ![Platform](https://img.shields.io/badge/platform-Windows-0078D6) ![Status](https://img.shields.io/badge/status-prototype-orange)

```text
 +------------------------ USPS Address Validator ------------------------+
 |  CSV File      [ test2.csv                               ] [Open...]   |
 |  Output        [ results.csv ]                                         |
 |  Base URL      [ USPS Verify endpoint                    ]             |
 |  Thread Count [100]  Refresh Rate [10000]  Task Delay [1]  [x] Preview |
 |                                                         [ Generate ]   |
 |  +--------------------- CSV preview grid ---------------------------+  |
 |  | Col0 (Address1) | Col1 (Address2) | Col2 (City) | Col3 | ...     |  |
 |  +------------------------------------------------------------------+  |
 |  Rows: 99,999                                                          |
 +------------------------------------------------------------------------+
        |
        v  a centered console window streams progress and the final count
```

A private prototype from February 2023. There is no hosted build; run it from source.

## Why

- Standardize a whole mailing list against USPS data in one click instead of one address at a time.
- Get ZIP+4 and corrected city and street lines back in the same six-column layout you loaded.
- Tune concurrency, delay and progress frequency from the form to fit the API's limits without recompiling.
- Preview the input in a grid before you spend any API calls on it.

## Features

- Open any header-less CSV of addresses; the grid previews it and the status bar shows the row count.
- Each row maps to Address1 (apartment or suite), Address2 (street), City, State, Zip5, Zip4.
- Each row becomes an `AddressValidateRequest` XML document (Revision 1), URL-encoded as ISO-8859-1 and sent as a GET query string.
- A `SemaphoreSlim` caps requests in flight at Thread Count, and each worker waits Task Delay milliseconds after a request.
- Responses are deserialized into a typed `AddressValidateResponse` model (address lines, city abbreviation, ZIP5, ZIP4, delivery point, carrier route, DPV confirmation and footnotes, business and vacant flags).
- The validated Address1, Address2, City, State, Zip5 and Zip4 are written to the results CSV, which then opens in Notepad.
- HTTP 404, HTTP 400 and exceptions are counted separately.
- Two sample inputs ship with the repo: `test1.csv` (99 rows) and `test2.csv` (99,999 rows) for load runs.

## Quick start

Prerequisites: Windows, the .NET 6 SDK (or Visual Studio 2022), and your own USPS Web Tools user ID.

```powershell
git clone https://github.com/mindattic/USPSAddressValidator.git
cd USPSAddressValidator
```

1. In `USPSAddressValidator\frmMain.cs`, set the API user ID constant and the results file path to values for your machine and account (see Configuration).
2. In `USPSAddressValidator\frmMain.Designer.cs`, set or clear the default CSV path, which the form opens on start.
3. Run it:

```powershell
dotnet run --project USPSAddressValidator\USPSAddressValidator.csproj
```

4. Click Open... and choose `test1.csv`, check the options, and click Generate.
5. Watch the console window; when it prints "Done." the results CSV opens in Notepad.

## Configuration

Form fields (defaults from `frmMain.Designer.cs`):

| Field | Default | Meaning |
| --- | --- | --- |
| CSV File | developer path | Input file, opened automatically on start |
| Output | `results.csv` | Shown on the form; the save path itself is set in `frmMain.cs` |
| Base URL | USPS Verify endpoint | Request prefix; the encoded XML is appended |
| Thread Count | 100 | Maximum concurrent requests (falls back to 3 if not a number) |
| Refresh Rate | 10000 | Print a progress line every N completed requests; 0 turns progress and the counter off |
| Task Delay | 1 | Milliseconds each worker waits after a request |
| Preview CSV | on | Bind the loaded table to the grid |

Source constants in `frmMain.cs`: the USPS Web Tools user ID that goes into every request, and the `FileUtility` path the results are written to. Supply your own user ID; never commit someone else's.

## How it works

```text
Open CSV --CSVUtility--> DataTable --GenerateRequests--> List<AddressValidateRequest>
                                                                  |
Generate -> Setup (read form fields, SemaphoreSlim, reset timers) v
          -> Process: per request, Task.Run
                ComposeXML -> UrlEncode(ISO-8859-1) -> GET Base URL + xml
                non-2xx -> 404/400 counters
                2xx -> XmlSerializer -> AddressValidateResponse -> CSV line
                every Refresh Rate -> progress line
             Task.WhenAll -> SaveResults (open in notepad.exe) -> PrintResults
```

## Project layout

| Path | Purpose |
| --- | --- |
| `USPSAddressValidator/USPSAddressValidator.sln` | Visual Studio solution |
| `USPSAddressValidator/USPSAddressValidator.csproj` | WinExe, `net6.0-windows`, Windows Forms |
| `USPSAddressValidator/frmMain.cs` | Form events, request pipeline, throttling, saving and reporting |
| `USPSAddressValidator/Models/` | XML request and response models, concurrent logger |
| `USPSAddressValidator/Utilities/CSVUtility.cs` | CSV to `DataTable` via `TextFieldParser` |
| `USPSAddressValidator/Utilities/EventUtility.cs` | Thread-safe success, 404, 400 and exception counters |
| `USPSAddressValidator/Utilities/ConsoleUtility.cs` | Centers the allocated console window with Win32 calls |
| `USPSAddressValidator/Utilities/FileUtility.cs` | Overwrite a file and open it in a text editor |
| `USPSAddressValidator/Utilities/PrintUtility.cs` | Console write helpers |
| `USPSAddressValidator/Extensions/` | `string.Repeat` and `List.ChunkBy` helpers |
| `USPSAddressValidator/test1.csv`, `USPSAddressValidator/test2.csv` | Sample inputs |
| `USPSAddressValidator/results.csv` | Output from an earlier run |
| `USPSAddressValidator/commit.cmd` | Stage, commit with a timestamp message, and push |

## Limitations

- The user ID and the results path are source constants, and the default CSV path points at the original developer machine; all three need editing before a run elsewhere.
- The Output field on the form is not used yet.
- Results from parallel workers are appended to one shared `StringBuilder` without a lock, so very large runs can lose or interleave lines.
- Address values are inserted into the XML without escaping, so a value containing `&` or `<` produces an invalid request.
- The 404, 400 and exception counters are collected but not printed.
- It targets the legacy USPS Web Tools XML Verify API. Check that the endpoint is still available to your account before relying on it.
- No tests.

## Documentation

There are no separate docs; this README and the source are the reference. [APIConsole](https://github.com/mindattic/APIConsole) runs the same pipeline as a console app, and [ApiCaller](https://github.com/mindattic/ApiCaller) is the generic JSON load tester with the same throttled request loop.

## License

No license file. All rights reserved.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [APIConsole](https://github.com/mindattic/APIConsole), [ApiCaller](https://github.com/mindattic/ApiCaller).
