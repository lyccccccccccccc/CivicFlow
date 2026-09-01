# CivicFlow Phase 3A Acceptance Report

## Scope

Phase 3A introduces a safe transition from the legacy Phase 2 database upgrade path to formal EF Core migrations, private case attachments backed by Azure Blob-compatible storage, and optional map coordinates for service requests. It preserves the Phase 2 role model, resident data isolation, case workflow, SLA history, audit trail, notifications, users, categories, and existing case data.

Phase 3A explicitly excludes virus scanning, paid geocoding, email/SMS delivery, general document management, public Blob URLs, and any destructive database recreation or reseeding workflow.

## Branch and commits

The implementation branch is `feature/phase-3a-attachments-map`, based on the immutable `phase-2-complete` tag at `2032fbd9482817c43342ba261751c30f5424cf76`.

| Commit | Purpose |
| --- | --- |
| `70d1f4695862a206fc23ec5f78645985605f6bb3` | Add EF Core migration baseline infrastructure |
| `9a2884aa321bb4f104c59d4230aeba3956b3d950` | Add safe legacy database baseline registration |
| `ae9177e622f336a03e209a08c8d23d0bd28fa146` | Replace `EnsureCreated` with migration startup |
| `95534550a814eecfaaf821d7865d29e6608cee80` | Verify migration baseline safety |
| `bbe57d9f0cbfbc0967da641c8d5d70a33cf09117` | Add private Blob storage and Azurite |
| `2081e8489e21db3eabc68f7680698a3a97ece3de` | Add attachment model and migration |
| `2edffd5c6130dd3a4a9cb5e706f67e83c7c1c10b` | Add authorized attachment APIs |
| `63f60e782ff228a983185a325edc16508497dc47` | Add attachment security coverage |
| `a6208eb00e15e886b51806496f0c0502cc9b3798` | Add configurable map locations |
| `8864e38fb20e896fc094ca54e0294b9cb401d627` | Add resident and staff attachment workflows |
| `c64d69d464e7a610cf1deedf554d8bfefb22fc9c` | Preserve the Phase 2 upgrade boundary |
| `d5d932b9d631039c72999317d06762117dc42665` | Serialize development seeding |
| `fe087b1f9c03e34be50035a0af6503e17d0de1ee` | Harden local Blob integration and logging |
| `8e768e2b58757a038d4f54787d468df0ee99ac92` | Add migration and attachment runbooks |
| `02c42982554607d7a0c47c1534b8591e9f9ae0db` | Verify attachment failure compensation |
| `73eddfe382c94eec93b4bbea998fd0d87364eba6` | Enforce attachment lifecycle authorization |
| `723b7b3fb724e5b7bb7a12109c11b2cc86c6a300` | Add the Phase 3A acceptance report and ignore local operational artifacts |
| `6e86a071939aee7ba8896e059c1b8267816f88e1` | Stabilize attachment concurrency and real-SQL test isolation |

## EF Core migrations and legacy upgrade

The applied migration sequence is:

1. `20260901013912_InitialCivicFlowSchema`
2. `20260901014743_AddLocationCoordinateConstraint`
3. `20260901014826_AddCaseAttachments`

`InitialCivicFlowSchema` represents the unmodified Phase 2 schema. Location constraints and attachments are deliberately separate subsequent migrations. New databases apply the full sequence normally. Existing Phase 2 databases can be registered only when the legacy-baseline feature is explicitly enabled and the semantic schema validator confirms that tables, columns, types, nullability, keys, indexes, and foreign keys match the Phase 2 model. Any difference aborts startup before baseline registration or migration.

Migration startup uses a database lock. Development and test environments may use `MigrateAsync`; production uses a reviewed migration bundle. Legacy `Phase2Upgrade` remains available only as an explicit transition tool and is not run again after successful baseline registration. Development seed operations are idempotent and serialized. `EnsureCreated` and `EnsureDeleted` are not used in the normal startup path.

The existing Phase 2 database was backed up, restored to an isolated upgrade-test database, schema-validated, baselined, migrated, and compared before the same process was applied to the primary database. Restart testing confirmed that all three migrations remain recorded exactly once.

## Map support

Resident submissions retain a required textual address and may optionally include a latitude/longitude pair selected with React Leaflet. Coordinates must be supplied together and remain within latitude `-90..90` and longitude `-180..180`. A read-only map is shown to the case owner and authorized staff. Submission remains available if tiles are unavailable and does not depend on geocoding.

The tile URL and attribution are configuration-driven. The default development provider uses OpenStreetMap with visible attribution, allowing a future provider adapter to replace it with Azure Maps or ArcGIS without changing case-domain rules.

## Attachment architecture and validation

`IFileStorage` is defined in the Application project. Infrastructure implements private Azure Blob-compatible storage: Azurite via a local connection string and Azure Blob Storage via managed identity in production. Blob URLs and storage credentials are never exposed through the API.

Attachment metadata records the case, uploader, sanitized original filename, randomized storage key, server-validated content type and size, SHA-256 integrity digest, visibility, upload time, and soft-delete metadata. DTOs omit storage keys, hashes, private URLs, and connection details.

Uploads accept JPG, PNG, and PDF only, up to 10 MB per file and five attachments per case. Validation covers extension, declared content type, file signature, safe filename normalization, decoded image validity, and image pixel limits. A randomized storage key prevents path traversal. Database failure after Blob storage triggers Blob compensation; storage failure creates no database row. Idempotency keys protect upload retries. The maintenance worker reconciles orphaned objects and physically removes soft-deleted Blobs only after the configured 30-day retention period.

Downloads always repeat case authorization, use a safe `Content-Disposition`, set `Content-Length`, `ETag`, and `X-Content-Type-Options: nosniff`, and force PDFs to download as attachments.

## Permission matrix

| Capability | Resident | Assigned Officer | Manager | Administrator |
| --- | --- | --- | --- | --- |
| List/download Public attachment | Own case | Assigned case | Any authorized case | Any authorized case |
| List/download Internal attachment | Never | Assigned case | Any authorized case | Any authorized case |
| Upload Public | Own editable case | Assigned editable case | Authorized case | Authorized case |
| Upload Internal | Never | Assigned editable case | Authorized case | Authorized case |
| Soft-delete attachment | Own Public upload only | Own upload only | Authorized case | Authorized case |
| View complete audit log | No | No | Yes | Yes |

Unauthorized and cross-resident attachment access returns the project's uniform 404 response to reduce resource enumeration. Internal attachments are never projected, downloaded, or notified to residents.

## Ownership and lifecycle rules

Residents may upload or delete their own Public attachments only while the case is Submitted, Triaged, Assigned, In progress, Waiting for resident, or Reopened. They cannot mutate attachments while Resolved, Closed, or Rejected; a Resolved case must be reopened first.

The assigned Officer follows the same editable lifecycle boundary and may upload Public or Internal attachments, but may delete only attachments they uploaded. Managers and Administrators retain authorized staff operations. Every deletion requires a reason of 10–500 characters, is a soft delete, and produces an immutable audit event.

Reopen retains the original attachments, resolution history, SLA baseline, and audit history. Concurrent Reopen requests were verified to produce one effective Reopened event and one Officer notification, with the competing request receiving a predictable conflict response.

## Audit and notifications

Successful attachment upload and soft deletion generate immutable audit activity. Public attachment uploads notify the appropriate non-actor recipient; Internal attachments never notify residents. Rejected authorization requests create no attachment row, Blob mutation, audit/activity record, cleanup candidate, or notification. Notification writes remain actor-safe and event-key idempotent. Mark-read state persists through page refresh and complete service/database restart.

## Persistence and backup acceptance

SQL Server, Azurite, API, and frontend were restarted without deleting or recreating volumes. Users, roles, cases, categories, activities, attachments, SLA fields, coordinates, assignment, resolution/reopen history, and migration history remained intact. Active attachment downloads retained the correct filename and content type. Internal attachments remained staff-only. Soft-deleted attachments remained absent from normal API lists while their Blobs remained stored for retention cleanup.

The final database backup is retained in an external, repository-excluded location. Its size and SHA-256 were recorded in private operational evidence, and SQL Server `RESTORE VERIFYONLY WITH CHECKSUM` passed. Machine-local paths and backup fingerprints are intentionally omitted from this public report.

No backup file, local storage data, key, token, connection secret, or private Blob address is committed to Git.

## Automated and browser acceptance

- Release restore and build: passed with zero compiler warnings and errors.
- Unit tests: 11 of 11 passed.
- Isolated integration tests: 36 of 36 passed.
- Real SQL Server integration tests: three consecutive complete runs passed, 36 of 36 in each run.
- Real SQL Server and Azurite API persistence, authorization, list, download, soft-delete, and Blob-retention checks: passed.
- Frontend ESLint and production build: passed.
- Resident, Officer, Manager, and Administrator browser acceptance: passed; each role opened its list and the acceptance case detail with zero console errors.
- Git whitespace validation: passed.

The acceptance suite includes new-database migration, legacy no-loss upgrade, repeat migration startup, attachment format/size/count/signature checks, cross-resident isolation, Internal visibility, storage/database failure compensation, deterministic parallel uploads to distinct cases, attachment-limit races, coordinate validation, and Phase 2 regression coverage.

## Security defect corrected during acceptance

Direct API testing found that an assigned Officer could previously upload to a Resolved case and delete an attachment uploaded by another user. The controller now enforces the lifecycle boundary and uploader ownership before any Blob or database mutation. Regression tests verify 404 responses and identical attachment, soft-delete, Blob, audit, cleanup, and notification state before and after rejected requests.

## Concurrency defect corrected at the sealing gate

The complete parallel SQL Server suite intermittently returned HTTP 500 while a Manager uploaded an Internal attachment. SQL Server `system_health` identified the inner failure as a deadlock: two attachment uploads opened Serializable transactions, counted active attachments through `IX_CaseAttachments_ServiceRequestId_IsDeleted_UploadedAtUtc`, held adjacent `RangeS-S` locks, and then both attempted the `RangeI-N` conversion required to insert. SQL Server selected one database transaction as the deadlock victim. Because the original flow stored the Blob before opening the database transaction and rethrew the database exception after compensation, the API surfaced a generic 500. Azurite was not involved; the failing real-SQL test host used its isolated in-memory file-storage double, and Azurite logs contained no corresponding failure.

SQL Server uploads now use a ReadCommitted transaction plus a transaction-owned `sp_getapplock` resource scoped to the Case ID. Uploads for the same case are serialized around the authoritative attachment-count check and insert, while uploads for unrelated cases no longer hold conflicting index range locks. The initial and locked attachment-limit checks return stable 409 ProblemDetails. Known storage, SQL, and database-write failures return safe 503 ProblemDetails without exposing inner exceptions or storage metadata.

Blob-first compensation is retained: any rejected database write removes the randomized Blob, and a compensation failure is logged for orphan reconciliation without exposing the key to the client. Regression assertions verify that storage failure, an existing five-attachment limit, and a concurrent limit reached after Blob storage create no attempted attachment row, upload audit, or orphan Blob.

The shared real-SQL test database also exposed multiple `WebApplicationFactory` instances starting their own SLA and attachment-cleanup loops. Those background services could mutate unrelated fixture state and race on notification indexes. Ordinary API fixtures now omit those loops while keeping xUnit parallel execution enabled; worker behavior remains covered by its dedicated tests. Every API test continues to create unique cases, attachment IDs, Blob keys, operation keys, and registration identities, and does not depend on historical database row counts.

New or strengthened regression tests cover deterministic parallel uploads to distinct cases, repeat-safe Internal attachment isolation, storage failure without database/audit/Blob side effects, the five-active-attachment 409 contract, concurrent limit compensation, resident exclusion from Internal projection/download, and cross-resident 404 behavior. The corrected target test passed five consecutive real-SQL runs, and the full real-SQL suite passed three consecutive runs at 36 of 36 with no new deadlock event.

## Known non-blocking limitations

- Virus scanning is not implemented. Malware scanning and quarantine are required before production launch.
- OpenStreetMap is suitable only for local or low-volume use until its production tile policy is reviewed or the provider is replaced.
- The main production JavaScript bundle is greater than 500 kB and should be code-split in a later performance iteration.
- The 30-day Blob cleanup path is covered by automated retention logic and controlled tests, but has not been observed through a real 30-day manual waiting period.
