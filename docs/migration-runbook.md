# CivicFlow EF Core migration runbook

## New database

Restore tools, review migrations, then apply the production bundle. Never combine `EnsureCreated` with this database.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations bundle --project src/CivicFlow.Infrastructure --startup-project src/CivicFlow.Api -c Release -o artifacts/civicflow-migrate.exe
artifacts/civicflow-migrate.exe --connection "$env:CIVICFLOW_PRODUCTION_SQL"
```

The bundle applies `InitialCivicFlowSchema`, `AddLocationCoordinateConstraint` and `AddCaseAttachments`. Normal production startup has `DatabaseMigration:AutoMigrate=false` and fails closed if anything is pending.

## Existing Phase 2 database

1. Stop API writers; leave the SQL volume intact.
2. Create and verify a full `COPY_ONLY, CHECKSUM` backup. Restore it under a different database name and run `DBCC CHECKDB`.
3. Record counts for users, roles, cases, categories, activities, notifications and refresh tokens plus representative SLA/audit fields.
4. On the restored copy only, set `DatabaseMigration__EnableLegacyBaselineRegistration=true` and start once in an approved Development/Testing migration environment. The validator must report an exact semantic Phase 2 match before it records Initial. Any difference aborts before history is written.
5. Apply the two later migrations, repeat startup to prove idempotency, compare counts/schema/keys/indexes, and run all integration tests.
6. Take a fresh verified backup of the real database and repeat exactly once. Return `EnableLegacyBaselineRegistration` to false after successful registration. Do not enable `ApplyPhase2UpgradeBeforeBaseline` for an already hardened Phase 2 database.

## Failure and rollback

Validation failure is non-mutating: correct the unexpected schema or configuration; never force-insert migration history. If a later transactional migration fails, retain logs and restore the verified backup to a new database name, validate it, then switch the connection string during a controlled outage. Do not delete the SQL volume or use `EnsureDeleted`. A down migration is used only after explicit data-loss review; backup restore is the default rollback for a production upgrade.

The legacy `Phase2Upgrade` code remains available only behind its explicit option for older pre-hardening databases. It must not run after a successful baseline registration.
