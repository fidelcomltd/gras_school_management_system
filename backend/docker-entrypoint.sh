#!/bin/sh
# The staging image's entrypoint (backend/Dockerfile). Three steps, in order:
#
#   1. Migrate. The connection string reaches the bundle through the environment (both names, as in
#      deploy/deploy.sh), never as an argument. If the database is unreachable the bundle exits non-zero,
#      `set -e` stops here and the app never starts, so a deploy cannot go live without its database.
#   2. Optionally create the first administrator. Render's free plan has no Shell to run bootstrap-admin
#      in, so it runs here when GRAS_BOOTSTRAP_ADMIN_EMAIL and GRAS_BOOTSTRAP_ADMIN_NAME are both set.
#      The temporary password is printed to the service log, so sign in and change it straight away,
#      then delete both variables: while they are set, every boot (and on the free plan every wake from
#      sleep) pays for an extra .NET start-up just to be refused, and an emptied database would get a
#      new administrator nobody asked for.
#   3. Start the app on $PORT (Render sets it), 8080 otherwise. `exec` keeps it as PID 1 so it receives
#      SIGTERM and shuts down gracefully.
set -eu

SCHOOLMANAGEMENT_DESIGNTIME_CONNECTION="$Database__ConnectionString" gras-efbundle

email=${GRAS_BOOTSTRAP_ADMIN_EMAIL:-}
name=${GRAS_BOOTSTRAP_ADMIN_NAME:-}
if [ -n "$email" ] && [ -n "$name" ]; then
  echo "GRAS_BOOTSTRAP_ADMIN_EMAIL is set: running bootstrap-admin before start-up."
  # The CLI exits 1 both for its normal refusal (an account already exists) and for a real failure, so
  # the two are told apart by its message. Only the refusal lets the app start: a bad name, a bad email
  # or a database error fails the boot, loudly, rather than going live with no administrator.
  if output=$(dotnet SchoolManagement.Api.dll bootstrap-admin --email="$email" --staff-name="$name" 2>&1); then
    printf '%s\n' "$output"
  else
    printf '%s\n' "$output"
    case $output in
      *"already exists"*)
        echo "WARNING: an administrator already exists. Delete GRAS_BOOTSTRAP_ADMIN_EMAIL and GRAS_BOOTSTRAP_ADMIN_NAME"
        echo "         in the Render dashboard: every boot pays for this check while they are set." ;;
      *)
        echo "FAILED: bootstrap-admin created no account (reason above). Fix the variables and redeploy." >&2
        exit 1 ;;
    esac
  fi
elif [ -n "$email" ] || [ -n "$name" ]; then
  echo "WARNING: only one of GRAS_BOOTSTRAP_ADMIN_EMAIL and GRAS_BOOTSTRAP_ADMIN_NAME is set, so no administrator is created."
fi

exec dotnet SchoolManagement.Api.dll --urls "http://+:${PORT:-8080}"
