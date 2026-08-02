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
| Create comment or reply | `POST /ajax/add_comment/` | JSON containing rendered comment HTML or validation messages |
| Delete own comment | `GET /engine/ajax/deletecomments.php` | JSON |
| Toggle comment like | `GET /engine/ajax/comments_like.php` | JSON with resulting state and count |
| Comment like users | `POST /ajax/comments_likes/` | JSON containing an HTML fragment |
| Report comment | `POST /engine/ajax/complaint.php` | JSON |
| Submit internal rating | `GET /engine/ajax/rating.php` | JSON with updated aggregate rating |
| Trailer | `POST /engine/ajax/gettrailervideo.php` | JSON with embed markup and metadata |
| Schedule watched state | `POST /engine/ajax/schedule_watched.php` | JSON |
| Password and avatar settings | `GET /settings/` and `GET /settings/security/` | HTML forms with a security token |
| General and playback settings | `POST /user/{id}/` and `POST /user/{id}/personality/` | HTML confirmation or validation errors |
| Save password or remove avatar | `POST /user/{id}/` or `POST /user/{id}/security/` | HTML confirmation or validation errors |
| Upload and crop avatar | `POST /engine/ajax/upload_avatar.php` | JSON from separate temporary upload and crop requests |

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
- franchises
- person biography and filmography
- Premium offers and payment history
- full search results

Extended media metadata, recommendations, and series schedules are parsed from
the already downloaded media page and do not create extra requests

## Optional read-only sources not exposed

These sources can be added when their data is needed:

- `/engine/ajax/quick_content.php` for hover-card details
- `/engine/ajax/get_newest_slider_content.php` for the compact home-page slider
- the home-page hot episode update list

Country, year, genre, and best-rating pages are available through
`CatalogClient.GetDirectoryAsync`. The structured `CatalogQuery` covers the
normal category, genre, year, and best-rating path shape, while the relative
path overload covers compatible mirror-specific directories

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

The library exposes password changes, avatar upload and removal, internal
ratings, comment creation, replies, and deletion as explicit methods. They are
never called while loading profile, media, or comment data.

The website exposes deletion controls for comments owned by the authenticated
account. Its current interface and JavaScript do not expose comment editing,
so the library does not implement an artificial delete-and-repost operation.

Watched schedule state, comment likes, comment reports, profile settings,
playback preferences, and bulk bookmark operations are explicit mutations.
They never run while loading a page

## Performance model

No dedicated threads are required for network requests. `HttpClient` waits
asynchronously, while bulk operations use `MaxConcurrentRequests` to overlap
independent requests without sending an uncontrolled burst to the website

The first page of a full search determines the total page count, remaining
pages load concurrently, and results are restored to page order. Translator
catalogs and whole-season streams use the same limited-concurrency model
