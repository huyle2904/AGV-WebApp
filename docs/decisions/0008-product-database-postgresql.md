# 0008 Product Database: PostgreSQL

Date: 2026-06-25

## Status

Accepted

## Context

`SPEC.md` recommends that NewAGV use a database once workflow definitions,
execution history, taskchain snapshots, map metadata, audit records, and command
attempts become product behavior.

The current codebase already uses EF Core with PostgreSQL/Npgsql:

- `src/NewAGV.Api/Program.cs` configures `UseNpgsql`.
- `src/NewAGV.Api/Data/NewAgvDbContext.cs` uses PostgreSQL-oriented schema and
  `jsonb` columns.
- `src/NewAGV.Api/Services/DatabaseInitializationService.cs` contains
  PostgreSQL-specific schema upgrade SQL.

The open question was whether to follow the SPEC's single-machine pilot option
of SQLite or keep PostgreSQL as the product database.

## Decision

NewAGV will use PostgreSQL as the product database.

PostgreSQL is the default target for product persistence in the current project,
including:

- Workflow definitions and versions.
- Workflow runs, step executions, attempts, and execution events.
- Taskchain catalog snapshots and sync history.
- Map metadata, map versions, station snapshots, and route metadata when
  available.
- Product audit records and command attempts.
- App settings that belong to NewAGV rather than AGV/RoboshopPRO.

The Harness durable database (`harness.db`) remains separate SQLite operational
state for agent/process records. It is not the NewAGV product database.

## Alternatives Considered

1. SQLite for pilot.
   - Pros: simple local setup, easy file backup, lower infrastructure cost.
   - Rejected because the codebase already depends on PostgreSQL-specific EF
     configuration and SQL, and future workflow/audit/sync behavior benefits
     from stronger production database semantics.

2. SQL Server.
   - Pros: strong Microsoft ecosystem fit.
   - Rejected for now because the current implementation already targets
     PostgreSQL and no enterprise SQL Server requirement has been stated.

3. No product database.
   - Rejected because saved workflows, run history, sync snapshots, audit, and
     command traceability are core product requirements.

## Consequences

Positive:

- Aligns with the current codebase and avoids a provider switch before the first
  stabilization pass.
- Supports production-style workflow history, sync metadata, audit, and command
  attempts.
- Allows richer indexes, constraints, and JSON payload snapshots where useful.

Tradeoffs:

- Local development needs PostgreSQL availability.
- Schema management must move away from ad hoc `EnsureCreated` plus manual
  upgrade SQL toward proper migrations.
- Single-machine pilot deployment is heavier than a pure SQLite setup.

## Follow-Up

- Add a product database runbook for local PostgreSQL setup.
- Replace ad hoc schema upgrade SQL with EF Core migrations or a documented
  migration flow.
- Add missing product tables for taskchain snapshots, map snapshots, audit, and
  command attempts in bounded stories.
- Keep `harness.db` documented as separate from the product database.
