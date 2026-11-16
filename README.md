# ECI Support Centre

An Android app (phone and tablet) for following the **European Citizens' Initiatives** published on [citizens-initiative.europa.eu](https://citizens-initiative.europa.eu/index_en).

> Status: **M8 done**: Home, Initiatives, News and Settings tabs, initiative details, tablet layout, signing on the official collection page, background sync and notifications, settings (language, theme, sync frequency, Wi-Fi only), offline and empty states, TalkBack support and an About page, all in the 24 EU languages (machine translations, not yet reviewed by native speakers). Next: M9, quality and release. See [milestones.md](milestones.md) for the roadmap.

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

## Features

- **All initiatives**: ongoing, registered, submitted, answered and failed, with search, filters and sorting.
- **Works offline**: a local SQLite database is kept in sync with the official register in the background.
- **News** from the official ECI news feed.
- **Sign**: opens the official online collection page of the initiative. The app never collects signatures or personal data.
- **Share** an initiative's title and official link with any app.
- **Notifications** for new initiatives, collection periods ending, success or failure, and the European Commission's answer.
- **24 EU languages**, with English as the default.
- **Settings**: language, light or dark theme, how often to sync, Wi-Fi only.
- **Accessible**: TalkBack labels and layouts that follow the system font size.

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

Try the background sync without waiting for its 12-hour period (Debug builds only): the app queues one `SyncWorker` run 15 seconds later; press Home to put the app in the background meanwhile.

```bash
adb shell am start -n io.github.vaspervnp.ecisupportcentre/crc641f9ab5b7e96c61fb.MainActivity --ez eci_debug_sync true
```

Run the live tests against the real ECI services (bash):

```bash
ECI_LIVE_TESTS=1 dotnet test tests/EciSupportCentre.Core.Tests --filter Category=Live
```

Smoke test of an APK on a connected emulator or device (bash; installs from scratch, waits for the first sync, opens every tab and an initiative, and fails on a crash, an ANR or an empty list):

```bash
scripts/smoke-test.sh path/to/io.github.vaspervnp.ecisupportcentre-Signed.apk
```

## Release

Version numbers are `ApplicationDisplayVersion` and `ApplicationVersion` in the app's csproj. Pushing a `v*` tag (or running the **Release** workflow by hand) runs the tests and builds a signed AAB for Google Play and a signed APK, uploaded as workflow artifacts. The workflow needs three repository secrets:

- `ANDROID_KEYSTORE_BASE64`: the upload keystore, base64-encoded
- `ANDROID_KEYSTORE_PASSWORD`: its password (used for the store and the key)
- `ANDROID_KEY_ALIAS`: the key alias

A local signed build reads the password from the `ECI_KEYSTORE_PASSWORD` environment variable; never commit the keystore.

```bash
dotnet publish src/EciSupportCentre.App -f net10.0-android -c Release -p:EciKeystore=/path/to/upload.jks -p:EciKeyAlias=upload
```

The Google Play texts in the 24 languages are in `fastlane/metadata/android/<locale>/` and the privacy policy is [docs/privacy-policy.md](docs/privacy-policy.md).

## Disclaimer

This is **not** an official app of the European Union or the European Commission. It is an independent tool that uses publicly available data.

## Licence

[MIT](LICENSE). The initiative data and news are © European Union and come from the Commission's public websites.
