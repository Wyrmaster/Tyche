# Random image API (readable)

[spec](spec.md) · [spec (readable)](spec-humanreadable.md)

Status: In progress

A small service on your home network that hands out a random picture from folders you choose, so a Homarr dashboard
background changes on every load. It can shrink the picture to the right size first.

## What changes for you

### [FR-1](spec.md#fr-1-random-image-endpoint) Random image
Open `/image` and you get one random picture. Every load gives a new one.

- [x] [AC-1.1](spec.md#ac-1-1) It returns a real picture
- [x] [AC-1.2](spec.md#ac-1-2) Different calls give different pictures
- [x] [AC-1.3](spec.md#ac-1-3) Browsers are told not to keep it
- [x] [AC-1.4](spec.md#ac-1-4) Without options the original file comes back untouched
- [x] [AC-1.5](spec.md#ac-1-5) An empty folder gives a clear "not found"

### [FR-2](spec.md#fr-2-collections-in-appsettingsjson) Folders in the settings
In `appsettings.json` you name folders (e.g. `nature`, `city`) and pick a default. `/image?collection=nature` uses that folder.

- [x] [AC-2.1](spec.md#ac-2-1) The named folder is used
- [x] [AC-2.2](spec.md#ac-2-2) No name means the default folder
- [x] [AC-2.3](spec.md#ac-2-3) Unknown name gives "not found"
- [x] [AC-2.4](spec.md#ac-2-4) Subfolders count, other file types are ignored
- [x] [AC-2.5](spec.md#ac-2-5) New or removed pictures show up without a restart
- [x] [AC-2.6](spec.md#ac-2-6) Wrong settings give a clear message
- [x] [AC-2.7](spec.md#ac-2-7) Nobody can read files outside the folders

### [FR-3](spec.md#fr-3-resize) Resize
`width` and `height` set the size, `dpi` is a scale (192 = twice as many pixels as 96), `mode` is `fit` (keeps the shape,
default) or `fill` (exact size, crops the edges).

- [x] [AC-3.1](spec.md#ac-3-1) Fit keeps the shape inside the box
- [x] [AC-3.2](spec.md#ac-3-2) One size alone also keeps the shape
- [x] [AC-3.3](spec.md#ac-3-3) Fill gives exactly the size
- [x] [AC-3.4](spec.md#ac-3-4) dpi=192 doubles the pixels
- [x] [AC-3.5](spec.md#ac-3-5) Fit never enlarges a small picture
- [x] [AC-3.6](spec.md#ac-3-6) Bad or too large values give a clear error
- [x] [AC-3.7](spec.md#ac-3-7) Animated gifs don't break

### [FR-4](spec.md#fr-4-format-conversion) Format conversion
`format=webp` (or jpg, png, gif; `jpeg` works as alias of jpg) converts the picture. Without it the original format stays.

- [x] [AC-4.1](spec.md#ac-4-1) Converted picture has the right type
- [x] [AC-4.2](spec.md#ac-4-2) No `format` keeps the original format
- [x] [AC-4.3](spec.md#ac-4-3) Unknown format gives an error
- [x] [AC-4.4](spec.md#ac-4-4) Format and resize work together

### [FR-5](spec.md#fr-5-cache-of-converted-images) Cache
Resized and converted pictures are saved so the same request is answered fast the next time.

- [x] [AC-5.1](spec.md#ac-5-1) Same request is served from the cache
- [x] [AC-5.2](spec.md#ac-5-2) A changed picture is not served stale
- [x] [AC-5.3](spec.md#ac-5-3) Cache folder can be a Docker volume
- [x] [AC-5.4](spec.md#ac-5-4) Size limit (500 MB), oldest entries are removed first
- [x] [AC-5.5](spec.md#ac-5-5) Simultaneous identical requests are safe
- [x] [AC-5.6](spec.md#ac-5-6) A failed conversion leaves nothing broken behind

### [FR-6](spec.md#fr-6-version-and-health-endpoints) Version and health
`/version` shows e.g. `v1.1.5`. `/health` says whether the service and ImageMagick work.

- [x] [AC-6.1](spec.md#ac-6-1) `/version` shows the 3-part version
- [x] [AC-6.2](spec.md#ac-6-2) `/health` is OK or not OK honestly
- [x] [AC-6.3](spec.md#ac-6-3) Docker uses it as healthcheck

### [FR-7](spec.md#fr-7-docker-image) Docker image
Small Alpine image with ImageMagick inside, not running as root.

- [x] [AC-7.1](spec.md#ac-7-1) Alpine based with ImageMagick
- [x] [AC-7.2](spec.md#ac-7-2) Non-root and can read/write the mounted folders
- [x] [AC-7.3](spec.md#ac-7-3) Folders as volumes, settings via environment variables
- [x] [AC-7.4](spec.md#ac-7-4) Working `compose.yaml` (productive) and `compose.dev.yaml` (development) examples
- [x] [AC-7.5](spec.md#ac-7-5) Labels with author and version

### [FR-8](spec.md#fr-8-github-build-on-version-tags) GitHub build
Push a tag like `v1.0.1` and GitHub builds and publishes the image.

- [ ] [AC-8.1](spec.md#ac-8-1) Only version tags publish
- [x] [AC-8.2](spec.md#ac-8-2) Build number is the GitHub run number (`v1.0.1.7`)
- [ ] [AC-8.3](spec.md#ac-8-3) Pushed to GHCR as `v1.0.1.7`, `v1.0.1` and `latest`
- [ ] [AC-8.4](spec.md#ac-8-4) Failing tests stop the release
- [x] [AC-8.5](spec.md#ac-8-5) Releasing needs no code change

### [FR-9](spec.md#fr-9-versioning-and-author-in-the-project) Version and author
Version comes from the git tag, with the build number as 4th part. Author is Stefan Pichler.

- [x] [AC-9.1](spec.md#ac-9-1) Build carries the 4-part version
- [x] [AC-9.2](spec.md#ac-9-2) Local builds get a clear dev version
- [x] [AC-9.3](spec.md#ac-9-3) Author named in project file and README

## Not part of it
- Login or HTTPS
- Uploading or deleting pictures through the API
- A web page or gallery
- Image libraries (ImageMagick command line only)
- Formats other than jpg, png, webp, gif
- A limit on waiting requests or on how many different sizes can be cached (accepted risk on a home network)
- Blocking symlinks inside an image folder that point elsewhere (you control the folders)
- Speeding up the cache clean-up scan (fine at the default size)

## Open questions
Answered in the [plan](plan-humanreadable.md): gifs keep their animation where possible, the "don't keep this" header is
always on, the limits are settings with the proposed defaults.
