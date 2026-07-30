# HDRezka.NET

HDRezka.NET is an asynchronous .NET 10 library for HDRezka-compatible
websites and can load movie and series metadata, select translations, resolve
video streams and subtitles, enumerate seasons and episodes, search and browse
the catalog, load curated collections, and work with authenticated account data

> [!IMPORTANT]
> Website mirrors can change their markup and endpoints without notice; the
> library does not contain a hard-coded domain, so always pass a mirror URL that
> you are allowed to access and comply with the laws and terms that apply to
> you

## Start here

- [Getting started](Getting-Started) — install the package and load the first
  media page
- [Authentication](Authentication) — log in, restore cookies, inspect account
  state, and log out
- [Account data](Account-Data) — load the profile, continue-watching history,
  and bookmark folders
- [Media metadata](Media-Metadata) — work with titles, ratings, formats,
  categories, translators, and related parts
- [Streams and subtitles](Streams-and-Subtitles) — resolve movie and episode
  streams, qualities, and subtitle tracks
- [Series and episodes](Series-and-Episodes) — enumerate seasons and load a
  complete season
- [Search](Search) — use fast suggestions or paginated catalog search
- [Catalogs and collections](Catalogs-and-Collections) — browse home-page
  sections and curated collections
- [Configuration](Configuration) — configure headers, cookies, proxies,
  translator priorities, and a custom `HttpClient`
- [Premium access](Premium-Access) — understand account tiers and protected
  qualities or translations
- [Error handling](Error-Handling) — handle library-specific and standard
  exceptions
- [Troubleshooting](Troubleshooting) — diagnose the most common integration
  failures

## Requirements

- .NET 10 SDK or a .NET 10 application
- An HDRezka-compatible website origin

All public types use the `HdRezka` namespace, while all network methods are
asynchronous and accept a `CancellationToken`

## Project links

- [Source code](https://github.com/freakdaniel/HDRezka.NET)
- [README](https://github.com/freakdaniel/HDRezka.NET#readme)
- [NuGet package](https://www.nuget.org/packages/HDRezka.NET)
- [Issues](https://github.com/freakdaniel/HDRezka.NET/issues)
- [Architecture and development](Architecture-and-Development)
