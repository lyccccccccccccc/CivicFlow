# Contributing to CivicFlow

## Development workflow

1. Create a focused branch from `master`.
2. Keep domain rules, API authorization, UI visibility, and tests aligned.
3. Never commit `.env`, credentials, databases, Blob data, backups, logs, test output, or personal information.
4. Use conventional commit messages such as `feat:`, `fix:`, `test:`, `docs:`, `ci:`, and `chore:`.
5. Do not use destructive database setup in tests or migrations.

## Required checks

```powershell
dotnet restore CivicFlow.sln
dotnet build CivicFlow.sln -c Release --no-restore
dotnet test CivicFlow.sln -c Release --no-build
Set-Location src/CivicFlow.Client
npm ci
npm run lint
npm run build
npm audit
```

Run the real SQL Server integration suite for persistence, migration, transaction, or concurrency changes. Run an Azurite-backed API smoke test for attachment changes. Document manual browser verification for visible workflow changes.

## Pull requests

Explain the user-visible outcome, authorization impact, migrations, tests, and known limitations. Keep unrelated formatting or refactoring out of the change. Security vulnerabilities must follow [SECURITY.md](SECURITY.md), not a public issue.
