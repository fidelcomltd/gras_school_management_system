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
# ORDER MATTERS: migrate first, then swap the files, then restart. A migration that fails leaves
# the previous version running and serving traffic.

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

# shellcheck disable=SC1090
set -a; source "$ENV_FILE"; set +a

if [[ -z "${Database__ConnectionString:-}" ]]; then
  echo "Database__ConnectionString is not set in $ENV_FILE." >&2
  exit 1
fi

# ── Database ─────────────────────────────────────────────────────────────────────────────────
log "Backing up the database before migrating"
/usr/local/bin/gras-backup-db --label pre-deploy

if [[ -x "$RELEASE_DIR/efbundle" ]]; then
  # A migration BUNDLE, not `dotnet ef`: the SDK and the EF tool are not installed on this box, and
  # a self-contained bundle is the supported way to apply migrations without them. CI builds it
  # from the same commit as the app, so the two can never be a version apart.
  log "Applying migrations"
  "$RELEASE_DIR/efbundle" --connection "$Database__ConnectionString"
else
  echo "No efbundle in the release — refusing to deploy a build that cannot migrate." >&2
  exit 1
fi

# ── Files ────────────────────────────────────────────────────────────────────────────────────
# Kept as timestamped directories with a symlink swap, so a rollback is one `ln -sfn` away and does
# not need CI, the network, or a rebuild.
STAMP=$(date -u +%Y%m%d%H%M%S)
TARGET="$RELEASES_DIR/$STAMP"

log "Installing the release into $TARGET"
install -d -o "$APP_USER" -g "$APP_USER" -m 0755 "$TARGET"
cp -a "$RELEASE_DIR"/. "$TARGET"/
rm -f "$TARGET/efbundle"
chown -R "$APP_USER:$APP_USER" "$TARGET"

log "Restarting gras-api"
# The symlink swap and the restart are adjacent on purpose: between them the unit's WorkingDirectory
# and the files it will load disagree, and the window should be as short as possible.
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
    echo "Roll back with: ln -sfn <previous release in $RELEASES_DIR> $APP_DIR && systemctl restart gras-api" >&2
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
