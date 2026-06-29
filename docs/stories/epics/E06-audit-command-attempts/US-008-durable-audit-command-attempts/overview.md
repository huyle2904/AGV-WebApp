# US-008 Durable Audit And Command Attempts

## Current Behavior

Product audit entries are held in memory by `AgvPlantStore` and are lost on API
restart. Mission command dispatch creates a command result and an in-memory
audit row, but command attempts are not durable product records.

## Target Behavior

NewAGV persists operator-facing audit entries and command attempts in
PostgreSQL while keeping the existing public API and Web behavior stable.
`GET /api/audit` continues to return `MissionAuditEntry` records, but reads from
durable storage when available.

## Affected Users

- Viewer: can inspect persisted recent audit entries after restart.
- Operator: command outcomes remain traceable.
- Supervisor/Admin: operational history becomes durable enough for incident
  review.

## Affected Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`

## Non-Goals

- Do not add authentication/RBAC.
- Do not change public API route shape.
- Do not standardize every command safety path in this story.
- Do not add retention policy UI.
