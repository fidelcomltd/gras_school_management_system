# The STAGING image (Render). Production runs the same publish output natively under systemd on the
# VPS — see docs/DEPLOYMENT.md for why: one 2 GB box running Postgres as well has no room to spare
# for a container runtime, and there is nothing to orchestrate.
#
# Build context is the `backend/` directory.

# PINNED to a 10.0.1xx SDK because global.json asks for 10.0.100 with rollForward=latestPatch, i.e. the
# 1xx feature band only (the band CI installs). The floating `sdk:10.0` tag follows the newest band
# (10.0.4xx by 2026-09), which global.json refuses, and MCR stopped publishing 1xx images at 10.0.103.
# Only the build stage is pinned: the runtime stage below stays on the floating, patched aspnet:10.0.
FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src

# Restore before copying the rest, so a source-only change reuses the restore layer. global.json
# pins the SDK feature band; Directory.Packages.props is the only file holding package versions.
COPY global.json nuget.config Directory.Build.props Directory.Build.targets Directory.Packages.props SchoolManagement.slnx dotnet-tools.json ./
COPY src/SchoolManagement.Api/SchoolManagement.Api.csproj src/SchoolManagement.Api/
COPY src/SchoolManagement.Application/SchoolManagement.Application.csproj src/SchoolManagement.Application/
COPY src/SchoolManagement.Domain/SchoolManagement.Domain.csproj src/SchoolManagement.Domain/
COPY src/SchoolManagement.Infrastructure/SchoolManagement.Infrastructure.csproj src/SchoolManagement.Infrastructure/
# For linux-x64 only: without a runtime identifier the publish output carries native libraries for every
# platform (about 350 MB of Windows and ARM builds of SkiaSharp and friends this container never loads).
RUN dotnet restore src/SchoolManagement.Api/SchoolManagement.Api.csproj -r linux-x64

# The analyzer severities (warnings are errors in Release). Without it the build applies the SDK's defaults
# and fails on rules this codebase deliberately turns off, such as CA1034 for the privilege register.
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/SchoolManagement.Api/SchoolManagement.Api.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    --self-contained false \
    --no-restore \
    /p:UseAppHost=false

# The migration bundle, built from the same source as the app so schema and code can never be a version
# apart (see deploy-production.yml for why the startup project is Infrastructure and why a placeholder
# connection string is enough at build time). Framework-dependent, unlike production's: the runtime stage
# already carries .NET, so a self-contained bundle would only add a second copy of it to the image.
RUN dotnet tool restore \
    && SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION='Host=localhost;Database=unused;Username=unused;Password=unused' \
       dotnet tool run dotnet-ef migrations bundle \
         --project src/SchoolManagement.Infrastructure \
         --startup-project src/SchoolManagement.Infrastructure \
         --configuration Release \
         --output /app/efbundle \
         --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root. The app writes only to the cache and key-ring directories below, both of which it owns.
RUN useradd --create-home --shell /usr/sbin/nologin gras \
    && mkdir -p /var/lib/gras/result-pdf-cache /var/lib/gras/dataprotection-keys \
    && chown -R gras:gras /var/lib/gras

COPY --from=build --chown=gras:gras /app/publish ./
COPY --from=build --chown=gras:gras /app/efbundle /usr/local/bin/gras-efbundle
USER gras

# Render routes to $PORT and terminates TLS at its edge, so Kestrel serves plain HTTP here.
ENV ASPNETCORE_ENVIRONMENT=Staging \
    DOTNET_gcServer=0

EXPOSE 8080

# Staging migrates on every boot, before the app starts: a staging database holds nothing that needs the
# production deploy's backup-then-migrate ceremony. Not Render's pre-deploy command: that does not exist
# on the free plan, and Render does not document how it treats an image's ENTRYPOINT. On a boot with
# nothing pending the bundle is a quick no-op. The steps, and the optional first-administrator bootstrap
# the free plan needs in place of a Shell, are in docker-entrypoint.sh. Run through `sh` so the script
# needs no executable bit, which a Windows checkout cannot carry.
COPY --chown=root:root docker-entrypoint.sh /usr/local/bin/gras-entrypoint
ENTRYPOINT ["/bin/sh", "/usr/local/bin/gras-entrypoint"]
