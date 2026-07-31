# Site requests and parsing

## How the library works

HDRezka.NET does not launch a browser, execute website JavaScript, or inspect a
browser DOM. It sends ordinary requests through `HttpClient`

Responses use one of two paths:

1. JSON and compact HTML fragments are read from direct website endpoints
2. Complete HTML pages are parsed in memory with AngleSharp when no smaller
   endpoint provides the required data

AngleSharp builds an in-memory document from the response body. This is still
an ordinary HTTP client workflow and does not require Selenium, Playwright, or
a browser process

## Direct endpoints currently used

| Feature | Request | Response |
| --- | --- | --- |
| Login | `POST /ajax/login/` | JSON and authentication cookies |
| Session verification | `GET /favorites/` | HTML |
| Logout | `GET /logout/` | HTML and expired cookies |
| Fast search | `POST /engine/ajax/search.php` | Compact HTML fragment |
| Seasons and streams | `POST /ajax/get_cdn_series/` | JSON |
| Comments | `GET /ajax/get_comments/` | JSON containing HTML fragments |
| Playback progress | `POST /ajax/send_save/` | JSON |
| Continue watched state | `POST /engine/ajax/cdn_saves_view.php` | JSON |
| Continue removal | `POST /engine/ajax/cdn_saves_remove.php` | JSON |
| Bookmark changes | `POST /ajax/favorites/` | JSON |

The player endpoint name contains `series`, but the website itself uses it for
both series and films with different `action` values

The current favorites and profile pages do not expose the account-tier token.
Authentication can therefore be verified from cookies while
`AuthenticationState.AccountTier` remains `Unknown`. Media pages still expose
the token and provide an accurate `Media.AccountTier`

## Complete pages currently parsed

- media pages
- account settings
- continue-watching history
- bookmark folders
- catalog sections and dedicated directories
- collections
- full search results

Extended media metadata, recommendations, and series schedules are parsed from
the already downloaded media page and do not create extra requests

## Discovered read-only sources not yet exposed

These sources can be added when their data is needed:

- `/ajax/person_info/` and `/person/{id}-{name}/` for person biography and
  filmography
- `/engine/ajax/gettrailervideo.php` for trailer metadata and embed code
- `/ajax/comments_likes/` for users who liked a comment
- `/engine/ajax/quick_content.php` for hover-card details
- `/engine/ajax/get_newest_slider_content.php` for the compact home-page slider
- the home-page hot episode update list
- country, year, genre, and best-rating directory pages

Quick-content and slider endpoints duplicate data already returned by catalog
or media pages. Calling them for every card would create an N+1 request pattern,
so they should remain explicit opt-in operations instead of enriching every
catalog result automatically

## Account-changing endpoints

Playback progress, continue-watching state, and bookmark changes are exposed as
explicit `AccountClient` operations

They never run as a side effect of loading a page or resolving a stream

`SavePlaybackProgressAsync` sends the media and translator identifiers together
with optional season, episode, current position, and duration

`SetContinueWatchingWatchedAsync` uses the state from a loaded
`ContinueWatchingEntry` and only calls the website toggle when a change is needed

`Media.SetBookmarkAsync` uses the selected folders parsed from the media page
and calls the website toggle only when the requested state differs

`AccountClient.ToggleBookmarkAsync` exposes the raw checkbox behavior and is
intentionally named as a toggle because the same `add_post` action handles both
states

Ratings, watched episode schedule state, comment creation, and comment likes
remain unexposed until they receive their own validated contracts and tests

## Performance model

No dedicated threads are required for network requests. `HttpClient` waits
asynchronously, while bulk operations use `MaxConcurrentRequests` to overlap
independent requests without sending an uncontrolled burst to the website

The first page of a full search determines the total page count, remaining
pages load concurrently, and results are restored to page order. Translator
catalogs and whole-season streams use the same limited-concurrency model
