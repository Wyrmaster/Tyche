# Random image API: tasks

[spec](spec.md) · [plan](plan.md) · [tasks](tasks.md) · [spec (readable)](spec-humanreadable.md) · [plan (readable)](plan-humanreadable.md) · [tasks (readable)](tasks-humanreadable.md)

Each task includes its own tests. Review happens after each step group; the Tester runs after T-15.

## Step 1: Foundation

- [x] <a id="t-1"></a>**T-1** Move the project to `Tyche\Service\Library\`, add `Tyche\Service\Test\Tyche.Service.Test` (xUnit), update `Tyche.slnx`, add `Directory.Build.props` (Authors, Version defaults, InformationalVersion without revision suffix) ([FR-9](spec.md#fr-9-versioning-and-author-in-the-project), [AC-9.1](spec.md#ac-9-1), [AC-9.2](spec.md#ac-9-2), [AC-9.3](spec.md#ac-9-3))
- [x] <a id="t-2"></a>**T-2** `ImageApiOptions`, bind and validate on start with clear messages, defaults from the plan, sample `appsettings.json` ([FR-2](spec.md#fr-2-collections-in-appsettingsjson), [AC-2.6](spec.md#ac-2-6))
- [x] <a id="t-3"></a>**T-3** `ImageFormat` and `IImageFormatMap` (extensions, content types, `jpeg` = jpg) ([FR-4](spec.md#fr-4-format-conversion), [AC-4.2](spec.md#ac-4-2))

## Step 2: Finding images

- [x] <a id="t-4"></a>**T-4** `ICollectionCatalog` (recursive scan, supported types only, TTL listing, default and unknown collection) and `IRandomImagePicker` ([FR-2](spec.md#fr-2-collections-in-appsettingsjson), [AC-2.1](spec.md#ac-2-1), [AC-2.2](spec.md#ac-2-2), [AC-2.3](spec.md#ac-2-3), [AC-2.4](spec.md#ac-2-4), [AC-2.5](spec.md#ac-2-5), [AC-2.7](spec.md#ac-2-7), [AC-1.2](spec.md#ac-1-2), [AC-1.5](spec.md#ac-1-5))
- [x] <a id="t-5"></a>**T-5** `ImageRequest` and `IImageRequestParser` with all validation rules and `ProblemDetails` messages ([FR-3](spec.md#fr-3-resize), [AC-3.6](spec.md#ac-3-6), [FR-4](spec.md#fr-4-format-conversion), [AC-4.3](spec.md#ac-4-3))

## Step 3: Processing and cache

- [x] <a id="t-6"></a>**T-6** `IProcessRunner` and `IImageProcessor`: fit/fill/dpi/format arguments, animation handling, alpha handling for jpg, timeouts, concurrency limit, `-limit` flags ([FR-3](spec.md#fr-3-resize), [AC-3.1](spec.md#ac-3-1), [AC-3.2](spec.md#ac-3-2), [AC-3.3](spec.md#ac-3-3), [AC-3.4](spec.md#ac-3-4), [AC-3.5](spec.md#ac-3-5), [AC-3.7](spec.md#ac-3-7), [FR-4](spec.md#fr-4-format-conversion), [AC-4.1](spec.md#ac-4-1), [AC-4.4](spec.md#ac-4-4))
- [x] <a id="t-7"></a>**T-7** `IImageCache` / `DiskImageCache`: key, per-key lock, atomic write, LRU eviction, temp cleanup on start ([FR-5](spec.md#fr-5-cache-of-converted-images), [AC-5.1](spec.md#ac-5-1), [AC-5.2](spec.md#ac-5-2), [AC-5.3](spec.md#ac-5-3), [AC-5.4](spec.md#ac-5-4), [AC-5.5](spec.md#ac-5-5), [AC-5.6](spec.md#ac-5-6))

## Step 4: API

- [x] <a id="t-8"></a>**T-8** `IImageService` and `GET /image` endpoint: pass-through, processed, `no-store`, error mapping ([FR-1](spec.md#fr-1-random-image-endpoint), [AC-1.1](spec.md#ac-1-1), [AC-1.3](spec.md#ac-1-3), [AC-1.4](spec.md#ac-1-4))
- [x] <a id="t-9"></a>**T-9** `IVersionProvider` and `GET /version` ([FR-6](spec.md#fr-6-version-and-health-endpoints), [AC-6.1](spec.md#ac-6-1))
- [x] <a id="t-10"></a>**T-10** `MagickHealthCheck` and `GET /health` ([FR-6](spec.md#fr-6-version-and-health-endpoints), [AC-6.2](spec.md#ac-6-2))

## Step 5: Delivery

- [x] <a id="t-11"></a>**T-11** Alpine multi-stage `Dockerfile` with ImageMagick, non-root, volumes, healthcheck, labels, version build args; update `compose.yaml` ([FR-7](spec.md#fr-7-docker-image), [AC-6.3](spec.md#ac-6-3), [AC-7.1](spec.md#ac-7-1), [AC-7.2](spec.md#ac-7-2), [AC-7.3](spec.md#ac-7-3), [AC-7.4](spec.md#ac-7-4), [AC-7.5](spec.md#ac-7-5))
- [x] <a id="t-12"></a>**T-12** `.github/workflows/release.yml`: tag trigger, tests, version and build number, GHCR push with three tags ([FR-8](spec.md#fr-8-github-build-on-version-tags), [AC-8.1](spec.md#ac-8-1), [AC-8.2](spec.md#ac-8-2), [AC-8.3](spec.md#ac-8-3), [AC-8.4](spec.md#ac-8-4), [AC-8.5](spec.md#ac-8-5))
- [x] <a id="t-13"></a>**T-13** `README.md`: usage, parameters, settings, compose example, release steps, author Stefan Pichler ([AC-9.3](spec.md#ac-9-3))

## Step 6: Verification

- [x] <a id="t-14"></a>**T-14** Integration tests with `WebApplicationFactory` and real `magick` for all endpoints and acceptance criteria not yet covered ([FR-1](spec.md#fr-1-random-image-endpoint), [FR-3](spec.md#fr-3-resize), [FR-4](spec.md#fr-4-format-conversion), [FR-5](spec.md#fr-5-cache-of-converted-images), [FR-6](spec.md#fr-6-version-and-health-endpoints))
- [x] <a id="t-15"></a>**T-15** Build the Docker image locally with `VERSION`/`BUILD_NUMBER`, run it with volumes, check `/image`, `/version`, `/health`, non-root and image size ([AC-7.1](spec.md#ac-7-1), [AC-7.2](spec.md#ac-7-2), [AC-6.3](spec.md#ac-6-3))
