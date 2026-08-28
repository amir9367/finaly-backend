# Clinic Appointment System — Project Plan

> Backend API + Admin Panel + Public Booking Page for a multi-doctor clinic,
> with two-way Excel synchronization and SMS notifications.

**Status:** Requirements confirmed with the owner on 2026-08-26. Nothing in this
document is assumed — every decision below was explicitly chosen by the owner
during the requirements interview (4 rounds of questions).

---

## 1. What this system does

Patients book appointments online through a public booking page:

1. Patient picks a **doctor**
2. Sees that doctor's **available days & working hours for the next 2 weeks only**
3. Picks any **free time** inside those hours (overlaps are impossible)
4. Enters **name + mobile number** → booking saved → **confirmation SMS sent**

Clinic staff control everything from an **admin panel**: manage doctors and
their schedules, view/cancel/reschedule appointments, upload/download Excel
files, and monitor SMS delivery. When a doctor cancels an appointment, the
patient is automatically notified by SMS.

Appointments can also arrive from **Excel**: staff uploads the clinic's
appointment spreadsheet, the system imports it; everything booked online or in
the panel can be **exported back to the same Excel template** (two-way sync).

---

## 2. Confirmed requirements

| # | Area | Decision |
|---|------|----------|
| 1 | Backend stack | **.NET 8 Web API** (C#) |
| 2 | Database | **PostgreSQL** + EF Core |
| 3 | Excel role | **Two-way sync** — import via upload endpoint, export back to template |
| 4 | Excel format | Template designed by us; **Jalali dates everywhere, including inside Excel** |
| 5 | Booking | Full online booking; window limited to **today → +14 days** |
| 6 | Slot rules | Patient chooses doctor → sees days/hours → books **any free time**; overlapping appointments forbidden |
| 7 | Visit length | **Per-doctor default duration** (minutes), set by admin |
| 8 | Patient identity | **Name + phone number** (no accounts for now; design must allow adding accounts later) |
| 9 | Patient cancellation | Patients **can cancel themselves**, verified via SMS OTP to their phone |
| 10 | Doctors | **Many doctors**, each with profession/specialty + weekly schedule (days & hours), managed in panel |
| 11 | Cancellations by clinic | Doctors/admin may cancel anytime → **patient gets SMS notice** |
| 12 | SMS provider | **Melipayamak** (`rest.payamak-panel.com`) |
| 13 | Admin panel | **Separate React app**, admin-only login, Persian RTL UI, controls everything |
| 14 | Public site | Simple **React booking page** included in this project |
| 15 | Deployment | **Docker / docker-compose** |
| 16 | Language | **Mixed** — English API/messages/docs; **Persian (فارسی، راست‌به‌چپ)** panel UI & SMS texts |

---

## 3. Architecture

```
┌──────────────────┐      ┌──────────────────────┐      ┌─────────────┐
│  Booking Page    │      │      Clinic API       │      │ PostgreSQL  │
│  (React, public) ├────► │      (.NET 8)        ├────► │   (16)      │
└──────────────────┘  /api└──┬──────────┬────────┘      └─────────────┘
                             │          │
              ┌──────────────┘          └──────────────┐
              ▼                                        ▼
   ┌────────────────────┐                   ┌────────────────────┐
   │   Admin Panel      │                   │  External services │
   │   (React, staff)   │                   │  • Melipayamak SMS │
   └────────────────────┘                   │  • Excel files     │
                                            │    (upload/export) │
                                            └────────────────────┘
```

- The React apps talk only to the REST API (`/api`).
- The API owns all business rules (availability, overlap prevention, Jalali
  conversion, SMS, Excel parsing). The frontends contain no business logic.
- All timestamps are stored in **UTC** (`timestamptz`); converted to/from
  **Iran time (UTC+03:30, no DST)** and the **Jalali calendar** at the API edge.
- Iranian week: **Saturday = 0 … Friday = 6** (used in schedules).

### Repository layout

```
D:\A.GH\Backend\
├── plan.md                  ← this file
├── README.md                ← how to run
├── docker-compose.yml       ← postgres + api + admin-panel + booking-page
├── src\
│   ├── Clinic.Api\          ← .NET 8 Web API (all backend logic)
│   ├── Clinic.AdminPanel\   ← Vite + React + TypeScript + Ant Design (RTL)
│   └── Clinic.BookingPage\  ← Vite + React + TypeScript + Ant Design (RTL)
└── tests\
    └── Clinic.Api.Tests\    ← xUnit unit tests
```

---

## 4. Data model

| Table | Columns (essential) | Notes |
|---|---|---|
| `doctors` | id, full_name, specialty, default_visit_minutes, is_active, created_at | Doctor matched in Excel by exact name |
| `doctor_schedules` | id, doctor_id, weekday (0=Sat…6=Fri), start_time, end_time, is_active | Weekly recurring availability |
| `appointments` | id, short_code, doctor_id, patient_name, patient_phone, starts_at, ends_at, status, source, notes, cancelled_at, cancel_reason, created_at | UTC timestamps |
| `phone_otps` | id, appointment_id, phone, code_hash, expires_at, attempts, used | Self-cancel verification |
| `sms_logs` | id, appointment_id, phone, type, body, status, provider_message_id, error, created_at, sent_at | Full audit of every SMS |
| `admin_users` | id, username, password_hash, created_at | Seeded from env on first run |
| `excel_sync_logs` | id, file_name, uploaded_at, total_rows, imported, updated, skipped, row_errors (jsonb) | Import report shown in panel |

**Enums**

- Appointment `status`: `Booked`, `CancelledByPatient`, `CancelledByClinic`
- Appointment `source`: `Online`, `Admin`, `ExcelImport`
- SmsLog `type`: `BookingConfirmation`, `CancellationNotice`, `CancelOtp`
- SmsLog `status`: `Pending`, `Sent`, `Failed`

**Double-booking is impossible at the database level.** A PostgreSQL
*exclusion constraint* rejects any two active appointments of the same doctor
whose time ranges intersect (adjacent bookings like 09:00–09:20 + 09:20–09:40
are allowed):

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE appointments ADD CONSTRAINT appointments_no_overlap
  EXCLUDE USING gist (
    doctor_id WITH =,
    tstzrange(starts_at, ends_at) WITH &&
  )
  WHERE (status = 0);   -- Booked only
```

---

## 5. REST API (English, Swagger UI at `/swagger`)

### Public endpoints

| Method & path | Purpose |
|---|---|
| `GET /api/doctors` | List active doctors (name, specialty, visit length) |
| `GET /api/doctors/{id}/availability` | Days + working hours + busy intervals for the next ≤14 days |
| `POST /api/appointments` | Book: `{doctorId, patientName, patientPhone, startJalali:"1405/06/04 14:30"}` → sends confirmation SMS, returns booking incl. `shortCode` |
| `POST /api/appointments/lookup` | `{shortCode, phone}` → booking details. A POST keeps the phone out of URLs (and out of access logs/history) |
| `POST /api/appointments/{id}/cancel/request` | `{phone}` → verifies it matches the booking, sends OTP SMS |
| `POST /api/appointments/{id}/cancel/confirm` | `{phone, code}` → cancels (`CancelledByPatient`); phone and code are both required |

### Admin endpoints (JWT Bearer)

| Method & path | Purpose |
|---|---|
| `POST /api/admin/auth/login` | `{username,password}` → JWT |
| `GET/POST/PUT/DELETE /api/admin/doctors` | Manage doctors **and their weekly schedules** |
| `GET /api/admin/appointments` | Filter by doctor / date range / status |
| `PATCH /api/admin/appointments/{id}/cancel` | Clinic cancels → **SMS notice to patient** |
| `PUT /api/admin/appointments/{id}/reschedule` | Move booking (validates availability) |
| `POST /api/admin/excel/import` | Upload `.xlsx` → import + per-row error report |
| `GET /api/admin/excel/template` | Download empty template |
| `GET /api/admin/excel/export` | Download current data as the same template |
| `GET /api/admin/sms/logs` | SMS audit log |
| `GET /api/admin/dashboard/stats` | Today count, next-7-days count, active doctors, failed SMS |

### Availability & booking rules

- Window: from **now** (Tehran time) through **+14 days**, inclusive. Nothing
  outside the window can be booked.
- A requested time is valid iff it lies fully inside one of the doctor's
  schedule windows for that weekday, is in the future, and does not overlap an
  existing `Booked` appointment.
- Overlap races (two patients clicking simultaneously) are resolved by the DB
  exclusion constraint → API returns HTTP **409 Conflict**.
- End time = start + doctor's `default_visit_minutes`.

---

## 6. Excel integration (two-way sync)

Template workbook designed by us (ClosedXML, MIT license):

- Sheet **`Appointments`** — columns:
  `Doctor | Specialty | Date | Start | Duration(min) | Patient Name | Patient Phone | Status | Notes`
  - `Date`: Jalali text, e.g. `1405/06/04`
  - `Start`: 24-hour text, e.g. `14:30`
  - `Duration`: optional — falls back to the doctor's default
  - `Status`: `Booked`, `CancelledByPatient`, `CancelledByClinic` (plain `Cancelled` is accepted as an alias for clinic cancellation; empty means `Booked`)
  - `Doctor` must match a doctor's name exactly (case-insensitive)
- Sheet **`ReadMe`** — instructions in English + Persian.

**Import:** rows are upserted by `(doctor, date+time)`. Rows with bad data are
never silently dropped — each problem is reported back (row number + reason).
A row that would collide with an *online* booking (same slot, different
patient) is reported as a conflict and skipped, never overwritten blindly.
Every import is recorded in `excel_sync_logs` and shown in the panel.

**Export:** regenerates `Appointments` from current database state — the
staff's way of receiving everything booked online/in-panel ("back" direction
of the sync).

---

## 7. SMS (Melipayamak)

Persian message templates:

| Type | Text |
|---|---|
| Confirmation | `{نام} عزیز، نوبت شما با {دکتر} در تاریخ {1405/06/04} ساعت {14:30} ثبت شد. کد پیگیری: {ABC12345}` |
| Cancellation (clinic) | `{نام} عزیز، نوبت شما با {دکتر} در تاریخ {…} ساعت {…} لغو شد. برای تعیین نوبت جدید با کلینیک تماس بگیرید.` |
| Cancel OTP | `کد تایید لغو نوبت شما: {123456}` |

- Provider call: `POST https://rest.payamak-panel.com/api/SendSMS/SendSMS`
  (form-encoded: username, password, to, from, text).
- Behind an `ISmsSender` interface. In Development the **console sender**
  prints messages to logs — the whole system works without credentials.
- Sends run fire-and-forget with retries; results land in `sms_logs`.
  **A booking never fails because an SMS failed.**

---

## 8. Security

- Admin auth: username + password (BCrypt hash) → JWT bearer token (default
  2 h, ≥32-char secret required at startup). First admin seeded from env on
  first run — there is **no** default password; seeding is refused without a
  strong one, as are known-default passwords.
- Patient self-cancel: 6-digit OTP sent via SMS, hashed at rest, valid 5 min,
  **max 5 attempts (enforced)**, single use, with a resend cooldown and a
  per-appointment daily cap. Cancelling requires phone **and** code together.
- Rate limiting on login, public booking, lookup and the OTP flow; online
  bookings are capped per phone number; background SMS dispatch is bounded.
- Excel exports escape formula-injection prefixes (`= + - @`); imports are
  capped by file size, decompressed size and row count.
- CORS open in dev only; in production each SPA reaches the API same-origin
  through its nginx proxy.
- Secrets come exclusively from environment variables / compose env — never
  hard-coded, never silently defaulted. Startup fails fast when they are
  missing or weak. Swagger UI and the console SMS sender are Development-only.
- All containers run non-root; Postgres has no host port; TLS terminates at
  your ingress/reverse proxy (HSTS hook present in both nginx configs).

---

## 9. Frontends

**Admin Panel** (`src/Clinic.AdminPanel`, Vite + React + TS + Ant Design 5,
`direction="rtl"`, Persian): Login → Dashboard, پزشکان (CRUD + weekly schedule
editor + visit duration), نوبت‌ها (filter, cancel, reschedule), اکسل (upload,
download template/export, import report), پیامک‌ها (log table).

**Booking Page** (`src/Clinic.BookingPage`, same stack): wizard
انتخاب پزشک → انتخاب روز (next 14 days, Jalali labels) → انتخاب ساعت (free
slots computed client-side from availability, re-validated server-side) →
فرم نام و موبایل → تأیید؛ plus «مدیریت نوبت من» (lookup by tracking code +
OTP cancel).

Both apps proxy `/api` to the API in dev and ship behind nginx proxies in
Docker.

---

## 10. Deployment (Docker)

`docker-compose.yml` services:

| Service | Image base | Port (host) |
|---|---|---|
| `postgres` | postgres:16-alpine + volume + healthcheck | internal only — no host port |
| `api` | .NET SDK build → ASP.NET runtime, runs schema bootstrap on start | **8080** |
| `admin` | node build → nginx (proxies `/api` → `api:8080`) | **3000** |
| `booking` | node build → nginx (proxies `/api` → `api:8080`) | **3001** |

One command brings the whole stack up: `docker compose up --build`.

Configuration is environment-driven (`ConnectionStrings__Default`,
`Jwt__Secret`, `AdminSeed__Username/Password`, `Sms__Provider`,
`Sms__Melipayamak__Username/Password/Origin`, `Booking__WindowDays`).

---

## 11. Implementation checklist

Status: **all phases coded** (2026-08-26). End-to-end verification via
`docker compose up --build` is still outstanding — see README quick-start.

- [x] Phase 0 — plan.md (this document)
- [x] Phase 1 — solution scaffold, EF Core model, DB bootstrap (seed + exclusion constraint)
- [x] Phase 2 — public API: doctors, availability, booking, OTP cancel
- [x] Phase 3 — admin API: auth, doctors CRUD, appointments, dashboard
- [x] Phase 4 — Excel template/import/export + sync logs
- [x] Phase 5 — SMS layer (console sender default, Melipayamak ready)
- [x] Phase 6 — Admin panel (React)
- [x] Phase 7 — Booking page (React)
- [x] Phase 8 — Docker compose + README
- [x] Phase 9 — Unit tests (Jalali math, slot math, Excel parser) — written; run with `dotnet test`

## 12. Open items (owner must provide before go-live)

1. **Melipayamak credentials**: username, password, sender line number.
2. **Production JWT secret** (≥32 random chars) and admin password.
3. Real clinic name/branding if the panels should carry it.
4. Whether reminder SMS before appointments is wanted (phase 2 candidate — not built yet).

## 13. How to run

```bash
cp .env.example .env   # then set strong POSTGRES_PASSWORD / JWT_SECRET / ADMIN_PASSWORD
docker compose up --build
```

Then: Panel http://localhost:3000 · Booking page http://localhost:3001 · API
http://localhost:8080 (`/health`). Swagger UI runs in Development only; log in
to the panel with the `ADMIN_USERNAME` / `ADMIN_PASSWORD` you set in `.env`.
