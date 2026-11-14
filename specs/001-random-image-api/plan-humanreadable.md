# Random image API: plan (readable)

[spec](spec.md) · [plan](plan.md) · [tasks](tasks.md) · [spec (readable)](spec-humanreadable.md) · [plan (readable)](plan-humanreadable.md) · [tasks (readable)](tasks-humanreadable.md)

Status: Done

## Answers to the open questions
- Animated gifs keep their animation when the result is gif or webp, otherwise the first frame is used.
- The "don't keep this" header is always on.
- Limits are settings: 8192 px maximum per side, 500 MB cache.

## How it's built
- **Base**: .NET 10 web API in the existing project, ImageMagick called as a command line program. The project moves to
  `Tyche\Service\Library\` with a test project next to it, following your folder rule. ([T-1](tasks.md#t-1))
- **Version and author**: one build file at the top sets Stefan Pichler as author and takes the version from the build
  command; the service reads it back for `/version`. ([T-1](tasks.md#t-1), [T-9](tasks.md#t-9))
- **Settings**: checked when the service starts, so a wrong folder or missing default stops it with a clear message.
  ([T-2](tasks.md#t-2))
- **Finding pictures**: the folder is scanned (including subfolders) and the list is kept for 10 seconds, so new pictures
  appear quickly without a restart. The client never sends a path, only a collection name. ([T-4](tasks.md#t-4))
- **Checking requests**: all values are turned into numbers or fixed choices first; bad ones get a clear 400.
  ([T-5](tasks.md#t-5))
- **Resizing**: ImageMagick is started directly without a shell, with a time limit, a limit on parallel runs and memory
  limits. Fit shrinks only, fill crops to the exact size. dpi multiplies the size. ([T-6](tasks.md#t-6))
- **Cache**: results are saved on disk under a name made from the source file and all options. Identical requests at the
  same time convert once. Over 500 MB the least recently used files are deleted. ([T-7](tasks.md#t-7))
- **Endpoints**: `/image`, `/version`, `/health` with errors as 404, 400 or a plain 500. ([T-8](tasks.md#t-8),
  [T-9](tasks.md#t-9), [T-10](tasks.md#t-10))
- **Docker**: Alpine image with ImageMagick, non-root, volumes for pictures and cache, healthcheck, labels with author
  and version. ([T-11](tasks.md#t-11))
- **GitHub**: a tag like `v1.0.1` runs the tests inside an Alpine Docker build (same ImageMagick as the shipped image), then pushes the image to GHCR as `v1.0.1.<run>`, `v1.0.1` and `latest`.
  ([T-12](tasks.md#t-12))
- **Tests**: each task has its own tests, plus integration tests that use the real ImageMagick (skipped locally if it is
  not installed, always run on GitHub). ([T-14](tasks.md#t-14))

## Review and testing
The Reviewer checks each group of tasks. At the end the Tester tries to break it (strange parameters, path tricks, huge
or broken pictures, many requests at once) and checks every acceptance criterion.

## Main risks
- Alpine's ImageMagick packages might lack a format; we add the missing package if so.
- Animated webp may not work on Alpine; first frame is the fallback.
- Cache cleanup while requests run is the trickiest part and gets its own tests.
- I read your folder rule as `Tyche\Service\Library`; moving is cheap if you meant otherwise.
