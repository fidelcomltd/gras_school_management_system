#!/usr/bin/env bash
#
# One-time provisioning of the production VPS (Ubuntu 24.04 LTS). Run as root:
#
#   sudo bash provision-vps.sh
#
# IDEMPOTENT: safe to re-run. It never overwrites an existing secret, never drops a database, and
# prints what it skipped. Deploys are a separate script (deploy.sh) run by CI.
#
# What it does NOT do, on purpose:
#   - issue TLS certificates (certbot needs DNS to be pointing here first — see DEPLOYMENT.md)
#   - create the first admin account (a separate, deliberate command that prints a password)
#   - open the staging database to Render (a decision with its own section in DEPLOYMENT.md)

set -euo pipefail

APP_USER=gras
APP_DIR=/opt/gras/api
STATE_DIR=/var/lib/gras
ENV_FILE=/etc/gras/api.env
BACKUP_DIR=/var/backups/gras
PROD_DB=gras_prod
STAGING_DB=gras_staging
PROD_ROLE=gras_prod
STAGING_ROLE=gras_staging

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }
note() { printf '    %s\n' "$1"; }

if [[ $EUID -ne 0 ]]; then
  echo "Run this as root (sudo bash provision-vps.sh)." >&2
  exit 1
fi

# ── Packages ─────────────────────────────────────────────────────────────────────────────────
log "Installing packages"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq \
  postgresql postgresql-contrib \
  nginx certbot python3-certbot-nginx \
  ufw fail2ban unattended-upgrades \
  curl ca-certificates gnupg

# The ASP.NET Core runtime only — the build happens in CI, not on a 2 GB box shared with Postgres.
if ! command -v dotnet >/dev/null 2>&1; then
  log "Installing the .NET 10 ASP.NET Core runtime"
  # Ubuntu's own feed carries .NET since 24.04, which keeps runtime updates on the normal
  # `apt upgrade` path instead of a second, hand-maintained Microsoft repository.
  apt-get install -y -qq aspnetcore-runtime-10.0
else
  note "dotnet already present: $(dotnet --list-runtimes | tr '\n' ' ')"
fi

# ── Service account and directories ──────────────────────────────────────────────────────────
log "Creating the service account and directories"
if ! id -u "$APP_USER" >/dev/null 2>&1; then
  # No login shell and no home: this account exists to own a process and two directories.
  adduser --system --group --no-create-home --shell /usr/sbin/nologin "$APP_USER"
else
  note "user $APP_USER already exists"
fi

install -d -o "$APP_USER" -g "$APP_USER" -m 0755 "$APP_DIR"
install -d -o "$APP_USER" -g "$APP_USER" -m 0755 "$STATE_DIR"

# 0700: the Data Protection key ring is plain XML (there is no OS key store to wrap it with on
# Linux), so the directory permissions ARE the protection.
install -d -o "$APP_USER" -g "$APP_USER" -m 0700 "$STATE_DIR/dataprotection-keys"
install -d -o "$APP_USER" -g "$APP_USER" -m 0700 "$STATE_DIR/result-pdf-cache"
install -d -o root -g root -m 0700 /etc/gras
install -d -o root -g root -m 0700 "$BACKUP_DIR"

# ── PostgreSQL ───────────────────────────────────────────────────────────────────────────────
# ONE instance, TWO databases, TWO roles. Separate roles are the point: it is what closes the
# "staging and dev-test databases share one role and password" drift item, so a staging credential
# leak cannot read a single real pupil's marks.
log "Creating databases and roles"
systemctl enable --now postgresql

create_role_and_db() {
  local role="$1" db="$2" password_file="$3"

  if [[ -f "$password_file" ]]; then
    note "password file $password_file exists — keeping the current password"
  else
    # 32 bytes of base64, no shell-special characters to escape into a connection string.
    openssl rand -base64 32 | tr -d '\n/+=' > "$password_file"
    chmod 0600 "$password_file"
  fi

  local password
  password=$(cat "$password_file")

  if sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='$role'" | grep -q 1; then
    note "role $role exists — resetting its password to the stored one"
    sudo -u postgres psql -qc "ALTER ROLE $role WITH LOGIN PASSWORD '$password';"
  else
    sudo -u postgres psql -qc "CREATE ROLE $role WITH LOGIN PASSWORD '$password';"
  fi

  if sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='$db'" | grep -q 1; then
    note "database $db already exists — left alone"
  else
    sudo -u postgres createdb -O "$role" "$db"
  fi

  # The role owns its own schema and nothing outside it. Migrations create tables, so it needs
  # CREATE on the schema; it has no business reading the other environment's database.
  sudo -u postgres psql -q -d "$db" -c "ALTER SCHEMA public OWNER TO $role;"
  sudo -u postgres psql -q -d "$db" -c "REVOKE ALL ON DATABASE $db FROM PUBLIC;"
  sudo -u postgres psql -q -d "$db" -c "GRANT ALL ON DATABASE $db TO $role;"
}

create_role_and_db "$PROD_ROLE" "$PROD_DB" /etc/gras/prod-db-password
create_role_and_db "$STAGING_ROLE" "$STAGING_DB" /etc/gras/staging-db-password

# Modest tuning for a 2 GB box that also runs the app. Defaults assume a dedicated database server.
PG_CONF=$(sudo -u postgres psql -tAc 'SHOW config_file')
log "Tuning $PG_CONF for a small shared box"
set_pg() {
  local key="$1" value="$2"
  if grep -qE "^\s*#?\s*$key\s*=" "$PG_CONF"; then
    sed -i -E "s|^\s*#?\s*$key\s*=.*|$key = $value|" "$PG_CONF"
  else
    printf '%s = %s\n' "$key" "$value" >> "$PG_CONF"
  fi
}
set_pg shared_buffers 256MB
set_pg effective_cache_size 768MB
set_pg work_mem 8MB
set_pg maintenance_work_mem 64MB
set_pg max_connections 60
systemctl restart postgresql

# ── Application secrets ──────────────────────────────────────────────────────────────────────
# THE PIN KEYS ARE GENERATED HERE, ONCE, AND NEVER REGENERATED.
#
#   Pins__LookupKey     is the HMAC key that turns a parent's pin into the lookup_key column used
#                       to FIND it. Change or lose it and every pin ever printed stops working —
#                       and it is not in the database, so a database backup does not save you.
#   Pins__EncryptionKey decrypts the stored copy used when staff REPRINT a pin. Lose it and
#                       reprints break; parents' pins keep working.
#
# Both must be copied into the school's password manager the day they are made.
log "Writing $ENV_FILE"
if [[ -f "$ENV_FILE" ]]; then
  note "$ENV_FILE exists — NOT touching it. Existing keys are kept."
  note "Add any missing setting by hand; the template is in backend/docs/DEPLOYMENT.md."
else
  PROD_DB_PASSWORD=$(cat /etc/gras/prod-db-password)
  LOOKUP_KEY=$(openssl rand -base64 32)
  ENCRYPTION_KEY=$(openssl rand -base64 32)

  cat > "$ENV_FILE" <<EOF
# GRAS production secrets. Root-only, mode 0600, never in git.
# Generated by provision-vps.sh on $(date -u +%Y-%m-%dT%H:%M:%SZ).

Database__ConnectionString=Host=127.0.0.1;Port=5432;Database=$PROD_DB;Username=$PROD_ROLE;Password=$PROD_DB_PASSWORD

# Spec 6.8.6. NEVER change these two once pins have been printed. Back them up offline.
Pins__LookupKey=$LOOKUP_KEY
Pins__EncryptionKey=$ENCRYPTION_KEY

# Cloudinary (TASK-0005b stage D). FILL THESE IN before the first logo upload — the service
# refuses to start with them missing.
Cloudinary__CloudName=
Cloudinary__ApiKey=
Cloudinary__ApiSecret=
EOF
  chmod 0600 "$ENV_FILE"
  chown root:root "$ENV_FILE"

  cat <<EOF

    ╔══════════════════════════════════════════════════════════════════════════════════╗
    ║  COPY THESE TWO KEYS INTO THE PASSWORD MANAGER NOW. They are shown once.         ║
    ║  Losing Pins__LookupKey kills every pin ever printed, and no database backup      ║
    ║  can bring it back.                                                              ║
    ╚══════════════════════════════════════════════════════════════════════════════════╝

    Pins__LookupKey=$LOOKUP_KEY
    Pins__EncryptionKey=$ENCRYPTION_KEY

    Also store the database password from /etc/gras/prod-db-password.

EOF
fi

# ── Firewall ─────────────────────────────────────────────────────────────────────────────────
# Postgres (5432) is deliberately NOT opened here. Staging on Render needs it, and that is a
# separate, deliberate step in DEPLOYMENT.md with its own allow-list.
log "Configuring the firewall"
ufw --force reset >/dev/null
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable
ufw status verbose

# ── Nginx and the service unit ───────────────────────────────────────────────────────────────
log "Installing the Nginx sites and the systemd unit"
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
install -m 0644 "$SCRIPT_DIR/nginx/gras-api.conf" /etc/nginx/sites-available/gras-api.conf
install -m 0644 "$SCRIPT_DIR/nginx/gras-portal.conf" /etc/nginx/sites-available/gras-portal.conf
ln -sf /etc/nginx/sites-available/gras-api.conf /etc/nginx/sites-enabled/gras-api.conf
ln -sf /etc/nginx/sites-available/gras-portal.conf /etc/nginx/sites-enabled/gras-portal.conf
rm -f /etc/nginx/sites-enabled/default

install -m 0644 "$SCRIPT_DIR/systemd/gras-api.service" /etc/systemd/system/gras-api.service
install -m 0755 "$SCRIPT_DIR/backup-db.sh" /usr/local/bin/gras-backup-db

# The deploy script lives at a fixed path because that path is what the sudoers rule names: CI is
# allowed to run THIS one command as root and nothing else. A script CI could overwrite and then
# run as root would be a root shell with extra steps, so /usr/local/bin/gras-deploy is root-owned
# and CI only ever writes to /tmp/gras-release.
install -m 0755 -o root -g root "$SCRIPT_DIR/deploy.sh" /usr/local/bin/gras-deploy
install -m 0644 "$SCRIPT_DIR/systemd/gras-backup.service" /etc/systemd/system/gras-backup.service
install -m 0644 "$SCRIPT_DIR/systemd/gras-backup.timer" /etc/systemd/system/gras-backup.timer
systemctl daemon-reload
systemctl enable gras-api.service >/dev/null
systemctl enable --now gras-backup.timer >/dev/null

# Nginx is NOT reloaded yet: the sites reference certificates certbot has not issued, so
# `nginx -t` fails until TLS is set up. DEPLOYMENT.md's next step is certbot.
log "Done"
cat <<'EOF'
    Next, in this order (backend/docs/DEPLOYMENT.md has the detail):

      1. Point DNS at this box:  api.<domain> and results.<domain>  (A records)
      2. sudo certbot --nginx -d api.<domain> -d results.<domain>
      3. Fill in the Cloudinary keys in /etc/gras/api.env
      4. Deploy the app (CI does this on a merge to main, or run deploy.sh by hand)
      5. Create the first admin:
           sudo -u gras dotnet /opt/gras/api/SchoolManagement.Api.dll bootstrap-admin ...
EOF
