# Architecture and development

## Repository layout

Production code is compiled into one `HDRezka.NET` assembly, while public
types use the single `HdRezka` namespace even though implementation
responsibilities are separated by directory:

| Directory | Responsibility |
| --- | --- |
| `Client` | Main entry point, configuration, authentication state, and cookie helpers |
| `Account` | Profile metadata, continue-watching history, and bookmarks |
| `Catalog` | Home-page catalog sections and shared media card models |
| `Collections` | Curated collection directory and collection contents |
| `Comments` | Paginated comment AJAX client and comment models |
| `Media` | Media facade, metadata, streams, subtitles, translators, seasons, and episodes |
| `Search` | Fast and full catalog search |
| `Exceptions` | Public library exception hierarchy |
| `Abstractions` | Internal contracts shared by clients and parsers |
| `Http` | Transport, cookies, headers, proxy behavior, and decompression |
| `Scraping` | AngleSharp parsing and authentication page inspection |
| `Translators` | Translator ordering and automatic selection |

The test projects are:

- `tests/HDRezka.NET.Tests` — deterministic unit tests with stubbed HTTP
- `tests/HDRezka.NET.IntegrationTests` — opt-in live website checks

## Build and test

```shell
dotnet restore HDRezka.NET.slnx
dotnet build HDRezka.NET.slnx --configuration Release --no-restore
dotnet test HDRezka.NET.slnx --configuration Release --no-build
```

## Pack

```shell
dotnet pack src/HDRezka.NET/HDRezka.NET.csproj \
  --configuration Release \
  --output artifacts
```

## Live integration test

The live test reads credentials and origin from environment variables:

```shell
HDREZKA_TEST_EMAIL="mail@example.com" \
HDREZKA_TEST_PASSWORD="password" \
HDREZKA_TEST_ORIGIN="https://your-mirror.example" \
dotnet test tests/HDRezka.NET.IntegrationTests \
  --configuration Release \
  --filter "Category=Live"
```

Do not commit real credentials or mirror-specific secrets

## Maintaining this wiki

The source of truth is `docs/wiki` in the main repository, so edit those files
in a normal branch, review them with the code change they describe, and do not
edit the generated GitHub Wiki directly because the next synchronization
replaces its Markdown files

The `Publish Wiki` GitHub Actions workflow runs when `docs/wiki` changes on
`main` and can also be started manually, copying the directory into the
repository's Git-backed wiki and creating a wiki commit only when content
changed

GitHub requires one initial wiki page before the wiki Git repository can be
cloned, so if the workflow reports that the wiki repository does not exist, open
the repository's **Wiki** tab, create a temporary `Home` page, and rerun the
workflow, then the synchronized `Home.md` will replace it

## Documentation conventions

- Use one top-level `#` heading per page
- Use GitHub Wiki page links such as `[Search](Search)`
- Add new reader-facing pages to `_Sidebar.md` and `Home.md`
- Keep examples compilable against the current public signatures
- Use placeholders for origins, paths, credentials, cookies, and stream URLs
- Explain cancellation and ownership for long-running or disposable APIs
