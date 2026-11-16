# Recorded samples

These are real responses of the ECI services, recorded on 2026-11-16 and trimmed for the tests.

| File | Source |
|---|---|
| `search_all_en.json`, `search_all_el.json` | `https://register.eci.ec.europa.eu/core/api/register/search/ALL/{EN,EL}/0/200`, with fewer entries (`recordsFound` changed to match) |
| `statistics.json` | `.../register/statistics` |
| `details_<status>.json` | `.../register/details/{year}/{number}`, one per status |
| `news_en.xml`, `news_el.xml` | `https://citizens-initiative.europa.eu/node/418/rss_{en,el}`, first 3 items |

How the details were trimmed:

- Only 3 language versions are kept (the original, EN, EL or DE). The live API returns all 24.
- Long HTML texts are shortened.
- Organiser and sponsor names are replaced with `Member N` / `Sponsor N`, and only 3 members are kept.
- Per-country signature lists keep 5 countries.
