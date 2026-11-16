# ECI Support Centre

An Android app (phone and tablet) for following the **European Citizens' Initiatives** published on [citizens-initiative.europa.eu](https://citizens-initiative.europa.eu/index_en).

> Status: **M5 done**: Home, Initiatives and News tabs, initiative details, tablet layout, and signing on the official collection page. Background sync and notifications follow in M6. See [milestones.md](milestones.md) for the roadmap.

## Project structure

```
EciSupportCentre.slnx
src/
  EciSupportCentre.Core/        models, API clients, sync engine (net10.0)
  EciSupportCentre.Data/        SQLite database and migrations (net10.0)
  EciSupportCentre.App/         .NET MAUI Android app (net10.0-android)
tests/
  EciSupportCentre.Core.Tests/  API client tests on recorded samples
  EciSupportCentre.Data.Tests/  database and sync tests
.github/workflows/ci.yml        tests and an installable APK on every push and pull request
```

## Features (planned)

- **All initiatives**: ongoing, registered, submitted, answered and failed, with search, filters and sorting.
- **Works offline**: a local SQLite database is kept in sync with the official register in the background.
- **News** from the official ECI news feed.
- **Sign**: opens the official online collection page of the initiative. The app never collects signatures or personal data.
- **Notifications** for new initiatives, collection periods ending, success or failure, and the European Commission's answer.
- **24 EU languages**, with English as the default.

## Technology

| Area | Choice |
|---|---|
| Platform | .NET 10 MAUI (`net10.0-android`), C# |
| Architecture | MVVM (`CommunityToolkit.Mvvm`), .NET MAUI Shell |
| Local database | SQLite (`Microsoft.Data.Sqlite`) |
| Network | `HttpClient`, `System.Text.Json` (source-generated), built-in retry handler |
| Background work | AndroidX WorkManager |
| Localization | `.resx` resources (bg, cs, da, de, el, en, es, et, fi, fr, ga, hr, hu, it, lt, lv, mt, nl, pl, pt, ro, sk, sl, sv) |
| Tests | xUnit |

## Data sources

- Register API (JSON): `https://register.eci.ec.europa.eu/core/api/register/`
  - `search/{ALL|ONGOING|ANSWERED}/{LANG}/{OFFSET}/{SIZE}`: list of initiatives
  - `details/{year}/{number}`: one initiative in all languages, with dates, signatures per country and the Commission's answer
  - `statistics`, `logo/{id}`
- News (RSS, per language): `https://citizens-initiative.europa.eu/node/418/rss_{lang}`
- Signing: the `supportLink` field of each initiative, which points to `https://eci.ec.europa.eu/...`

See [milestones.md](milestones.md#data-sources-verified-in-m1-2026-11-16) for the details and quirks of each endpoint.

All data belongs to the European Commission. The app only reads public data.

## Requirements

- .NET 10 SDK with the MAUI workload: `dotnet workload install maui-android`
- Android SDK (API 24 or later), an emulator or a device
- Visual Studio 2026, VS Code with C# Dev Kit, or JetBrains Rider

## Build and run

```bash
dotnet build src/EciSupportCentre.App -f net10.0-android
```

Run on a connected device or a running emulator:

```bash
dotnet build src/EciSupportCentre.App -f net10.0-android -t:Run
```

Run the tests (offline, using recorded samples and temporary SQLite files):

```bash
dotnet test tests/EciSupportCentre.Core.Tests
```

```bash
dotnet test tests/EciSupportCentre.Data.Tests
```

Run the live tests against the real ECI services (bash):

```bash
ECI_LIVE_TESTS=1 dotnet test tests/EciSupportCentre.Core.Tests --filter Category=Live
```

## Disclaimer

This is **not** an official app of the European Union or the European Commission. It is an independent tool that uses publicly available data.

## Licence

To be decided.
