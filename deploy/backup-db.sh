#!/usr/bin/env bash
#
# Backs up the production database. Installed as /usr/local/bin/gras-backup-db and run nightly by
# gras-backup.timer; deploy.sh also calls it with --label pre-deploy before migrating.
#
#   gras-backup-db [--label <name>]
#
# WHAT A RESTORE NEEDS, AND WHAT THIS DOES NOT HOLD: the dump alone is not enough to bring the
# system back. Pins__LookupKey and Pins__EncryptionKey live in /etc/gras/api.env, and without the
# lookup key every pin ever printed is dead — restoring the database will not undo that. The keys
# belong in the school's password manager, and the env file is included in the offsite copy below
# for exactly this reason.

set -euo pipefail

LABEL=nightly
if [[ ${1:-} == "--label" && -n ${2:-} ]]; then
  LABEL=$2
fi

BACKUP_DIR=/var/backups/gras
RETAIN_DAYS=14
DB=gras_prod
STAMP=$(date -u +%Y%m%d-%H%M%S)
ARCHIVE="$BACKUP_DIR/$DB-$LABEL-$STAMP.dump"

install -d -o root -g root -m 0700 "$BACKUP_DIR"

# --format=custom, not plain SQL: it restores with pg_restore, compresses, and allows a single-table
# restore, which is what is actually wanted when one import went wrong.
#
# The dump goes to STDOUT and this script's own redirection writes the file. `pg_dump --file=` would
# be the postgres user opening the path, and /var/backups/gras is root-only 0700 — so that form
# fails with "permission denied" every night, and takes every deploy down with it (deploy.sh calls
# this first). Redirecting means the file is created by root, which can write there.
umask 077
sudo -u postgres pg_dump --format=custom "$DB" > "$ARCHIVE"
chmod 0600 "$ARCHIVE"

printf 'wrote %s (%s)\n' "$ARCHIVE" "$(du -h "$ARCHIVE" | cut -f1)"

# A dump that is silently empty is worse than no dump, because it looks like a backup. pg_restore
# --list on the archive is the cheapest real check that the file is a readable dump with content.
# Read back as root, not as postgres: the archive is mode 0600 and owned by root.
if ! pg_restore --list "$ARCHIVE" | grep -q 'TABLE DATA'; then
  echo "FAILED: $ARCHIVE contains no table data. Not pruning old backups." >&2
  exit 1
fi

# ── Offsite ──────────────────────────────────────────────────────────────────────────────────
# A backup on the same disk as the database is not a backup: it survives a bad migration, and
# nothing else. The destination is a human decision (open question — see DEPLOYMENT.md), so this
# hook stays inert until /etc/gras/offsite.sh exists. It receives the archive path as $1.
if [[ -x /etc/gras/offsite.sh ]]; then
  /etc/gras/offsite.sh "$ARCHIVE"
else
  echo "NOTE: no /etc/gras/offsite.sh — this backup exists only on this box." >&2
fi

find "$BACKUP_DIR" -maxdepth 1 -name "$DB-*.dump" -mtime "+$RETAIN_DAYS" -print -delete
