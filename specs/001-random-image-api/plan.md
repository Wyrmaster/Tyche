# Random image API: plan

[spec](spec.md) · [plan](plan.md) · [tasks](tasks.md) · [spec (readable)](spec-humanreadable.md) · [plan (readable)](plan-humanreadable.md) · [tasks (readable)](tasks-humanreadable.md)

Status: In progress

## Decisions

- **Stack**: .NET 10 minimal API in the existing `Tyche.Service` project, xUnit tests, ImageMagick 7 (`magick`) CLI.
- **Folders** (global C# rule, siblings → `Library`): `Tyche.Service` moves to `Tyche\Service\Library\`, the new test
  project `Tyche.Service.Test` goes to `Tyche\Service\Test\`. The solution, Dockerfile and compose paths follow.
- **Open questions of the spec**
  - Animated gifs/webp keep their animation when the output format can animate (gif, webp); otherwise the first frame
    is used (AC-3.7).
  - `Cache-Control: no-store` is always sent, not switchable (YAGNI).
  - Limits are settings with the proposed defaults: max 8192 px per side, cache 500 MB.
- **No shell**: ImageMagick is started with `ProcessStartInfo.ArgumentList`, never through a shell. The input path comes
  from the folder scan, never from the client; all client values are parsed to numbers/enums before use (AC-2.7).
- **Process safety**: timeout per run (default 30 s), a limit on concurrent runs (default = CPU count), and ImageMagick
  `-limit` flags for memory/map/time, so a hostile request cannot exhaust the host.
- **Cache key**: SHA-256 over source path, modification time, file length and all normalized parameters.
  Result files are written to a temp name in the cache folder and renamed atomically (AC-5.6).
- **Listing**: a collection's files are enumerated recursively and cached in memory for 10 s, so added/removed files
  show up without a restart without scanning on every request (AC-2.5).

## Settings (`ImageApi` section)

| Key | Default | Notes |
| --- | --- | --- |
| `Collections` | none | name → folder path |
| `DefaultCollection` | none | must exist in `Collections` |
| `MaxDimension` | 8192 | output pixels per side |
| `CachePath` | `/cache` | folder for converted images |
| `CacheMaxSizeMb` | 500 | oldest-used entries removed above this |
| `MagickPath` | `magick` | binary name or path |
| `ProcessTimeoutSeconds` | 30 | per ImageMagick run |
| `MaxConcurrentProcesses` | CPU count | |
| `ListingCacheSeconds` | 10 | |

Docker overrides use `ImageApi__Collections__nature=/images/nature` and so on.

## Parts of the solution

All new types are one per file; every service has an interface (global rules). Root namespace `Tyche.Service`.

### Versioning and author (FR-9) · [T-1](tasks.md#t-1), [T-13](tasks.md#t-13)
`Directory.Build.props` at the repo root sets `Authors` (Stefan Pichler), `Version` (default `0.0.0.0`),
`InformationalVersion` (default `v0.0.0.0-dev`) and turns off the source-revision suffix. CI and Docker pass
`-p:Version=1.0.1.7 -p:InformationalVersion=v1.0.1.7`. The service reads its version from the assembly.

### Configuration (FR-2) · [T-2](tasks.md#t-2)
`ImageApiOptions` bound from `ImageApi`, with `IValidateOptions` checking: at least one collection, default exists,
folders exist and are readable, positive limits. Validated on start (`ValidateOnStart`), so wrong settings fail
at startup with a message.

### Formats · [T-3](tasks.md#t-3)
`ImageFormat` enum (jpg, png, webp, gif) and `IImageFormatMap` for extension ↔ format ↔ content type, case-insensitive,
`jpeg` is jpg.

### Catalog and picking (FR-1, FR-2) · [T-4](tasks.md#t-4)
`ICollectionCatalog` resolves a name (or the default) to its files, recursive, supported extensions only, cached for the
listing TTL; unknown collection → not found result. `IRandomImagePicker` chooses uniformly.

### Request parsing (FR-3, FR-4) · [T-5](tasks.md#t-5)
`ImageRequest` record (collection, width, height, dpi, mode, format) and `IImageRequestParser` producing either a valid
request or a list of problems for `400` (`ProblemDetails`). Rules: positive ints, dpi default 96 and positive,
`fill` needs both sizes, scaled output ≤ `MaxDimension`, `mode`/`format` from the allowed set. A request without
width, height or format is a "pass-through" request.

### Processing (FR-3, FR-4) · [T-6](tasks.md#t-6)
`IProcessRunner` (runs a program with an argument list, timeout, captures stderr) and `IImageProcessor` which builds the
`magick` arguments:

- Input `path[0]`, or `-coalesce` first when the source and target both animate.
- `-auto-orient`, `-strip`; jpg target: `-background white -alpha remove -alpha off`; quality 85 for jpg/webp.
- Scaled size = `round(size × dpi / 96)`.
- `fit`: `-resize WxH>` (only `Wx>` or `xH>` with one side), so it never upscales.
- `fill`: `-resize WxH^ -gravity center -extent WxH`.
- Format only: no resize, just the target format.
- A semaphore enforces `MaxConcurrentProcesses`; `-limit memory/map/time` are always added.

### Cache (FR-5) · [T-7](tasks.md#t-7)
`IImageCache` / `DiskImageCache`: `GetOrCreateAsync(key, factory)` with a per-key lock so identical simultaneous requests
convert once; hit updates the file's access time; miss writes a temp file and moves it. After a write, if the folder is
over `CacheMaxSizeMb`, the least recently used files are deleted (guarded so only one eviction runs at a time); temp
files left from a crash are cleaned at start.

### Service and endpoints (FR-1, FR-6) · [T-8](tasks.md#t-8), [T-9](tasks.md#t-9), [T-10](tasks.md#t-10)
`IImageService` ties it together: catalog → picker → pass-through file or cache/processor. `ImageEndpoints` maps
`GET /image`, `GET /version`, `GET /health` and sets `Cache-Control: no-store` on `/image`. `IVersionProvider` takes the
informational version, drops the build part and any suffix (`v1.0.1.7` → `v1.0.1`, dev → `v0.0.0-dev`).
`MagickHealthCheck` runs `magick -version`; unhealthy → `503`. Errors map to `404` (no image/unknown collection),
`400` (bad parameter), `500` with a generic message when ImageMagick fails (details only in the log).

### Docker (FR-7) · [T-11](tasks.md#t-11)
Multi-stage `Tyche/Service/Library/Dockerfile` (moved with the project): `dotnet/sdk:10.0-alpine` builds,
`dotnet/aspnet:10.0-alpine` runs with `apk add --no-cache imagemagick imagemagick-jpeg imagemagick-webp`.
Build args `VERSION` and `BUILD_NUMBER` feed `dotnet publish`. Non-root `app` user owns `/cache`; `/images` is a
read-only mount. Port 8080 only (no HTTPS). `HEALTHCHECK` uses `wget -q --spider http://localhost:8080/health`.
Labels: `org.opencontainers.image.authors`, `.version`, `.source`. `compose.yaml` shows volumes, port and example
settings.

### GitHub workflow (FR-8) · [T-12](tasks.md#t-12)
`.github/workflows/release.yml`, trigger `push` of tags `v[0-9]+.[0-9]+.[0-9]+`. Steps: checkout, run the tests by building the
Dockerfile `test` target (Alpine, same ImageMagick as the shipped image; `push: false`; missing `magick` fails the run
instead of skipping, via `REQUIRE_MAGICK=1`), log in to GHCR with `GITHUB_TOKEN` (`packages: write`),
derive version from `github.ref_name` and build number from `github.run_number`, build and push with Buildx tags
`v<ver>.<run>`, `v<ver>`, `latest`. Test failure stops before the push.

### Tests · [T-14](tasks.md#t-14), [T-15](tasks.md#t-15)
`Tyche.Service.Test` (xUnit). Unit tests per part (parser, argument builder, catalog, cache, version provider).
Integration tests with `WebApplicationFactory` and temp folders using the real `magick`; they are skipped with a clear
message when `magick` is not installed locally, and always run in CI. Each task ships its own tests.

## Review and test phases
After each group of tasks the Reviewer reviews against this plan and the style rules. When all tasks are done the Tester
attacks the result (hostile parameters, path tricks, huge or corrupt images, zip-bomb style files, concurrency, cache
eviction under load, container as non-root with read-only images) and runs the acceptance criteria end to end.

## Changes after review
- CI runs the tests in a Dockerfile `test` stage on Alpine instead of `dotnet test` on the runner (runner only has
  ImageMagick 6; the shipped image uses Alpine ImageMagick 7). Approved by Stefan Pichler. Touches [T-11](tasks.md#t-11)
  and [T-12](tasks.md#t-12).

## Risks
- **Alpine ImageMagick packages**: exact package names/delegates (jpeg, webp, gif animation) must be verified in the
  image build; fallback is adding the missing `imagemagick-*` delegate package.
- **Animated webp** support in the Alpine build may be limited; fallback is first frame (documented).
- **Cache eviction under load** is the most delicate part (races between hit, write and delete); tested explicitly.
- **Folder layout rule** is read as `Tyche\Service\Library` per the global example; moving costs only paths in
  solution, Dockerfile and compose.
- Large source images are read fully by ImageMagick; limited by the `-limit` flags and timeout, not by file size checks.
