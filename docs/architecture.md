# CivicFlow architecture decisions

## 1. Modular monolith

CivicFlow begins as a modular monolith. The project needs clear boundaries and maintainability, but its portfolio-sized workload does not justify distributed deployment, messaging infrastructure or microservice failure modes.

## 2. Controller-based ASP.NET Core API

Controllers make endpoint grouping, authorisation attributes, filters, response documentation and enterprise conventions visible to reviewers. Minimal APIs may still be used for isolated infrastructure endpoints such as health checks.

## 3. Domain-enforced state transitions

`ServiceRequest` owns its status changes. Controllers call domain methods rather than assigning arbitrary status values. This prevents invalid changes from alternative clients and provides a focused unit-test surface.

## 4. SQL Server and EF Core

The domain contains strongly related transactional data: users, teams, categories, cases, assignments, comments and audit events. A relational database is therefore the primary store. EF Core configurations live in Infrastructure so the Domain project remains independent of persistence.

## 5. Identity separation

ASP.NET Core Identity lives in Infrastructure. Business entities keep user identifiers but do not depend on Identity framework classes. JWT authentication and hashed rotating refresh tokens are exposed by the API.

## 6. UTC persistence

All server timestamps use UTC. The client is responsible for presenting dates in the user's local timezone.

## 7. Accessibility and privacy

The client targets WCAG 2.2 AA practices. Public comments and internal notes are filtered at the API boundary to reduce accidental disclosure risk.

## 8. Phase 2 additive hardening

CaseActivities remain the append-only source of audit truth. Business pages use an explicit allowlist/projection rather than a public flag alone. No API edits/deletes audit rows; the persistence boundary rejects modified/deleted audit entities. SystemAdministrator and TeamManager have a separate read-only audit route; user/category writes remain administrator-only.

The original database used EnsureCreated rather than an EF migration baseline. `Phase2Upgrade` therefore runs a narrowly scoped, idempotent transactional SQL Server upgrade, protected by an application lock: nullable `CaseActivities.OperationKey` and `UserNotifications.EventKey` columns plus filtered unique indexes. Existing rows are retained. Only cases with both SLA targets missing receive category defaults from their original SubmittedAt, with an appended audit record. Existing due dates, summaries and history are untouched. Startup seeds only a newly created database, never an existing one (so disabled categories and changed user roles are preserved).

Case UpdatedAt is an optimistic concurrency token; state change, activity and notification are saved in one transaction. A concurrent state/unique-key conflict returns 409. SLA monitoring emits one at-risk and one overdue notice per target/recipient, not a repeat every twelve hours.

The SLA upgrade adds nullable `FirstResponseCompletedAtUtc` and `FirstResponseWasBreached` columns. Existing business fields and history are not cleaned, deleted or rewritten; only these new derived columns are backfilled from the earliest stored public staff comment and its stored due date. Future public staff messages set both fields atomically; the breach flag is deliberately not recalculated after manager due-date changes.

Integration tests default to isolated EF InMemory databases. Set `CIVICFLOW_TEST_SQL` to run the same suite against a real SQL Server database; the suite never deletes/recreates the database. It adds uniquely labelled regression cases/categories and retains them for inspection. Supply the connection string via the environment, never a committed file. The SQL-specific duplicate constraint assertion runs only on SQL Server (the default run checks model metadata).

## 9. Phase 3A migrations, files and location

`InitialCivicFlowSchema` is a frozen representation of the `phase-2-complete` schema. Existing databases may record that migration only when the explicit legacy-registration switch is enabled and a semantic SQL Server validator confirms the Phase 2 tables, columns, types, nullability, keys, indexes and foreign keys. Constraint names are not compared. `AddLocationCoordinateConstraint` and `AddCaseAttachments` are always applied normally after the baseline. `Phase2Upgrade` remains an explicit legacy tool but is not part of normal startup after registration.

Development and test startup uses `MigrateAsync` behind a SQL Server application lock. Production startup never applies pending changes: the release pipeline creates and applies an EF migration bundle, and startup fails if a migration remains pending. Seeding is idempotent and limited to Development/Testing.

The Application project owns `IFileStorage` and storage contracts. Infrastructure implements private Azure Blob-compatible storage using an Azurite connection string locally or `DefaultAzureCredential`/managed identity in production. The API creates a randomized storage key, validates content, stores the blob, then commits attachment/audit/notification data. A database failure deletes the just-written blob; a storage failure creates no row. A maintenance worker removes unreferenced blobs and physically deletes blobs only after the 30-day soft-delete retention period.

Latitude/longitude are optional but must be supplied as a pair and remain within WGS84 bounds. Address text stays mandatory. The client map boundary is limited to configurable tile URL/attribution inputs and numeric coordinates, so a future Azure Maps or ArcGIS component can replace Leaflet without changing the case API.
