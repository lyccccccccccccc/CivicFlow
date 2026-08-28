# CivicFlow

CivicFlow is a full-stack community service request and case management portfolio system. Residents report local issues and track outcomes; internal teams triage, assign and resolve cases against transparent service targets.

> Independent portfolio prototype. CivicFlow is not affiliated with the Queensland Government or any government organisation.

## What is included

- Resident registration, JWT login, refresh-token rotation and role-based access
- Four roles: Resident, Case Officer, Team Manager and System Administrator
- Service request creation, search, detail view and ownership enforcement
- Domain-enforced workflow: submitted, triaged, assigned, in progress, waiting, resolved, closed, reopened and rejected
- Category-based first-response and resolution SLA targets
- Manager assignment, officer case actions and operational metrics
- Public resident communication, private internal notes and activity history
- In-app notifications, seeded categories and four demonstration accounts
- Responsive React/TypeScript/Material UI client
- SQL Server persistence, health check, OpenAPI, Docker Compose and CI
- xUnit domain tests and production front-end lint/build configuration

## Technology

| Layer | Technology |
| --- | --- |
| Client | React 19, TypeScript, Vite, Material UI, React Router |
| API | C# 14, ASP.NET Core 10 Controller API |
| Identity | ASP.NET Core Identity, JWT bearer tokens, rotating refresh tokens |
| Data | SQL Server 2022, Entity Framework Core 10 |
| Tests | xUnit |
| Delivery | Docker Compose, GitHub Actions |

## Start on Windows

Prerequisites: .NET 10 SDK, Node.js 22+, Docker Desktop and Git.

After extracting the ZIP, right-click `start-civicflow.ps1` and choose **Run with PowerShell**, or run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\start-civicflow.ps1
```

The script starts SQL Server, the API and the client, then opens `http://localhost:5173`. The database is created and seeded automatically on first run. Use `stop-civicflow.ps1` to stop the database container.

## Demo accounts

All seeded accounts use the development-only password `REDACTED_HISTORICAL_DEVELOPMENT_SECRET`.

| Role | Email |
| --- | --- |
| Resident | `resident@civicflow.local` |
| Case Officer | `officer@civicflow.local` |
| Team Manager | `manager@civicflow.local` |
| System Administrator | `admin@civicflow.local` |

## Manual development commands

```powershell
docker compose up -d
dotnet restore CivicFlow.sln
dotnet run --project src/CivicFlow.Api
```

In another terminal:

```powershell
cd src/CivicFlow.Client
npm install
npm run dev
```

- Client: `http://localhost:5173`
- API: `http://localhost:5168`
- Health: `http://localhost:5168/health`
- OpenAPI JSON (Development): `http://localhost:5168/openapi/v1.json`

## Architecture

The back end is a modular monolith with dependency direction `API → Infrastructure/Application → Domain`. Workflow invariants live in the Domain project, persistence and Identity live in Infrastructure, and HTTP/authentication concerns stay in the API. See `docs/architecture.md` and `docs/api-reference.md`.

## Security notes

- The API enforces authorization; UI visibility is not treated as security.
- Residents can access only their own cases, and internal notes are excluded from resident responses.
- Refresh tokens are cryptographically random and stored as SHA-256 hashes.
- The committed signing key, seeded passwords and launcher database password are development-only values. Replace all of them and use environment variables or a secret store before deployment.
- `EnsureCreated` is intentionally used for the self-contained portfolio demo. Production deployments should use reviewed EF Core migrations.
