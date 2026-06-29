# Validation

## Proof Strategy

The story is complete when durable schema exists, current audit read/write paths
use the database without changing public API shape, command attempts are written
for mission dispatch results, and the solution builds.

## Test Plan

| Layer | Cases |
| --- | --- |
| Unit | Deferred until test project exists. |
| Integration | API audit endpoint still returns `MissionAuditEntry`; command dispatch writes audit and attempt rows. |
| E2E | Deferred. |
| Platform | `dotnet build NewAGV.sln`. |
| Logs/Audit | Runtime proof should show schema initialization and durable audit read/write path. |

## Fixtures

- Rejected command for missing robot can prove durable write without sending a
  SEER command.

## Commands

```text
dotnet build NewAGV.sln
.\scripts\bin\harness-cli.exe story verify US-008
```

## Acceptance Evidence

- Added PostgreSQL schema initialization for `app.audit_entries` and
  `app.command_attempts`.
- Added EF entities and `AuditLogService` to persist audit entries and command
  attempts while preserving the current `MissionAuditEntry` public API shape.
- Routed mission command dispatch and taskchain audit writes through durable
  audit persistence.
- Runtime proof: started `NewAGV.Api`, schema initialization log showed
  `CREATE TABLE IF NOT EXISTS app.audit_entries` and
  `CREATE TABLE IF NOT EXISTS app.command_attempts`; posted a rejected command
  for robot `MISSING-US008`, and API log showed `INSERT INTO app.audit_entries`
  plus `INSERT INTO app.command_attempts`; `GET /api/audit` returned the
  rejected `GoToStation` audit row.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.
- Harness verification: `.\scripts\bin\harness-cli.exe story verify US-008`
  passed.
