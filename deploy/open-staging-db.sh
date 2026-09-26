#!/usr/bin/env bash
#
# Opens the STAGING database on the production VPS to the staging API on Render (DEPLOYMENT.md section 7).
# Run as root, passing Render's outbound addresses for the service's region (Render dashboard -> the
# service -> Connect -> Outbound; a bare address or a CIDR range):
#
#   sudo bash open-staging-db.sh 18.156.158.53 18.156.42.200 52.59.103.54
#
# IDEMPOTENT, and re-run it whenever Render's list changes: the pg_hba block and the firewall rules it
# owns are REPLACED by the addresses given, so an address dropped from the list loses access.
#
# What it does:
#   1. makes sure the gras_staging role and database exist, creating them ONLY if they do not (an
#      existing role keeps its password, an existing database is never touched)
#   2. caps the staging role's connections, so staging cannot starve production of the instance's 60
#   3. makes Postgres listen on this box's public address as well as localhost, with TLS on
#   4. allows ONLY role gras_staging into database gras_staging, ONLY from the given addresses, ONLY over
#      TLS, in pg_hba.conf and in ufw
#   5. prints the connection string to paste into Render
#
# THE ADDRESSES ARE NOT THE BOUNDARY. Render shares its outbound ranges across every service in a region,
# so any Render customer in Frankfurt passes the address check. What actually keeps them out is the
# long random staging password, sent inside TLS with SCRAM channel binding (the printed connection
# string requires it, so a man in the middle presenting its own certificate cannot relay the login).
# The address filter only keeps the rest of the internet off port 5432.
#
# The production database stays unreachable from outside: no pg_hba line names it.
#
# Override the detected public address with VPS_IP=<address> if the box has more than one.

set -euo pipefail

STAGING_DB=gras_staging
STAGING_ROLE=gras_staging
PASSWORD_FILE=/etc/gras/staging-db-password
# Render's zero-downtime deploy keeps the old instance running for at least 60 seconds after the new one
# is healthy, so two pools overlap: 2 x 5 (Maximum Pool Size in the printed connection string), plus the
# new instance's migration connection, plus a psql session. max_connections is 60 (provision-vps.sh) and
# production needs the rest.
STAGING_POOL_SIZE=5
CONNECTION_LIMIT=12
MARK=gras-staging-render

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }
note() { printf '    %s\n' "$1"; }
fail() { printf '\n\033[1;31mFAILED: %s\033[0m\n' "$1" >&2; exit 1; }
psql_q() { sudo -u postgres psql -v ON_ERROR_STOP=1 -tAc "$1"; }

[[ $EUID -eq 0 ]] || fail "run this as root (sudo bash open-staging-db.sh <render-ip> ...)."
[[ $# -gt 0 ]] || fail "pass Render's outbound addresses for your region, e.g. sudo bash open-staging-db.sh 18.156.158.53"

# ── The addresses ────────────────────────────────────────────────────────────────────────────
# IPv4 only, each octet 0-255, optional /0-32. Anything else is refused before a single file changes:
# a typo here would otherwise open 5432 to an address nobody meant.
ADDRESSES=()
for raw in "$@"; do
  if [[ ! $raw =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})(/([0-9]{1,2}))?$ ]]; then
    fail "'$raw' is not an IPv4 address or CIDR range."
  fi
  for octet in "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}" "${BASH_REMATCH[3]}" "${BASH_REMATCH[4]}"; do
    (( 10#$octet <= 255 )) || fail "'$raw' has an octet above 255."
    # Postgres reads a leading zero as octal (053 is 43) while ufw refuses it: two tools, two answers.
    [[ $octet == 0 || $octet != 0* ]] || fail "'$raw' has an octet with a leading zero."
  done
  prefix=${BASH_REMATCH[6]:-32}
  (( 10#$prefix <= 32 )) || fail "'$raw' has a prefix above /32."
  # A wide range would open the database to far more than Render; Render publishes addresses and small ranges.
  (( 10#$prefix >= 16 )) || fail "'$raw' is wider than /16, which is not a Render outbound range."
  ADDRESSES+=("${raw%/*}/$prefix")
done

command -v ufw >/dev/null || fail "ufw is not installed; run provision-vps.sh first."
# With ufw off, the listen address below would put 5432 on the whole internet with pg_hba as the only filter.
ufw status | grep -q '^Status: active' || fail "ufw is not active, so port 5432 would be open to everyone. Enable it (provision-vps.sh does) and re-run."
systemctl is-active --quiet postgresql || fail "PostgreSQL is not running."

VPS_IP=${VPS_IP:-$(ip -4 route get 1.1.1.1 2>/dev/null | sed -n 's/.* src \([0-9.]*\).*/\1/p')}
[[ -n $VPS_IP ]] || fail "could not detect this box's public address; re-run with VPS_IP=<address>."

# ── 1. Role and database: create only what is missing ────────────────────────────────────────
log "Checking for the $STAGING_ROLE role and the $STAGING_DB database"
role_exists=$(psql_q "SELECT 1 FROM pg_roles WHERE rolname='$STAGING_ROLE'")
db_exists=$(psql_q "SELECT 1 FROM pg_database WHERE datname='$STAGING_DB'")

if [[ $role_exists == 1 ]]; then
  note "role $STAGING_ROLE already exists: kept as it is, password unchanged"
  if [[ ! -f $PASSWORD_FILE ]]; then
    fail "role $STAGING_ROLE exists but $PASSWORD_FILE does not, so its password is unknown here. Set one with
        sudo -u postgres psql -c \"ALTER ROLE $STAGING_ROLE PASSWORD '<new password>'\"
        write the same value to $PASSWORD_FILE (mode 0600), then re-run this script."
  fi
else
  if [[ -f $PASSWORD_FILE ]]; then
    note "reusing the password already stored in $PASSWORD_FILE"
  else
    install -d -o root -g root -m 0700 /etc/gras
    # Same shape as provision-vps.sh: no shell- or connection-string-special characters.
    openssl rand -base64 32 | tr -d '\n/+=' > "$PASSWORD_FILE"
    chmod 0600 "$PASSWORD_FILE"
  fi
  sudo -u postgres psql -v ON_ERROR_STOP=1 -q \
    -c "CREATE ROLE $STAGING_ROLE WITH LOGIN PASSWORD '$(cat "$PASSWORD_FILE")';"
  note "created role $STAGING_ROLE"
fi

if [[ $db_exists == 1 ]]; then
  note "database $STAGING_DB already exists: left alone"
else
  sudo -u postgres createdb -O "$STAGING_ROLE" "$STAGING_DB"
  # The same ownership and grants provision-vps.sh gives it. Migrations create tables, so the role owns
  # its schema; nobody else may connect.
  sudo -u postgres psql -v ON_ERROR_STOP=1 -q -d "$STAGING_DB" \
    -c "ALTER SCHEMA public OWNER TO $STAGING_ROLE;" \
    -c "REVOKE ALL ON DATABASE $STAGING_DB FROM PUBLIC;" \
    -c "GRANT ALL ON DATABASE $STAGING_DB TO $STAGING_ROLE;"
  note "created database $STAGING_DB"
fi

# ── 2. Connection cap ────────────────────────────────────────────────────────────────────────
log "Capping $STAGING_ROLE at $CONNECTION_LIMIT connections"
sudo -u postgres psql -v ON_ERROR_STOP=1 -q -c "ALTER ROLE $STAGING_ROLE CONNECTION LIMIT $CONNECTION_LIMIT;"

# ── 3. Listen address and TLS ────────────────────────────────────────────────────────────────
PG_CONF=$(psql_q 'SHOW config_file')
HBA_FILE=$(psql_q 'SHOW hba_file')
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
restart_needed=false
# Kept whatever happens, so a bad edit is one `cp` away from undone.
cp -a "$PG_CONF" "$PG_CONF.bak.$STAMP"

log "Checking TLS"
if [[ $(psql_q 'SHOW ssl') == on ]]; then
  note "ssl is on"
else
  # Ubuntu's package ships a self-signed "snakeoil" pair for exactly this. SSL Mode=Require encrypts
  # without validating the certificate, and channel binding (in the printed connection string) is what
  # stops a substituted certificate, so a self-signed one is enough.
  if ! grep -qE "^\s*ssl_cert_file\s*=" "$PG_CONF"; then
    printf "ssl_cert_file = '/etc/ssl/certs/ssl-cert-snakeoil.pem'\nssl_key_file = '/etc/ssl/private/ssl-cert-snakeoil.key'\n" >> "$PG_CONF"
  fi
  # A certificate the server cannot read is a cluster that does not come back after the restart, and
  # production shares it. Check the files the config will actually use, as the postgres user, first.
  cert=$(sed -n -E "s/^\s*ssl_cert_file\s*=\s*'([^']*)'.*/\1/p" "$PG_CONF" | tail -1)
  key=$(sed -n -E "s/^\s*ssl_key_file\s*=\s*'([^']*)'.*/\1/p" "$PG_CONF" | tail -1)
  data_dir=$(psql_q 'SHOW data_directory')
  [[ $cert == /* ]] || cert="$data_dir/${cert:-server.crt}"
  [[ $key == /* ]] || key="$data_dir/${key:-server.key}"
  if ! sudo -u postgres test -r "$cert" || ! sudo -u postgres test -r "$key"; then
    cp -a "$PG_CONF.bak.$STAMP" "$PG_CONF"
    fail "ssl is off, and the postgres user cannot read the certificate ($cert) or key ($key) the config names.
        Nothing was restarted and $PG_CONF is unchanged. Install ssl-cert (apt-get install ssl-cert) or fix
        those paths, then re-run."
  fi
  sed -i -E "s|^\s*#?\s*ssl\s*=.*|ssl = on|" "$PG_CONF"
  grep -qE '^ssl = on' "$PG_CONF" || printf 'ssl = on\n' >> "$PG_CONF"
  note "turning ssl on with $cert"
  restart_needed=true
fi

log "Checking listen_addresses"
current_listen=$(psql_q 'SHOW listen_addresses')
if [[ $current_listen == '*' || ",$current_listen," == *",$VPS_IP,"* ]]; then
  note "already listening on $VPS_IP ($current_listen)"
else
  wanted="localhost,$VPS_IP"
  if grep -qE "^\s*#?\s*listen_addresses\s*=" "$PG_CONF"; then
    sed -i -E "s|^\s*#?\s*listen_addresses\s*=.*|listen_addresses = '$wanted'|" "$PG_CONF"
  else
    printf "listen_addresses = '%s'\n" "$wanted" >> "$PG_CONF"
  fi
  note "listen_addresses: '$current_listen' -> '$wanted'"
  restart_needed=true
fi

# ── 4a. pg_hba.conf ──────────────────────────────────────────────────────────────────────────
# One block, fenced by markers and rewritten whole on every run. Appended at the end: pg_hba is
# first-match, and nothing above it matches a remote address, so an address outside this block
# simply finds no line and is refused.
log "Writing the Render block in $HBA_FILE"
cp -a "$HBA_FILE" "$HBA_FILE.bak.$STAMP"
sed -i "/^# BEGIN $MARK/,/^# END $MARK/d" "$HBA_FILE"
{
  printf '# BEGIN %s (managed by deploy/open-staging-db.sh; edits here are overwritten)\n' "$MARK"
  for address in "${ADDRESSES[@]}"; do
    printf 'hostssl %s %s %s scram-sha-256\n' "$STAGING_DB" "$STAGING_ROLE" "$address"
  done
  printf '# END %s\n' "$MARK"
} >> "$HBA_FILE"
note "${#ADDRESSES[@]} address(es): ${ADDRESSES[*]}"

# ── 4b. Firewall ─────────────────────────────────────────────────────────────────────────────
# Rules this script added carry the comment "$MARK". Any of those whose address is no longer in the
# list is deleted; the rest are asserted (ufw allow is idempotent).
log "Updating the firewall"
while read -r rule; do
  from=$(sed -n 's/.* from \([0-9./]*\) .*/\1/p' <<<"$rule")
  [[ $from == */* ]] || from="$from/32"
  keep=false
  for address in "${ADDRESSES[@]}"; do [[ $address == "$from" ]] && keep=true; done
  if [[ $keep == false ]]; then
    ufw delete allow from "${from%/32}" to any port 5432 proto tcp >/dev/null
    note "removed the rule for $from"
  fi
done < <(ufw show added | grep -F "comment '$MARK'" || true)
for address in "${ADDRESSES[@]}"; do
  ufw allow from "${address%/32}" to any port 5432 proto tcp comment "$MARK" >/dev/null
done
note "5432/tcp open to ${ADDRESSES[*]} only"

# ── Apply ────────────────────────────────────────────────────────────────────────────────────
if [[ $restart_needed == true ]]; then
  # listen_addresses and ssl need a restart. Production's API drops its connections for a second or
  # two and its pool reconnects on the next request.
  log "Restarting PostgreSQL (listen address or TLS changed)"
  if ! systemctl restart postgresql; then
    fail "PostgreSQL did not come back, and PRODUCTION shares it. Put the previous config back and start it:
        sudo cp -a $PG_CONF.bak.$STAMP $PG_CONF && sudo cp -a $HBA_FILE.bak.$STAMP $HBA_FILE && sudo systemctl restart postgresql
        The reason is in: sudo journalctl -u postgresql -n 50"
  fi
else
  log "Reloading PostgreSQL (pg_hba.conf changed)"
  systemctl reload postgresql
fi

sleep 2
ss -ltn "sport = :5432" | grep -qF "$VPS_IP:5432" || [[ $(psql_q 'SHOW listen_addresses') == '*' ]] ||
  fail "PostgreSQL is not listening on $VPS_IP:5432. Check: sudo journalctl -u postgresql -n 50"

log "Done"
STAGING_DB_PASSWORD=$(cat "$PASSWORD_FILE")
cat <<EOF
    Paste this into Render as Database__ConnectionString (it contains the staging password, so paste
    it there and nowhere else):

    Host=$VPS_IP;Port=5432;Database=$STAGING_DB;Username=$STAGING_ROLE;Password=$STAGING_DB_PASSWORD;SSL Mode=Require;Channel Binding=Require;Maximum Pool Size=$STAGING_POOL_SIZE;GSS Encryption Mode=Disable

    Channel Binding=Require ties the login to this TLS session, so a relayed login fails even though the
    certificate is self-signed. GSS Encryption Mode=Disable: the container has no Kerberos library, so
    Npgsql's default GSS attempt only logs a load error on every new connection.

    If Render's outbound addresses change, re-run this script with the new list.
EOF
