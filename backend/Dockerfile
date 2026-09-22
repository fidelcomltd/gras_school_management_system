# The STAGING image (Render). Production runs the same publish output natively under systemd on the
# VPS — see docs/DEPLOYMENT.md for why: one 2 GB box running Postgres as well has no room to spare
# for a container runtime, and there is nothing to orchestrate.
#
# Build context is the `backend/` directory.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying the rest, so a source-only change reuses the restore layer. global.json
# pins the SDK feature band; Directory.Packages.props is the only file holding package versions.
COPY global.json nuget.config Directory.Build.props Directory.Build.targets Directory.Packages.props SchoolManagement.slnx ./
COPY src/SchoolManagement.Api/SchoolManagement.Api.csproj src/SchoolManagement.Api/
COPY src/SchoolManagement.Application/SchoolManagement.Application.csproj src/SchoolManagement.Application/
COPY src/SchoolManagement.Domain/SchoolManagement.Domain.csproj src/SchoolManagement.Domain/
COPY src/SchoolManagement.Infrastructure/SchoolManagement.Infrastructure.csproj src/SchoolManagement.Infrastructure/
RUN dotnet restore src/SchoolManagement.Api/SchoolManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/SchoolManagement.Api/SchoolManagement.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root. The app writes only to the cache and key-ring directories below, both of which it owns.
RUN useradd --create-home --shell /usr/sbin/nologin gras \
    && mkdir -p /var/lib/gras/result-pdf-cache /var/lib/gras/dataprotection-keys \
    && chown -R gras:gras /var/lib/gras

COPY --from=build --chown=gras:gras /app/publish ./
USER gras

# Render routes to $PORT and terminates TLS at its edge, so Kestrel serves plain HTTP here.
ENV ASPNETCORE_ENVIRONMENT=Staging \
    DOTNET_gcServer=0

EXPOSE 8080

# Binds $PORT when the platform sets one (Render does), 8080 otherwise. A shell form is needed
# because the exec form does not expand variables; `exec` keeps the app as PID 1 so it receives
# SIGTERM and shuts down gracefully.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet SchoolManagement.Api.dll --urls http://+:${PORT:-8080}"]
