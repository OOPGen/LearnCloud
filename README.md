# LearnCloud

**School management platform for independent schools in Zimbabwe:** fees, attendance,
timetables, examinations and report cards, plus portals for parents, teachers and students.
Built in Bulawayo by [MKLabs](https://mklabs.co.zw).

![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![React](https://img.shields.io/badge/React-Vite_+_Tailwind-20232A?logo=react&logoColor=61DAFB)
![Cloudflare Workers](https://img.shields.io/badge/Cloudflare-Workers-F38020?logo=cloudflare&logoColor=white)
![Railway](https://img.shields.io/badge/Railway-API_+_DB-0B0D0E?logo=railway&logoColor=white)

---

## What it does

| Area | Highlights |
|---|---|
| **Fees & finance** | Invoices, part-payments allocated oldest-first, arrears at a glance. All money arithmetic lives in one tested service, to the cent |
| **Attendance & timetable** | One-tap register capture; timetable editor that explains clashes in plain language |
| **Examinations** | Marks capture and term reports with fair ranking (ties, absences and optional subjects handled explicitly) |
| **Portals** | Parents see only their own children; teachers only their own classes, enforced server-side |
| **Setup & onboarding** | Guided setup wizard and CSV learner import |
| **Platform** | Multi-tenant SaaS: each school on its own subdomain, with plans, trials and per-learner billing |

Also includes modules for library, hostel, transport, HR, messaging, online payments and analytics.

## Architecture

```
Browser ──► Cloudflare Worker (React web app) ──► /api/* proxy ──► ASP.NET Core API (Railway) ──► PostgreSQL (Railway)
Browser ──► Cloudflare Worker (marketing site) ─────────────────► API (demo & contact forms)
```

- **Modular monolith:** one ASP.NET Core API composed from feature modules (`src/LearnCloud.*`):
  Auth, MultiTenancy, Fees, Finance, Examinations, AttendanceTimetable, portals, PlatformBilling and more.
- **Multi-tenancy:** shared database and schema with a `tenant_id` discriminator. The tenant is
  resolved from the validated JWT, then applied through EF Core global query filters and
  checked again on save. Cross-tenant access is rejected with 403 and logged.
- **Auth:** JWT access tokens plus an `HttpOnly`, `SameSite=Strict` refresh cookie, Argon2id
  password hashing, and role-based permissions.
- **Data:** EF Core migrations are the single source of truth, applied automatically before each deploy.

## Quality

CI (GitHub Actions) runs on every push:

- Release build of the whole solution
- **Tenant isolation guards:** a script that fails the build if tenant-scoping rules are broken
- **Migration drift check:** fails if the EF model and migrations disagree
- **Vulnerable package scan** across all transitive dependencies
- **147 automated tests:** xUnit unit tests, plus integration tests against a real PostgreSQL in Docker (Testcontainers)
- Production Docker image builds

## Tech stack

**Backend:** C#, .NET 8, ASP.NET Core Web API, Entity Framework Core, Npgsql, FluentValidation,
Swagger / OpenAPI, MailKit · **Database:** PostgreSQL 16 ·
**Frontend:** React, React Router, Vite, Tailwind CSS · **Hosting:** Railway (API + database, Docker),
Cloudflare Workers (web app and marketing site) · **Testing:** xUnit, Testcontainers

## Running locally

Requires the .NET 8 SDK and Docker.

```powershell
./scripts/dev-setup.ps1              # secrets, PostgreSQL in Docker, migrations
dotnet run --project src/LearnCloud.Api
```

Then open Swagger at <http://localhost:5080/swagger>. Full steps are in
[docs/LOCAL_DEVELOPMENT.md](docs/LOCAL_DEVELOPMENT.md), and deployment is covered in
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Documentation

Specifications live in [`docs/specs`](docs/specs): requirements (SRS), database schema,
multi-tenancy, roles & permissions matrix, fee calculation rules, report-card edge cases,
offline sync strategy and the system test plan.

---

Built by **[Michael Junior Jere](https://mklabs.co.zw/michael)** · [MKLabs](https://mklabs.co.zw), Bulawayo, Zimbabwe
