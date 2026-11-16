# ECI Support Centre

An Android app (phone and tablet) for following the **European Citizens' Initiatives** published on [citizens-initiative.europa.eu](https://citizens-initiative.europa.eu/index_en).

> Status: **M0 done**: project skeleton and CI. See [milestones.md](milestones.md) for the roadmap.

## Project structure

```
EciSupportCentre.slnx
src/
  EciSupportCentre.Core/        models, API clients, sync logic (net10.0)
  EciSupportCentre.Data/        SQLite storage (net10.0)
  EciSupportCentre.App/         .NET MAUI Android app (net10.0-android)
tests/
  EciSupportCentre.Core.Tests/  xUnit tests
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
| Architecture | MVVM (`CommunityToolkit.Mvvm`) |
| Local database | SQLite (`sqlite-net-pcl`) |
| Network | `HttpClient`, `System.Text.Json`, Polly |
| Background work | AndroidX WorkManager |
| Localization | `.resx` resources (bg, cs, da, de, el, en, es, et, fi, fr, ga, hr, hu, it, lt, lv, mt, nl, pl, pt, ro, sk, sl, sv) |
| Tests | xUnit |

## Data sources

- Register API (JSON): `https://register.eci.ec.europa.eu/core/api/register/search/{STATUS}/{LANG}/{OFFSET}/{SIZE}`
- News (RSS): `https://citizens-initiative.europa.eu/node/418/rss_en`
- Signing: the `supportLink` field of each initiative, which points to `https://eci.ec.europa.eu/...`

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

Run the tests:

```bash
dotnet test
```

## Disclaimer

This is **not** an official app of the European Union or the European Commission. It is an independent tool that uses publicly available data.

## Licence

To be decided.
