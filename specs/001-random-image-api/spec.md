# Random image API

[spec](spec.md) · [spec (readable)](spec-humanreadable.md)

Status: In progress

## Problem
Dashboards such as Homarr can use an image URL as background. Stefan Pichler wants one small service on the home network
that serves a random image from folders he configures, optionally resized to the dashboard's size, so the background
changes on every load. No authentication or encryption is needed (home network only). Resizing and conversion use the
ImageMagick CLI inside the container, no image libraries.

## Feature requests

### FR-1 Random image endpoint
`GET /image` returns the bytes of one randomly chosen image from a collection, with the correct `Content-Type`. It can be
used directly as a CSS/Homarr background URL. Query parameters: `collection`, `width`, `height`, `dpi`, `mode`, `format`.
The response carries `Cache-Control: no-store` so every load gets a new random image.

- [x] <a id="ac-1-1"></a>**AC-1.1** `GET /image` returns `200` with an image body and a matching `Content-Type`
- [x] <a id="ac-1-2"></a>**AC-1.2** Repeated calls return different images (uniformly random over the files of the collection)
- [x] <a id="ac-1-3"></a>**AC-1.3** The response has `Cache-Control: no-store`
- [x] <a id="ac-1-4"></a>**AC-1.4** Without resize or format parameters the original file is returned unchanged, without calling ImageMagick
- [x] <a id="ac-1-5"></a>**AC-1.5** A collection without any image returns `404` with a short problem message, not a `500`

### FR-2 Collections in appsettings.json
Image folders are configured by name in `appsettings.json` (and thereby overridable by environment variables in Docker):
a `Collections` map of name to folder path, and a `DefaultCollection` name. Folders are scanned recursively for `jpg`,
`jpeg`, `png`, `webp` and `gif` (case-insensitive). The scan picks up added and removed files without a restart.

- [x] <a id="ac-2-1"></a>**AC-2.1** `?collection=nature` serves from the folder configured as `nature`
- [x] <a id="ac-2-2"></a>**AC-2.2** Without `collection` the `DefaultCollection` is used
- [x] <a id="ac-2-3"></a>**AC-2.3** An unknown collection name returns `404`
- [x] <a id="ac-2-4"></a>**AC-2.4** Subfolders are included, other file types are ignored
- [x] <a id="ac-2-5"></a>**AC-2.5** Files added to or removed from a folder are reflected without restarting the service
- [x] <a id="ac-2-6"></a>**AC-2.6** Invalid configuration (no collections, missing default, folder does not exist) is reported with a clear message at startup or on request, never silently serving nothing
- [x] <a id="ac-2-7"></a>**AC-2.7** A request can never read a file outside the configured folders (no path from the client is used)

### FR-3 Resize
`width` and `height` are in CSS pixels. `dpi` is a scale factor relative to 96 (default 96): output pixels are
`round(size × dpi / 96)`. `mode` is `fit` (default) or `fill`.

- `fit`: scale down to fit inside width × height, keep the aspect ratio, never crop, never upscale. One of the two sizes
  may be omitted; the other limits the size.
- `fill`: scale and centre-crop to exactly width × height (after dpi scaling); needs both sizes. May upscale.

- [x] <a id="ac-3-1"></a>**AC-3.1** `width=800&height=600` in `fit` mode returns an image that fits inside 800×600 with the original aspect ratio
- [x] <a id="ac-3-2"></a>**AC-3.2** Only `width` or only `height` given in `fit` mode keeps the aspect ratio
- [x] <a id="ac-3-3"></a>**AC-3.3** `mode=fill` with both sizes returns exactly width × height pixels
- [x] <a id="ac-3-4"></a>**AC-3.4** `dpi=192` doubles the output pixel size compared to the default
- [x] <a id="ac-3-5"></a>**AC-3.5** `fit` never upscales: a source smaller than the requested size is returned at its own size
- [x] <a id="ac-3-6"></a>**AC-3.6** `mode=fill` without both sizes, a non-numeric or non-positive value, an unknown `mode`, or a value above the configured maximum (default 8192 output pixels per side) returns `400` with the reason
- [x] <a id="ac-3-7"></a>**AC-3.7** Animated gifs are resized without breaking (first frame is acceptable if animation cannot be kept; decided in the plan)

### FR-4 Format conversion
`format=jpg|png|webp|gif` (`jpeg` is accepted as alias of `jpg`) converts the output. Without `format` the original format is kept. `format` can be combined
with resizing; on its own it converts at the original size.

- [x] <a id="ac-4-1"></a>**AC-4.1** `format=webp` returns a webp image with `Content-Type: image/webp`
- [x] <a id="ac-4-2"></a>**AC-4.2** Without `format` the output format equals the source format
- [x] <a id="ac-4-3"></a>**AC-4.3** An unsupported `format` returns `400`
- [x] <a id="ac-4-4"></a>**AC-4.4** `format` and resize parameters combine in one request

### FR-5 Cache of converted images
Converted and resized images are cached on disk to avoid repeating the ImageMagick call. The key covers the source file
(path and modification time) and all parameters. Originals are not cached (they are served as-is).

- [x] <a id="ac-5-1"></a>**AC-5.1** The same source file with the same parameters is served from the cache the second time, without calling ImageMagick
- [x] <a id="ac-5-2"></a>**AC-5.2** A changed source file (new modification time) does not return the stale cached result
- [x] <a id="ac-5-3"></a>**AC-5.3** The cache folder is configurable and can be a Docker volume
- [x] <a id="ac-5-4"></a>**AC-5.4** The cache has a configurable size limit (default 500 MB); the oldest unused entries are removed when it is exceeded
- [x] <a id="ac-5-5"></a>**AC-5.5** Simultaneous identical requests do not corrupt the cache or run the same conversion several times in parallel
- [x] <a id="ac-5-6"></a>**AC-5.6** A failed conversion leaves no partial file in the cache

### FR-6 Version and health endpoints
`GET /version` returns the version without build number (`v1.1.5`). `GET /health` returns `200` when the service is up
and at least the ImageMagick binary can be started; used for the Docker healthcheck.

- [x] <a id="ac-6-1"></a>**AC-6.1** `/version` returns the major.minor.patch version of the build, e.g. `v1.1.5`
- [x] <a id="ac-6-2"></a>**AC-6.2** `/health` returns `200` when healthy and `503` when ImageMagick is not usable
- [x] <a id="ac-6-3"></a>**AC-6.3** The Docker image has a `HEALTHCHECK` using `/health`

### FR-7 Docker image
The service is delivered as a small Alpine-based image with ImageMagick installed, running as a non-root user.

- [x] <a id="ac-7-1"></a>**AC-7.1** The final image is based on the .NET Alpine runtime and installs ImageMagick (only the packages needed for the supported formats)
- [x] <a id="ac-7-2"></a>**AC-7.2** The service runs as a non-root user and can read the mounted image folders and write the cache folder
- [x] <a id="ac-7-3"></a>**AC-7.3** Image folders and the cache folder are mounted as volumes; settings can be overridden with environment variables
- [x] <a id="ac-7-4"></a>**AC-7.4** `compose.yaml` (productive, published image) and `compose.dev.yaml` (development, local build) show working examples with the volumes and port
- [x] <a id="ac-7-5"></a>**AC-7.5** The image labels name the author (Stefan Pichler) and the build version

### FR-8 GitHub build on version tags
A GitHub Actions workflow builds and publishes the image when a tag like `v1.0.1` is pushed.

- [ ] <a id="ac-8-1"></a>**AC-8.1** Pushing a tag `vMAJOR.MINOR.PATCH` starts the workflow; other pushes do not publish an image
- [x] <a id="ac-8-2"></a>**AC-8.2** The build number is the GitHub run number, giving the build version `v1.0.1.<run>`
- [ ] <a id="ac-8-3"></a>**AC-8.3** The image is pushed to GHCR with the tags `v1.0.1.<run>`, `v1.0.1` and `latest`
- [ ] <a id="ac-8-4"></a>**AC-8.4** The build runs the tests and fails without publishing when they fail
- [x] <a id="ac-8-5"></a>**AC-8.5** Version information comes from the tag only, nothing in the source has to change for a release

### FR-9 Versioning and author in the project
The project follows the global conventions: version from the git tag, build number as 4th part, web applications show
three parts, author is Stefan Pichler.

- [x] <a id="ac-9-1"></a>**AC-9.1** The assembly/file version is the build version (4 parts) and the informational version is derived from the tag
- [x] <a id="ac-9-2"></a>**AC-9.2** A local build without a tag gets a clear development version (e.g. `v0.0.0.0-dev`) instead of failing
- [x] <a id="ac-9-3"></a>**AC-9.3** `Authors` in the project file and the README name Stefan Pichler

## Out of scope
- Authentication, HTTPS/encryption (home network only)
- Uploading, deleting or managing images through the API
- A web UI or gallery
- Image libraries (all processing via the ImageMagick CLI)
- Other formats than jpg, png, webp, gif (e.g. avif, heic)
- A cap on queued requests or on the number of distinct cache keys: many different `width`/`height` combinations each
  cause a conversion; only the number of parallel processes is bounded (accepted risk on a home network, Tester finding)
- Protection against symlinks inside a configured image folder that point outside it; the folders are set by the operator
  and no client input is ever used as a path
- Optimizing the cache eviction scan, which lists the cache folder on every cache miss (fine at the default 500 MB)

## Open questions
Resolved in the [plan](plan.md#decisions):
- Animated gifs keep their animation when the output can animate (gif, webp), otherwise the first frame is used
- The no-store header is always sent, not switchable
- Defaults of 8192 px per side and 500 MB cache are kept as settings
