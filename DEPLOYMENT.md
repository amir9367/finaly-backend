# Clinic deployment guide

Covers: VPS sizing, Cloudflare DNS, automatic HTTPS via Caddy, and the deploy
steps. Written for `darmangahteboreza.ir` pointing at VPS `63.185.208.128`.

## 1. VPS spec

**2 vCPU / 4 GB RAM / 40–60 GB SSD (NVMe preferred)** — comfortable for a
single clinic doing ~200 patients/day. Live traffic is light (bursty
bookings/cancels + throttled SMS sends, max 4 concurrent); the only real
resource spike is during `docker compose up --build`, when the two frontend
images run `npm install` + `vite build`. If you're tight on budget, 1 vCPU /
2 GB works too as long as you add 2 GB of swap first.

Postgres backups matter more than raw compute at this scale — set up
automated snapshots for the VPS or the `clinic-pgdata` volume.

## 2. Cloudflare DNS

Add three A records, each pointing at the bare VPS IP — **no port in the
IP field**, Cloudflare's proxy only forwards ports 80/443 to the origin:

| Type | Name    | IPv4 address     | Proxy status |
|------|---------|------------------|--------------|
| A    | admin   | 63.185.208.128   | Proxied      |
| A    | booking | 63.185.208.128   | Proxied      |
| A    | api     | 63.185.208.128   | Proxied      |

Then in **SSL/TLS → Overview**, set the mode to **Full** or **Full
(strict)** — not Flexible. Caddy issues a real cert on the origin, so
Cloudflare → origin traffic needs to be HTTPS too, or you'll hit a redirect
loop.

### The Saba Host TXT record

Separate from the above. Saba Host's ticket asked you to add a TXT record
on the root domain (`darmangahteboreza.ir`) so *their* panel can validate
ownership and issue its own certificate for whatever they host for you
(webmail, their default page, etc.). It's independent of the Caddy setup
here — TXT records on the root don't conflict with A records on
subdomains, so it's safe to add regardless. It does **not** cover
`admin.`, `booking.`, or `api.` — Caddy still issues its own certs for
those automatically, no manual TXT step needed.

## 3. Firewall

Open only **80** and **443** on the VPS. Nothing else needs to be exposed —
Postgres has no host port, and admin/booking/api are only reached
internally by Caddy.

## 4. Files

Three files go at the repo root, alongside the existing `src/` folder:

- `docker-compose.yml` — replaces the existing one; adds the `caddy`
  service and removes the host port mappings on `admin`, `booking`, and
  `api` (Caddy reaches them over the internal Docker network instead).
- `Caddyfile` — new; routes each subdomain to its container and handles
  HTTPS automatically via Let's Encrypt (HTTP-01 challenge on port 80, no
  DNS step needed).
- `.env.example` — unchanged from the current repo; included here for
  completeness.

## 5. Deploy steps

```bash
# On the VPS, in the repo root:
cp .env.example .env
nano .env                     # fill in real values — see required vars below

docker compose up -d --build
docker compose logs -f caddy  # confirm certs issue cleanly on first request
```

Required `.env` values (compose fails fast if any are missing):
- `POSTGRES_PASSWORD` — strong DB password
- `JWT_SECRET` — 32+ random characters
- `ADMIN_PASSWORD` — 12+ characters, the clinic admin login

First request to each subdomain triggers Caddy to fetch its cert
automatically — allow a few seconds on the very first hit to each domain.

## 6. Verify

- `https://admin.darmangahteboreza.ir` → admin panel loads over HTTPS, no
  cert warning
- `https://booking.darmangahteboreza.ir` → booking page loads
- `https://api.darmangahteboreza.ir/health` → API health check responds
- `docker compose ps` → all five containers (`postgres`, `api`, `admin`,
  `booking`, `caddy`) show healthy/running
