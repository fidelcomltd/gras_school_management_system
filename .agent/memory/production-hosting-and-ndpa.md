---
name: production-hosting-and-ndpa
description: "Prod is PostgreSQL + app on one Namecheap Pulsar VPS, files on Cloudinary; NDPA compliance is relaxed for this school"
metadata: 
  node_type: memory
  type: project
  modified: 2026-09-22T19:06:45.042Z
---

Production (decided by the project lead 2026-09-22): the .NET app and PostgreSQL both run on **one Namecheap Pulsar VPS**; uploaded
files (logo, signature, later photos) go to **Cloudinary**. This resolved STATE.md open question 5.
The school's domain is **goldenroyalark.com** (project lead, 2026-09-22). The four hostnames were confirmed by the project lead on
2026-09-22: **api.** (prod API), **results.** (parent portal, so `Portal__PublicUrl=https://results.goldenroyalark.com`),
**app.** (admin frontend on Netlify), **staging-api.** (Render). All four must stay on goldenroyalark.com: the session
cookie is `__Host-` + `SameSite=Lax`, so a `*.netlify.app` or `*.onrender.com` frontend origin breaks every sign-in.
Changing **results.** after a pin batch is printed invalidates the slips and QR codes already handed to parents.

Secrets never go in chat (project lead asked 2026-09-22 whether to paste Cloudinary keys; told no): they go in
`/etc/gras/api.env` on the VPS, Render's dashboard, or `dotnet user-secrets` locally.

NDPA 2023: the school (Owerri) is **not seeking NDPA certification** and the project lead said it isn't strictly applied locally.
Don't gate features or add processing-agreement work for NDPA; the project lead will say if that changes.

**Why:** these settle the deployment drift items (DP key ring on VPS disk, SameSite=Lax if frontend and API share a domain,
cookie domain = school domain) and remove a compliance blocker on Cloudinary.

**How to apply:** design for a single Linux VPS (local disk available, one Postgres instance, no managed services except
Cloudinary). Keep good data hygiene (EXIF stripping, audit, access control) but don't add NDPA-only ceremony.
