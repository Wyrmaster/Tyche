# Build context is the repository root:
#   docker build -f Tyche/Service/Library/Dockerfile --build-arg VERSION=1.0.1 --build-arg BUILD_NUMBER=7 -t tyche.service .

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG VERSION=0.0.0
ARG BUILD_NUMBER=0
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Tyche/Service/Library/Tyche.Service.csproj", "Tyche/Service/Library/"]
RUN dotnet restore "Tyche/Service/Library/Tyche.Service.csproj"
COPY ["Tyche/Service/Library/", "Tyche/Service/Library/"]
RUN dotnet publish "Tyche/Service/Library/Tyche.Service.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:Version=$VERSION.$BUILD_NUMBER \
    -p:InformationalVersion=v$VERSION.$BUILD_NUMBER

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS test
# Runs the unit and integration tests against the same ImageMagick packages the final image uses:
#   docker build -f Tyche/Service/Library/Dockerfile --target test .
RUN apk add --no-cache imagemagick imagemagick-jpeg imagemagick-webp
ENV REQUIRE_MAGICK=1
WORKDIR /src
COPY ["Tyche.slnx", "Directory.Build.props", "./"]
COPY ["Tyche/Service/Library/Tyche.Service.csproj", "Tyche/Service/Library/"]
COPY ["Tyche/Service/Test/Tyche.Service.Test.csproj", "Tyche/Service/Test/"]
RUN dotnet restore "Tyche/Service/Test/Tyche.Service.Test.csproj"
COPY ["Tyche/Service/Library/", "Tyche/Service/Library/"]
COPY ["Tyche/Service/Test/", "Tyche/Service/Test/"]
RUN dotnet test "Tyche/Service/Test/Tyche.Service.Test.csproj" --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG VERSION=0.0.0
ARG BUILD_NUMBER=0
LABEL org.opencontainers.image.title="Tyche" \
      org.opencontainers.image.description="Serves a random image from configured folders, optionally resized" \
      org.opencontainers.image.authors="Stefan Pichler" \
      org.opencontainers.image.version="v$VERSION.$BUILD_NUMBER" \
      org.opencontainers.image.source="https://github.com/Wyrmaster/Tyche"

# ImageMagick 7 with the delegates for jpg and webp (png and gif are built in).
RUN apk add --no-cache imagemagick imagemagick-jpeg imagemagick-webp \
    && mkdir -p /cache /images \
    && chown app:app /cache

ENV ASPNETCORE_URLS=http://+:8080 \
    ImageApi__CachePath=/cache

WORKDIR /app
COPY --from=build /app/publish .

USER app
VOLUME ["/cache"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Tyche.Service.dll"]
