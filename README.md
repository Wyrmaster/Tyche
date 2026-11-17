# Tyche

Author: Stefan Pichler

Tyche is a small web service that returns a random image from folders you configure, optionally resized and converted.
Point a dashboard such as Homarr at `http://host:8080/image` as background image and it changes on every load. There is
no authentication and no HTTPS: run it on your home network only. Resizing uses the ImageMagick CLI, no image library.

## Usage

```
GET /image?collection=nature&width=1920&height=1080&dpi=96&mode=fit&format=webp
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `collection` | `DefaultCollection` | Name of a configured collection. Unknown name: `404`. |
| `width`, `height` | none | CSS pixels, positive whole numbers. |
| `dpi` | `96` | Scale factor relative to 96. Output pixels are `round(size * dpi / 96)`. |
| `mode` | `fit` | `fit`: scale down to fit inside the box, keep the aspect ratio, never crop, never upscale (one size may be omitted). `fill`: scale and centre-crop to exactly width x height, may upscale, needs both sizes. |
| `format` | source format | `jpg` (alias `jpeg`), `png`, `webp` or `gif`. Alone it converts at the original size. |

- Without `width`, `height` and `format` the original file is returned unchanged (no ImageMagick call).
- Invalid parameters return `400` with the reason, an empty or unknown collection `404`, an ImageMagick failure `500`
  (details only in the log). The maximum output size is `MaxDimension` pixels per side (default 8192).
- Every response has `Cache-Control: no-store`, so each load gets a new random image.
- Animated gifs and webps keep their animation when the output is gif or webp, otherwise the first frame is used.
- Converted images are cached on disk (key: source path, modification time, size and all parameters).

Other endpoints: `GET /version` returns the version, for example `v1.0.1`. `GET /health` returns `200` when the service
runs and ImageMagick can be started, otherwise `503`.

## Settings

Settings are in the `ImageApi` section of `appsettings.json`. With environment variables use `__` as separator, for
example `ImageApi__Collections__nature=/images/nature`. Wrong settings stop the service at startup with a message.

| Key | Default | Notes |
| --- | --- | --- |
| `Collections` | none | name to folder path; folders are scanned recursively for jpg, jpeg, png, webp, gif |
| `DefaultCollection` | none | must be one of `Collections` |
| `MaxDimension` | 8192 | maximum output pixels per side |
| `CachePath` | `/cache` | folder for converted images |
| `CacheMaxSizeMb` | 500 | least recently used entries are removed above this |
| `MagickPath` | `magick` | binary name or path |
| `ProcessTimeoutSeconds` | 30 | per ImageMagick run |
| `MaxConcurrentProcesses` | CPU count | parallel ImageMagick runs |
| `ListingCacheSeconds` | 10 | how long a folder listing is remembered; added or removed files show up after this |

## Docker

The image is based on `dotnet/aspnet:10.0-alpine` with ImageMagick, runs as the non-root user `app` (uid 1654) and
listens on port 8080. Mount your images (read-only is fine) and a cache volume the `app` user can write to
(a named volume works out of the box). A bind-mounted `/cache` must be writable by uid 1654, for example `chown 1654:1654 ./cache`.

Two compose files are provided:

- `compose.yaml` is for productive use. It runs the published image `ghcr.io/wyrmaster/tyche:latest`; adjust the image
  folder under `volumes:` and start it with `docker compose up -d`.
- `compose.dev.yaml` is for development. It builds the image from the local source, runs in the Development environment,
  limits memory to 1 GB and mounts `./dev-images` (read-only) and uses a named cache volume. Put a few small images into
  `./dev-images/nature` and `./dev-images/city`, then run `docker compose -f compose.dev.yaml up --build`.

Build it yourself from the repository root:

```
docker build -f Tyche/Service/Library/Dockerfile --build-arg VERSION=1.0.1 --build-arg BUILD_NUMBER=7 -t tyche .
```

## Development

```
dotnet build
./inspectcode.sh  # Rider/ReSharper inspections (warnings and errors fail); complexity limits are checked by dotnet build
dotnet test      # needs ImageMagick 7 (`magick`); the integration tests are skipped without it (not with REQUIRE_MAGICK=1 or CI=true)
docker build -f Tyche/Service/Library/Dockerfile --target test .   # runs all tests on Alpine with ImageMagick
```

Local builds get the version `v0.0.0.0-dev` (assembly version `0.0.0.0`).

## Release

The version comes from the git tag only, nothing in the source changes for a release.

1. Make sure the tests pass on `main`.
2. Tag and push: `git tag v1.0.1 && git push origin v1.0.1`.
3. The GitHub workflow `.github/workflows/release.yml` runs the tests (in the Dockerfile `test` stage on Alpine), then builds and pushes the image to GHCR with the
   tags `v1.0.1.<run number>`, `v1.0.1` and `latest`. If a test fails nothing is published.

The build version is `v1.0.1.<run number>` (4 parts); `/version` shows three parts.

## License

See [LICENSE](LICENSE).
