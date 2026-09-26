#!/bin/sh
# The staging image's entrypoint (backend/Dockerfile). Three steps, in order:
#
#   1. Migrate. The connection string reaches the bundle through the environment (both names, as in
#      deploy/deploy.sh), never as an argument. If the database is unreachable the bundle exits non-zero,
#      `set -e` stops here and the app never starts, so a deploy cannot go live without its database.
#   2. Optionally create the first administrator. Render's free plan has no Shell to run bootstrap-admin
#      in, so it runs here when GRAS_BOOTSTRAP_ADMIN_EMAIL and GRAS_BOOTSTRAP_ADMIN_NAME are both set.
#      The command refuses once any account exists (spec 6.1.6), so a later boot with the variables still
#      set only logs that refusal. The temporary password is printed to the service log: it must be
#      changed at first sign-in, and the two variables should be deleted once it has been used.
#   3. Start the app on $PORT (Render sets it), 8080 otherwise. `exec` keeps it as PID 1 so it receives
#      SIGTERM and shuts down gracefully.
set -eu

SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION="$Database__ConnectionString" gras-efbundle

if [ -n "${GRAS_BOOTSTRAP_ADMIN_EMAIL:-}" ] && [ -n "${GRAS_BOOTSTRAP_ADMIN_NAME:-}" ]; then
  echo "GRAS_BOOTSTRAP_ADMIN_EMAIL is set: running bootstrap-admin before start-up."
  # Never fatal: an existing account is the normal answer on every boot after the first.
  dotnet SchoolManagement.Api.dll bootstrap-admin \
    --email="$GRAS_BOOTSTRAP_ADMIN_EMAIL" --staff-name="$GRAS_BOOTSTRAP_ADMIN_NAME" ||
    echo "bootstrap-admin created no account (see above). If one already exists, delete GRAS_BOOTSTRAP_ADMIN_EMAIL and GRAS_BOOTSTRAP_ADMIN_NAME."
fi

exec dotnet SchoolManagement.Api.dll --urls "http://+:${PORT:-8080}"
