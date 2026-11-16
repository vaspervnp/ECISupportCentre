# ECI Support Centre — Milestones

Android app (phone and tablet) in C# on .NET 10. It tracks every European Citizens' Initiative (ECI) published on <https://citizens-initiative.europa.eu/index_en>.

## Goals

1. Keep a list of **all** initiatives (ongoing, registered, submitted, answered, failed).
2. Keep a **local database** on the device in sync with the official register.
3. Show **ECI news**.
4. Send the user to the **official signing page** of an initiative.
5. **Notify** the user when an initiative's collection period ends and whether it **succeeded or failed**.
6. Ship the UI in **all 24 official EU languages**, with English as the default.

## Data sources (verified in M1, 2026-11-16)

Register API base: `https://register.eci.ec.europa.eu/core/api/register/` (public, JSON, no key).

| Endpoint | Use | Notes |
|---|---|---|
| `search/{FILTER}/{LANG}/{OFFSET}/{SIZE}` | Initiative list | `FILTER` is `ALL`, `ONGOING` or `ANSWERED` (anything else is a 400). `LANG` is any of the 24 EU codes in upper case; titles and `supportLink` (`?lg=xx`) follow it. `OFFSET` is an **index**, not a page number. `ALL` and `ANSWERED` apply offset and size; `ONGOING` ignores them and returns every ongoing initiative. The envelope holds register-wide totals. |
| `details/{year}/{number}` | One initiative | All 24 language versions (title, objectives, annex, treaties, support link), dates (`registrationDate`, `startCollectionDate`, `deadline`, `earlyClosureDate`), `progress` steps with dates, signatures per country (`sosReport`, then `submission` after verification), Commission `answer`, `funding`, organisers. About 230 KB, so the sync should fetch it only when `latestUpdateDate` changes. |
| `statistics` | Home totals | `registered`, `ongoing`, `answered`, `rejected`, `unsuccessful`, `withdrawn`, `startingSoon`, ... |
| `logo/{id}` | Logo image | Served as `application/octet-stream` |
| `document/{id}` | PDFs (e.g. draft legal act) | Not used yet |

Statuses: `REGISTERED`, `ONGOING`, `CLOSED`, `VERIFICATION`, `SUBMITTED`, `ANSWERED`, `INSUFFICIENT_SUPPORT`, `INSUFFICIENT_SUPPORT_AFTER_VERIFICATION`, `WITHDRAWN`.
Dates are `dd/MM/yyyy` or `dd/MM/yyyy HH:mm` (Brussels time); a missing date is often an empty string.

Other sources:

| Purpose | Source | Notes |
|---|---|---|
| Signing | `supportLink` of each initiative (e.g. `https://eci.ec.europa.eu/069/public/?lg=el`) | Opened in the browser or a Custom Tab. The app never collects signatures itself. Initiatives that collected only on paper have no link. |
| News | `https://citizens-initiative.europa.eu/node/418/rss_{lang}` | RSS 2.0 in every EU language; articles that are not translated appear in English. |
| Website | `https://citizens-initiative.europa.eu/index_{lang}` | |

The servers rate-limit with **429** and a fractional `Retry-After` (e.g. `0.809`), so the client must retry politely.

## Technology

- **.NET 10 MAUI**, target `net10.0-android` (minimum API 24). Layouts adapt to phones and tablets.
- **MVVM** with `CommunityToolkit.Mvvm`, and `CommunityToolkit.Maui` for UI helpers.
- **SQLite** through `Microsoft.Data.Sqlite` with hand-written SQL (no reflection, so it is safe with trimming and AOT).
- `HttpClient` with `System.Text.Json` source generators, and a small built-in retry handler (429 / `Retry-After`, 5xx, network errors) instead of Polly.
- **AndroidX WorkManager** for periodic background sync.
- **Local notifications** through `NotificationCompat` with notification channels (Android 13+ asks for `POST_NOTIFICATIONS`).
- **Localization** with `.resx` resources for 24 languages.
- **xUnit** tests for the core library.
- **GitHub Actions** CI that builds and tests and produces a signed APK/AAB.

## Solution layout

```
src/
  EciSupportCentre.Core/      # models, API clients, RSS parser, storage contracts, sync engine, status logic (net10.0)
  EciSupportCentre.Data/      # SQLite database, schema migrations (net10.0)
  EciSupportCentre.App/       # MAUI app (net10.0-android): views, view models, platform services
tests/
  EciSupportCentre.Core.Tests/  # API clients against recorded samples
  EciSupportCentre.Data.Tests/  # database and sync scenarios on real SQLite files
```

---

## M0 — Project setup ✅
- [x] Solution (`EciSupportCentre.slnx`) with the Core, Data and App projects and a test project
- [x] `.editorconfig`, `Directory.Build.props`, `global.json`, nullable references on, warnings treated as errors
- [x] App icon, splash screen, package id `io.github.vaspervnp.ecisupportcentre` (not an `eu.*` id, since the app is not official)
- [x] CI workflow: restore, build, test, build an installable Android test APK

**Done when:** an empty app runs on an emulator and CI is green.

## M1 — Check the data sources and build the API client ✅
- [x] Check the register API with every language code, with paging, and with every status filter
- [x] Find the per-initiative detail endpoint (`details/{year}/{number}`), plus `statistics` and `logo/{id}`
- [x] Typed models and a `RegisterApiClient` with paging, cancellation, retries and timeouts
- [x] `NewsFeedClient` (RSS) with per-language feeds and an English fallback
- [x] Map the API statuses to a phase (`Upcoming`, `Collecting`, `Verification`, `Submitted`, `Answered`, `Failed`, `Withdrawn`) and an outcome (`Pending`, `Successful`, `Failed`, `Withdrawn`)
- [x] Tests against recorded JSON and RSS samples (66 offline tests), plus live tests behind `ECI_LIVE_TESTS=1`

**Done when:** the Core library downloads and parses all initiatives and the news, and the tests pass offline.

## M2 — Local database and sync engine ✅
- [x] SQLite schema: `initiative`, `initiative_text` (title and signing link per language), `initiative_details` (full record as JSON), `initiative_change`, `news_item`, `follow`, `sync_state`
- [x] `EciDatabase` with schema migrations (`PRAGMA user_version`); refuses a database written by a newer app version
- [x] Sync engine: the list on every sync; details only for active initiatives and for any whose `latestUpdateDate` or status changed (at most 30 per sync); details of finished initiatives on demand
- [x] Detect changes (new initiative, status change, signature milestone 100k/250k/500k/750k/1M, `lastCall`) and keep them until notified. The first sync records none. Collection ending soon is time-based and belongs to M6.
- [x] The app works offline: it always reads from the local database. Search ignores case and accents (e.g. Greek tonos).
- [x] Checked on the emulator: first sync 136 initiatives, 17 with details, 30 news in 48 s (Debug build); second sync 0 changes, 0 details, 12 s

**Done when:** a sync fills the database, and a second sync records only the real changes.

## M3 — Initiative list and details ✅
- [x] Shell with two tabs (Home, Initiatives); MVVM with `CommunityToolkit.Mvvm`; every UI text in `AppResources.resx` (bound through a `Translate` markup extension, so M7 only adds translations and the language can change at run time)
- [x] Home tab with totals (registered, collecting, successful, answered, not enough support), the five collecting initiatives that end soonest, and a background refresh when the data is older than six hours
- [x] List with accent-insensitive search, filter chips (all, collecting, starting soon, successful, not enough support, withdrawn, followed) and sorting (newest, most supporters, ending soon, recently updated)
- [x] Detail page: logo, title, number, phase, supporters, dates, objectives (HTML), timeline, signatures per country (names in the app language), Commission answer and documents, more information, treaty provisions, Follow, and a link to the official page. Missing or outdated details are fetched when the page opens.
- [x] Pull to refresh on Home and on the list
- [x] Tablet layout (from 840 dp): list and details side by side, selected card highlighted
- [x] Checked on the emulator in portrait (phone) and landscape (915 dp, tablet layout)

**Done when:** the user can browse, filter and open every initiative offline.

## M4 — Signing ✅
- [x] Logos are loaded from `register/logo/{id}` and cached by the image loader; checked that they still show with the network off
- [x] **Sign** button on initiatives that are collecting, which opens `supportLink` in a Chrome Custom Tab in the user's language (`?lg=xx`). Only links to `https://eci.ec.europa.eu` are opened.
- [x] The button is hidden when collection is closed; the panel shows the days left and the last day
- [x] Optional "I signed this" mark (schema v2, table `signed`), stored only on the device and shown as a badge on the card
- [x] Checked on the emulator: the button opens the official collection page in Chrome

**Done when:** every collecting initiative opens its official collection page.

## M5 — News ✅
- [x] News tab from the RSS feed, cached in SQLite and refreshed by every sync (pull to refresh, and in the background when the data is older than six hours)
- [x] Clean the HTML in descriptions into a short plain-text summary; show images from `enclosure` (kept exactly as the feed gives them: some file names really contain `%20`)
- [x] Open the full article in a Custom Tab (https only); opened articles lose their "New" badge (unread and at most 14 days old)
- [x] News in the user's language where a feed exists, with English as the fallback (when the feed is missing, or nothing is stored in that language yet)
- [x] Two columns on tablets; checked on the emulator in portrait, landscape and with the network off

**Done when:** the news shows offline and refreshes during sync.

## M6 — Background sync and notifications ✅
- [x] Periodic WorkManager job (`SyncWorker`, default every 12 hours, only with a network; Wi-Fi only and the interval are stored settings, their screen comes in M8). WorkManager 2.10.3, the last line that matches MAUI's AndroidX Lifecycle 2.9.
- [x] Notification channels: *New initiatives*, *Collection ending*, *Results*, *News* (news are silent); channel names follow the app language
- [x] Notifications for: a new initiative registered, collection started, collection ending soon (7 days and 1 day before, each once), collection ended, signature milestones (100k to 1 million), success (submitted), failure (insufficient support), withdrawal, the Commission's answer, and new articles (one notification for all)
- [x] Rules in Core (`NotificationPlanner`, tested): after missed syncs only the latest status worth telling and the highest milestone are shown; a result replaces the milestone reached on the way; later notifications for the same initiative replace earlier ones
- [x] **Follow** an initiative to get notifications for it only, or follow all (switch on Home, on by default). New initiatives and news are always announced.
- [x] Tapping a notification opens the initiative's details (or the News tab), also when the app was closed
- [x] Android 13+ permission asked once, after the first data arrived; handled events are recorded even without permission, so allowing it later brings no flood of old events
- [x] Checked on the emulator: changes simulated in the database (success, failure, collection started, new initiative, 7 days left, 100k signatures, new article) all notified from the background job; taps open the right initiative

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
- [ ] Performance check: first sync under 10 seconds on 4G (48 s in a Debug build on the emulator in M2; fetch details in parallel or after the list is shown), smooth scrolling of the list
- [ ] Release build with trimming and AOT where it is stable, and a signed AAB
- [ ] Privacy policy (no personal data, no tracking)
- [ ] Google Play listing (store texts in the 24 languages), internal testing, then production

**Done when:** version 1.0 is on Google Play.

## Later (after 1.0)
- Home-screen widget with ongoing initiatives
- Supporters per member state compared with each country's threshold
- Shared links and deep links
- iOS and Windows builds from the same MAUI code
