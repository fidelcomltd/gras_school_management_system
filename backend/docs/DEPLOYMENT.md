# Deployment runbook

Written for: whoever is standing up or operating this system — the maintainer, not an agent.

Three environments, three hosts:

| | Production | Staging | Local |
|---|---|---|---|
| API + parent portal | VPS (Namecheap Pulsar), systemd + Nginx | Render, from `backend/Dockerfile` | `dotnet run` |
| Database | PostgreSQL on the same VPS, `gras_prod` | PostgreSQL on the same VPS, `gras_staging` | Testcontainers / local |
| Admin web app | Netlify | Netlify | Vite dev server |
| Uploaded files | Cloudinary, `gras/prod` | Cloudinary, `gras/staging` | in process memory |

The API and the parent portal are **one process on two hostnames** (human ruling, 2026-09-22). There
is nothing separate to deploy for the portal.

---

## 0. If you are moving DNS to Netlify: copy the records FIRST

Netlify DNS means delegating the whole domain by changing nameservers at Namecheap, and the new
nameservers answer for **everything** — not just the records you added. Any record that exists at
Namecheap and not at Netlify simply stops resolving when the delegation takes effect.

The one that hurts: **MX records**. If the school receives email at `@goldenroyalark.com`, moving
nameservers without copying the MX (and any SPF/DKIM/DMARC TXT) records silently stops mail
delivery, and the bounces are not obvious for hours.

So, in this order: list every existing record at Namecheap (A, CNAME, MX, TXT, SRV) → recreate them
all in Netlify DNS, including `api` and `results` pointing at the VPS → only then change the
nameservers. Keep the old records visible in another tab until resolution has moved; propagation is
usually minutes but can take up to 48 hours.

Staying on Namecheap DNS is also fine: point `app` at Netlify with a CNAME and keep the rest where
it is. That avoids the email risk entirely, at the cost of managing records in two places.

## 1. Before you touch the server: the hostname rule

Sign-in uses a `__Host-` prefixed session cookie with `SameSite=Lax`
([AuthCookies.cs](../src/SchoolManagement.Api/Security/AuthCookies.cs)). That has one hard
consequence:

> **The admin web app and the API must be served from the same registrable domain.**

So `app.goldenroyalark.com` calling `api.goldenroyalark.com` works, and a `*.netlify.app` frontend
calling that API **does not** — the browser withholds the session cookie and every sign-in fails
with no useful error. The same applies to staging: Render must serve a `goldenroyalark.com`
subdomain, not `*.onrender.com`.

All four were confirmed by the school on 2026-09-22 and are what the committed configuration uses.
Two of them are expensive to change later:

| Name | In config | Changing it later costs |
|---|---|---|
| API | `api.goldenroyalark.com` | a config change and a redeploy |
| Parent portal | `results.goldenroyalark.com` | **every pin slip already printed** — it is printed on the slip and in the QR code |
| Admin web app | `app.goldenroyalark.com` | a `Cors__AllowedOrigins__0` change |
| Staging API | `staging-api.goldenroyalark.com` | a Render setting |

Point A records for the API and portal names at the VPS **before** step 3, because certbot proves
domain control over HTTP.

---

## 2. Provision the box (once)

Ubuntu 24.04 LTS. Copy the `deploy/` directory to the server and run:

```bash
sudo bash provision-vps.sh
```

It installs PostgreSQL, Nginx, certbot, the .NET 10 ASP.NET Core runtime and a firewall; creates
the `gras` service account, both databases with **separate roles and passwords**, the state
directories, the systemd unit and the nightly backup timer; and generates the secrets. It is
idempotent — re-running it never overwrites a secret or touches an existing database.

### The two pin keys

The script generates these once and **prints them once**:

- **`Pins__LookupKey`** is the HMAC key that turns a parent's pin into the `lookup_key` column used
  to find it. If it is lost or changed, **every pin ever printed stops working** — and it is not
  stored in the database, so no database backup can recover it.
- **`Pins__EncryptionKey`** decrypts the stored copy used when staff reprint a pin. If it is lost,
  reprints break; parents' pins keep working.

Copy both into the school's password manager the day they are generated, along with the database
password from `/etc/gras/prod-db-password`. Nothing else in this system has that property.

### `/etc/gras/api.env`

Root-owned, mode 0600, read by the systemd unit, never in git:

```
# Single-quoted: the value contains semicolons, and anything that reads this file as shell would
# otherwise truncate it at the first one. systemd strips the quotes.
Database__ConnectionString='Host=127.0.0.1;Port=5432;Database=gras_prod;Username=gras_prod;Password=...'
Pins__LookupKey=<base64 of 32 bytes>
Pins__EncryptionKey=<base64 of 32 bytes>
Cloudinary__CloudName=...
Cloudinary__ApiKey=...
Cloudinary__ApiSecret=...
```

Everything that is *not* a secret — the CORS origin, the portal's public URL, the proxy trust list,
the key-ring path — is committed in
[appsettings.Production.json](../src/SchoolManagement.Api/appsettings.Production.json). Any of it
can still be overridden here: an environment variable beats the file, and nested keys use a double
underscore (`Cors__AllowedOrigins__0`).

The service **refuses to start** with the pin keys or the Cloudinary credentials missing. That is
deliberate: the alternative is a system that accepts uploads it will lose, and prints pins nobody
can redeem.

## 3. TLS

```bash
sudo certbot --nginx -d api.goldenroyalark.com -d results.goldenroyalark.com
sudo nginx -t && sudo systemctl reload nginx
```

certbot edits the two site files in place and installs its own renewal timer. The sites are not
reloaded by the provisioning script, because they reference certificates that do not exist yet.

## 4. Cloudinary

In the Cloudinary dashboard, under Settings → API Keys, create a key pair and put the cloud name,
key and secret into `/etc/gras/api.env`. Then `sudo systemctl restart gras-api`.

Assets are uploaded as `type: authenticated`, which means they are **not** publicly reachable by
URL: the API fetches them with a signed URL and streams them back through its own
privilege-checked endpoint (spec 9.6). The `gras/prod` folder prefix keeps production and staging
apart inside one account.

**Smoke test — one command, no database or browser needed:**

```bash
cd /opt/gras/api
sudo -u gras env ASPNETCORE_ENVIRONMENT=Production   $(sudo grep -E '^Cloudinary__' /etc/gras/api.env | xargs)   dotnet /opt/gras/api/SchoolManagement.Api.dll cloudinary-smoke-test
```

Three details in that command are load-bearing: `sudo grep`, because `api.env` is root-only and a
plain `grep` as the deploy user silently yields nothing (the test then runs with no credentials and
fails confusingly); `cd /opt/gras/api`, because the content root comes from the working directory
and `appsettings.Production.json` would not load from anywhere else; and
`ASPNETCORE_ENVIRONMENT=Production`, for the same reason — without both, the asset lands under
`gras/` instead of `gras/prod/`.

It uploads a 73-byte PNG through the same store the endpoints use, reads it back through a signed
URL, compares the bytes and exits non-zero with the reason if any step fails. It **refuses to pass**
against the in-memory store, so a green run cannot be a false one. It prints the asset id; delete it
from the dashboard if you like.

Run this before anything else depends on the box. The adapter was verified this way against the
school's real account on 2026-09-22 (byte-identical round trip), so a failure here is this host's
configuration, not the code.

In the Cloudinary dashboard the new asset should show as **Authenticated**, not public. That is the
privacy property spec 9.6 relies on, and it is worth one look the first time.

## 5. Deploy

From GitHub: **Actions → deploy-production → Run workflow**, with the ref to deploy (`main`).

It is `workflow_dispatch` only — a merge does not deploy. `backend-ci.yml` decides whether `main`
is deployable; this decides when it is deployed.

The workflow needs four repository secrets on a `production` environment:

| Secret | What |
|---|---|
| `VPS_HOST` | the server's address |
| `VPS_USER` | the SSH user (not root) |
| `VPS_SSH_KEY` | that user's private key |
| `VPS_KNOWN_HOSTS` | output of `ssh-keyscan <host>`, so the runner verifies the box |

And one sudoers rule, so the deploy user can run exactly the deploy and nothing else:

```
# /etc/sudoers.d/gras-deploy  (mode 0440, validate with `visudo -c`)
deployer ALL=(root) NOPASSWD: /usr/local/bin/gras-deploy /tmp/gras-release
```

What a deploy does, in order: back up the database → install the release into
`/opt/gras/releases/<timestamp>` (root-owned, read-only) → apply migrations from a bundle built from
the same commit, run as the `gras` user with the connection string passed through the environment
rather than argv → swap the `/opt/gras/api` symlink → restart → poll `/health/ready` for 20
seconds. **If the migration fails, the previous version is still running and serving traffic**,
because the symlink has not moved.

`/opt/gras/api` is a symlink, not a directory. Provisioning deliberately does not create it; the
first deploy does.

### Rollback

Releases are kept as timestamped directories, three deep:

```bash
ls /opt/gras/releases
sudo ln -sfn /opt/gras/releases/<previous> /opt/gras/api.new
sudo mv -Tf /opt/gras/api.new /opt/gras/api
sudo systemctl restart gras-api
```

(Two steps so the swap is atomic — the same thing `deploy.sh` does.)

That rolls back **code only**. A migration that has already run stays applied, which is why every
deploy takes a `pre-deploy` dump first — see `/var/backups/gras`.

## 6. The first admin account

```bash
sudo -u gras dotnet /opt/gras/api/SchoolManagement.Api.dll bootstrap-admin \
  --email=head@goldenroyalark.com --staff-name="Firstname Lastname"
```

It prints a temporary password to the terminal, once, and the account must change it at first
sign-in. Run it in a session whose scrollback you can clear.

## 6a. The admin web app on Netlify

Build config is committed in [netlify.toml](../../netlify.toml) — base `frontend`, publish
`frontend/dist`, the SPA fallback the router needs, and the four environment variables that are the
same everywhere. Two things are NOT in it and must be set per site, because production and staging
are two Netlify sites reading the same file:

| | Production site | Staging site |
|---|---|---|
| `VITE_API_BASE_URL` | `https://api.goldenroyalark.com` | `https://staging-api.goldenroyalark.com` |
| `VITE_APP_ENV` | `production` | `staging` |

Vite bakes these into the bundle at build time, so a change needs a redeploy.

**Add the custom domain before expecting sign-in to work.** On the Netlify-assigned
`*.netlify.app` address the app loads and looks fine, but every sign-in fails: that origin is
cross-site to the API, so the browser withholds the `__Host-` session cookie, and the API's CORS
list does not contain it either. This is configuration, not a bug — do not go debugging auth over
it. The app is only usable on `app.goldenroyalark.com`.

Order that works: create the site, set the two variables to their FINAL values (even before the API
exists), deploy, add the custom domain. The first deploy then only proves the build; the app starts
working when the API comes up.

## 7. Staging on Render

Deploy from `backend/Dockerfile`, build context `backend/`. Render terminates TLS at its edge and
sets `$PORT`, both of which the image handles.

Environment variables to set in the Render dashboard:

```
Database__ConnectionString=Host=<vps-host>;Port=5432;Database=gras_staging;Username=gras_staging;Password=...;SSL Mode=Require
Cors__AllowedOrigins__0=https://staging.goldenroyalark.com
Portal__PublicUrl=https://staging-api.goldenroyalark.com
Proxy__Enabled=true
Proxy__TrustAllProxies=true
Pins__LookupKey=<a DIFFERENT key from production>
Pins__EncryptionKey=<a DIFFERENT key from production>
Cloudinary__CloudName=...        # same account
Cloudinary__ApiKey=...
Cloudinary__ApiSecret=...
Cloudinary__FolderPrefix=gras/staging
```

Generate staging's pin keys separately (`openssl rand -base64 32`). Sharing production's would mean
a staging compromise hands over the ability to compute production lookup keys.

`Proxy__TrustAllProxies` is right here and wrong on the VPS: Render's edge address is not
published, and nothing but that edge can route to the container. On the VPS, Nginx is named by
address instead.

### Opening the staging database to Render

Render reaches the VPS over the public internet, so this is a deliberate exposure of port 5432 —
restricted to Render's published outbound addresses for your region, and to the staging role only.
The production database stays unreachable from outside the box.

1. In `/etc/postgresql/16/main/postgresql.conf`: `listen_addresses = 'localhost,<vps-ip>'`
2. In `pg_hba.conf`, one line per Render outbound address, TLS required:
   `hostssl gras_staging gras_staging <render-ip>/32 scram-sha-256`
3. `sudo ufw allow from <render-ip> to any port 5432 proto tcp`
4. `sudo systemctl restart postgresql`

Render publishes its outbound IPs per region in the dashboard. If they change, staging loses its
database and production is unaffected — which is the intended blast radius.

## 8. Backups

`gras-backup.timer` runs nightly at 00:30 UTC (01:30 WAT) and before every deploy. Dumps land in
`/var/backups/gras`, are kept 14 days, and each one is verified readable (`pg_restore --list`)
before older dumps are pruned.

**They are all on the same disk as the database, which means they are not yet backups.** To send
them offsite, create `/etc/gras/offsite.sh` (mode 0700, root-owned); the backup script calls it
with the archive path as `$1` when it exists, and warns on every run while it does not. Whatever
destination you choose, **include `/etc/gras/api.env`** — a dump without the pin keys restores to a
system where no printed pin works.

Restore:

```bash
sudo -u postgres pg_restore --clean --if-exists -d gras_prod /var/backups/gras/<file>.dump
```

## 9. Operating it

```bash
sudo systemctl status gras-api          # is it up
sudo journalctl -u gras-api -f          # live logs
sudo journalctl -u gras-api -p err -n 50
curl -s localhost:5000/health/ready     # database reachable?
sudo systemctl list-timers gras-backup  # when did it last back up
```

`/health/live` says the process is alive; `/health/ready` opens a real database connection. Nginx
keeps `/health/live` out of the access log.

## 10. Known limits of this setup

Honest list, so nobody discovers these during an incident:

- **One box, one process.** A restart is a few seconds of downtime, and there is no failover. For
  one school of a few hundred pupils this is the right trade; it is still a trade.
- **The Data Protection key ring is plain XML** in a mode-0700 directory. Linux has no OS key store
  to wrap it with, so directory permissions are the protection. Losing it invalidates outstanding
  CSRF tokens, nothing more — sessions are database-backed.
- **Staging's database sits on the production box.** Separate role, separate database, separate
  password, but the same disk, the same CPU and the same Postgres instance. A runaway staging query
  can slow production down.
- **Postgres 5432 is exposed to Render's addresses** once section 7 is done. That is the price of
  staging not needing its own database host.
- **Result PDFs and the key ring are on local disk**, not in the database, so they are outside the
  nightly dump. The PDFs are a cache and regenerate; the key ring is discussed above.
