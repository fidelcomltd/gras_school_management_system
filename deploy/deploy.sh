#!/usr/bin/env bash
#
# Deploys an already-built publish output to the VPS. Run ON THE VPS as root, with the release
# unpacked in a staging directory:
#
#   sudo bash deploy.sh /tmp/gras-release
#
# CI (.github/workflows/deploy-production.yml) does exactly this over SSH after building. Running it
# by hand is the same path, which is the point — there is no second, untested procedure for a
# deploy at 9pm when CI is broken.
#
# ORDER MATTERS: back up, install the files into a NEW directory, migrate, and only then swap the
# symlink and restart. A migration that fails leaves the previous version running and serving
# traffic, because nothing it serves has been touched yet.

set -euo pipefail

RELEASE_DIR=${1:-}
APP_DIR=/opt/gras/api
APP_USER=gras
ENV_FILE=/etc/gras/api.env
KEEP_RELEASES=3
RELEASES_DIR=/opt/gras/releases

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }

if [[ $EUID -ne 0 ]]; then
  echo "Run this as root." >&2
  exit 1
fi

if [[ -z "$RELEASE_DIR" || ! -f "$RELEASE_DIR/SchoolManagement.Api.dll" ]]; then
  echo "Usage: deploy.sh <directory containing SchoolManagement.Api.dll>" >&2
  exit 1
fi

if [[ ! -x "$RELEASE_DIR/efbundle" ]]; then
  echo "No efbundle in the release — refusing to deploy a build that cannot migrate." >&2
  exit 1
fi

# The connection string is READ, not SOURCED. `source` would run the file as shell, and
# `Database__ConnectionString=Host=127.0.0.1;Port=5432;...` is, to bash, an assignment of
# "Host=127.0.0.1" followed by commands named `Port=5432` and so on — the value silently truncates
# at the first semicolon and the migration then fails with a useless error. Quotes are stripped the
# same way systemd's EnvironmentFile parser does, so a quoted or unquoted file both work.
read_env_value() {
  local key="$1" value
  value=$(sed -n "s/^${key}=//p" "$ENV_FILE" | head -n 1)
  value=${value%$'\r'}
  if [[ ${value:0:1} == "'" && ${value: -1} == "'" ]] || [[ ${value:0:1} == '"' && ${value: -1} == '"' ]]; then
    value=${value:1:-1}
  fi
  printf '%s' "$value"
}

CONNECTION_STRING=$(read_env_value 'Database__ConnectionString')

if [[ -z "$CONNECTION_STRING" ]]; then
  echo "Database__ConnectionString is not set in $ENV_FILE." >&2
  exit 1
fi

# ── Backup ───────────────────────────────────────────────────────────────────────────────────
log "Backing up the database before migrating"
/usr/local/bin/gras-backup-db --label pre-deploy

# ── Files ────────────────────────────────────────────────────────────────────────────────────
# Installed BEFORE the migration runs, so the migration bundle executes from a root-owned
# directory rather than from the deployer-writable upload directory. Releases are kept as
# timestamped directories, so a rollback is one symlink away and needs no rebuild.
STAMP=$(date -u +%Y%m%d%H%M%S)
TARGET="$RELEASES_DIR/$STAMP"

log "Installing the release into $TARGET"
install -d -o root -g root -m 0755 "$RELEASES_DIR"
install -d -o "$APP_USER" -g "$APP_USER" -m 0755 "$TARGET"
cp -a "$RELEASE_DIR"/. "$TARGET"/
chown -R root:root "$TARGET"
# The app only needs to READ its own files; it writes to /var/lib/gras alone. Root-owned means a
# bug in the app cannot rewrite the code that will run after the next restart.
chmod -R a-w,a+rX "$TARGET"

# ── Database ─────────────────────────────────────────────────────────────────────────────────
log "Applying migrations"
# A migration BUNDLE, not `dotnet ef`: the SDK and the EF tool are not installed on this box, and a
# self-contained bundle is the supported way to apply migrations without them. CI builds it from
# the same commit as the app, so schema and code can never be a version apart.
#
# The connection string goes in through the ENVIRONMENT, never as `--connection`: an argument is
# visible to every user on the box in `ps`. Both names are exported because the bundle resolves its
# context either through DesignTimeDbContextFactory or through the app's own configuration,
# depending on how it was built.
#
# Run as the service account, not as root: the bundle is the one deployer-supplied binary this
# script executes, and it has no business holding root on the database host.
# The bundle is a self-contained single file: it unpacks its runtime on first run, by default under the
# user's home. The service account has none (/nonexistent), so it gets a scratch directory instead,
# removed once the migration has run.
EXTRACT_DIR=/var/lib/gras/bundle-extract
rm -rf "$EXTRACT_DIR"
install -d -o "$APP_USER" -g "$APP_USER" -m 0700 "$EXTRACT_DIR"
chmod a+x "$TARGET/efbundle"
if ! sudo -u "$APP_USER" \
      SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION="$CONNECTION_STRING" \
      Database__ConnectionString="$CONNECTION_STRING" \
      DOTNET_BUNDLE_EXTRACT_BASE_DIR="$EXTRACT_DIR" \
      "$TARGET/efbundle"; then
  echo "FAILED: migrations did not apply. Nothing was swapped — the previous release is still" >&2
  echo "        running and serving traffic. The pre-deploy dump is in /var/backups/gras." >&2
  rm -rf "$TARGET" "$EXTRACT_DIR"
  exit 1
fi

# Not part of the running application, and it embeds a whole EF toolchain.
rm -f "$TARGET/efbundle"
rm -rf "$EXTRACT_DIR"

# ── Swap and restart ─────────────────────────────────────────────────────────────────────────
log "Restarting gras-api"

# /opt/gras/api is a SYMLINK to the live release. On the very first deploy it may not exist at all,
# or (from an older provisioning script) exist as a real directory — `mv -T` refuses to replace a
# directory with a symlink, which is why that case is cleared first rather than discovered halfway
# through a deploy.
if [[ -d "$APP_DIR" && ! -L "$APP_DIR" ]]; then
  log "$APP_DIR is a real directory — replacing it with a release symlink"
  rm -rf "$APP_DIR"
fi

# Two steps so the replacement is atomic: `ln` into a temporary name, then `mv -T` over the live
# one. A plain `ln -sfn` would briefly unlink the path a starting process might be reading.
ln -sfn "$TARGET" "$APP_DIR.new"
mv -Tf "$APP_DIR.new" "$APP_DIR"
systemctl restart gras-api

# ── Smoke test ───────────────────────────────────────────────────────────────────────────────
# /health/ready, not /health/live: ready opens a real database connection, which is what a bad
# migration or a wrong password actually breaks. Direct to Kestrel, bypassing Nginx, so a TLS or
# proxy problem is not misread as the app being down.
log "Smoke-testing http://127.0.0.1:5000/health/ready"
for attempt in $(seq 1 20); do
  if curl --fail --silent --show-error --max-time 5 http://127.0.0.1:5000/health/ready >/dev/null; then
    printf '    ready after %ss\n' "$attempt"
    break
  fi

  if [[ $attempt -eq 20 ]]; then
    echo "FAILED: the service did not become ready. Last 40 log lines:" >&2
    journalctl -u gras-api -n 40 --no-pager >&2
    echo "Roll back with: ln -sfn <previous release in $RELEASES_DIR> $APP_DIR.new &&" >&2
    echo "                mv -Tf $APP_DIR.new $APP_DIR && systemctl restart gras-api" >&2
    exit 1
  fi

  sleep 1
done

# ── Housekeeping ─────────────────────────────────────────────────────────────────────────────
log "Pruning old releases (keeping $KEEP_RELEASES)"
# `tail -n +N` on a reverse-sorted list: everything past the newest KEEP_RELEASES. The live release
# is always the newest, so this can never delete what is running.
find "$RELEASES_DIR" -maxdepth 1 -mindepth 1 -type d | sort -r | tail -n "+$((KEEP_RELEASES + 1))" | while read -r old; do
  printf '    removing %s\n' "$old"
  rm -rf "$old"
done

log "Deployed $STAMP"
