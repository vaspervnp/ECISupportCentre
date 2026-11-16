# ECI Support Centre — Milestones

Android app (phone and tablet) in C# on .NET 10. It tracks every European Citizens' Initiative (ECI) published on <https://citizens-initiative.europa.eu/index_en>.

## Goals

1. Keep a list of **all** initiatives (ongoing, registered, submitted, answered, failed).
2. Keep a **local database** on the device in sync with the official register.
3. Show **ECI news**.
4. Send the user to the **official signing page** of an initiative.
5. **Notify** the user when an initiative's collection period ends and whether it **succeeded or failed**.
6. Ship the UI in **all 24 official EU languages**, with English as the default.

## Data sources (checked 2026-11-16)

| Purpose | Source | Notes |
|---|---|---|
| Initiative list | `https://register.eci.ec.europa.eu/core/api/register/search/{STATUS}/{LANG}/{OFFSET}/{SIZE}` | JSON. `ALL` and `ONGOING` were checked. Fields: `id`, `pubRegNum`, `year`, `number`, `status`, `languageCode`, `title`, `totalSupporters`, `supportLink`, `latestUpdateDate` (`dd/MM/yyyy HH:mm`), `lastCall`, `logo`. The envelope also holds register-wide totals. |
| Status values seen | — | `ONGOING`, `REGISTERED`, `SUBMITTED`, `ANSWERED`, `INSUFFICIENT_SUPPORT`, `INSUFFICIENT_SUPPORT_AFTER_VERIFICATION` (the list may be incomplete) |
| Signing | `supportLink` of each entry (e.g. `https://eci.ec.europa.eu/069/public/?lg=en`) | Opened in the browser or a Custom Tab. The app never collects signatures itself. |
| News | `https://citizens-initiative.europa.eu/node/418/rss_en` | RSS 2.0 |
| Website (per language) | `https://citizens-initiative.europa.eu/index_{lang}` | 24 language suffixes |

Not checked yet: the API with languages other than `EN`, paging past the first page, a per-initiative detail endpoint (needed for collection start and end dates), and whether there are RSS feeds in other languages. Milestone 1 covers these.

## Technology

- **.NET 10 MAUI**, target `net10.0-android` (minimum API 24). Layouts adapt to phones and tablets.
- **MVVM** with `CommunityToolkit.Mvvm`, and `CommunityToolkit.Maui` for UI helpers.
- **SQLite** through `sqlite-net-pcl` for the local database.
- `HttpClient` with `System.Text.Json` source generators, and **Polly** for retries.
- **AndroidX WorkManager** for periodic background sync.
- **Local notifications** through `NotificationCompat` with notification channels (Android 13+ asks for `POST_NOTIFICATIONS`).
- **Localization** with `.resx` resources for 24 languages.
- **xUnit** tests for the core library.
- **GitHub Actions** CI that builds and tests and produces a signed APK/AAB.

## Solution layout (planned)

```
src/
  EciSupportCentre.Core/      # models, API client, RSS parser, sync engine, status logic (net10.0)
  EciSupportCentre.Data/      # SQLite repositories, migrations
  EciSupportCentre.App/       # MAUI app (net10.0-android): views, view models, platform services
tests/
  EciSupportCentre.Core.Tests/
```

---

## M0 — Project setup ✅
- [x] Solution (`EciSupportCentre.slnx`) with the Core, Data and App projects and a test project
- [x] `.editorconfig`, `Directory.Build.props`, `global.json`, nullable references on, warnings treated as errors
- [x] App icon, splash screen, package id `io.github.vaspervnp.ecisupportcentre` (not an `eu.*` id, since the app is not official)
- [x] CI workflow: restore, build, test, build an installable Android test APK

**Done when:** an empty app runs on an emulator and CI is green.

## M1 — Check the data sources and build the API client
- [ ] Check the register API with every language code, with paging, and with every status filter
- [ ] Find the per-initiative detail endpoint (registration date, collection start and end, objectives, organisers, answer of the Commission)
- [ ] Typed models and a `RegisterApiClient` with paging, cancellation, retries and timeouts
- [ ] `NewsFeedClient` (RSS) and a check for feeds in other languages
- [ ] Map the API statuses to an app status: `Collecting`, `Upcoming`, `Verification`, `Submitted`, `Answered` (success), `Failed`, `Withdrawn`
- [ ] Tests against recorded JSON and RSS samples

**Done when:** the Core library downloads and parses all initiatives and the news, and the tests pass offline.

## M2 — Local database and sync engine
- [ ] SQLite schema: `Initiative`, `InitiativeTranslation` (per language), `StatusHistory`, `NewsItem`, `UserFollow`, `SyncState`
- [ ] Repositories and schema migrations
- [ ] Sync engine: full sync on first run, then incremental sync based on `latestUpdateDate`
- [ ] Detect changes (new initiative, status change, signature milestone, `lastCall`, collection ended) and record them in `StatusHistory`
- [ ] The app works offline: it always reads from the local database

**Done when:** a sync fills the database, and a second sync records only the real changes.

## M3 — Initiative list and details
- [ ] Home tab with register totals (registered, ongoing, successful, answered)
- [ ] List with search, status filters and sorting (newest, most supporters, ending soon)
- [ ] Detail page: title, logo, status, supporters, dates, objectives, status history and a link to the official page
- [ ] Pull to refresh
- [ ] Tablet layout with a list and the details side by side

**Done when:** the user can browse, filter and open every initiative offline.

## M4 — Signing
- [ ] **Sign** button on initiatives that are collecting, which opens `supportLink` in a Chrome Custom Tab in the user's language (`?lg=xx`)
- [ ] Hide or disable the button when collection is closed
- [ ] Optional "I signed this" mark, stored only on the device

**Done when:** every collecting initiative opens its official collection page.

## M5 — News
- [ ] News tab from the RSS feed, cached in SQLite
- [ ] Clean the HTML in descriptions, show images from `enclosure`
- [ ] Open the full article in a Custom Tab
- [ ] News in the user's language where a feed exists, with English as the fallback

**Done when:** the news shows offline and refreshes during sync.

## M6 — Background sync and notifications
- [ ] Periodic WorkManager job (default every 12 hours, adjustable, only on Wi-Fi if the user chooses)
- [ ] Notification channels: *New initiatives*, *Collection ending*, *Results*, *News*
- [ ] Notifications for: a new initiative registered, collection ending soon (7 days and 1 day before), collection ended, success (1 million signatures and thresholds met, then submitted or answered), failure (insufficient support), and the Commission's answer
- [ ] **Follow** an initiative to get notifications for it only, or follow all
- [ ] Tapping a notification opens the initiative's details

**Done when:** a simulated status change in the database triggers the right notification on a device.

## M7 — Localization in the 24 EU languages
- [ ] Languages: bg, cs, da, de, el, **en (default)**, es, et, fi, fr, ga, hr, hu, it, lt, lv, mt, nl, pl, pt, ro, sk, sl, sv
- [ ] `.resx` for every UI text; a language picker in Settings, defaulting to the device language when it is supported, else English
- [ ] Initiative data in the chosen language through the API language code, falling back to English or the original language
- [ ] Dates and numbers formatted for each locale
- [ ] Review of the translations (machine translation first, then a review by native speakers)

**Done when:** the whole UI and the data switch language at run time without a restart.

## M8 — Settings, accessibility and polish
- [ ] Settings: language, sync frequency, Wi-Fi only, notification types, light, dark or system theme
- [ ] Accessibility: TalkBack labels, dynamic font size, contrast
- [ ] Empty, error and offline states
- [ ] An About page with data sources, a disclaimer (not an official EU app) and the licence

**Done when:** the accessibility scanner reports nothing serious and all settings take effect.

## M9 — Quality and release
- [ ] Unit tests for Core and Data, UI smoke tests on an emulator
- [ ] Performance check: first sync under 10 seconds on 4G, smooth scrolling of the list
- [ ] Release build with trimming and AOT where it is stable, and a signed AAB
- [ ] Privacy policy (no personal data, no tracking)
- [ ] Google Play listing (store texts in the 24 languages), internal testing, then production

**Done when:** version 1.0 is on Google Play.

## Later (after 1.0)
- Home-screen widget with ongoing initiatives
- Supporters per member state compared with each country's threshold
- Shared links and deep links
- iOS and Windows builds from the same MAUI code
