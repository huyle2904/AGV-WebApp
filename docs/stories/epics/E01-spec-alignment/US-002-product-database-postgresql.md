# US-002 Product Database PostgreSQL Decision

## Status

implemented

## Lane

high-risk

## Product Contract

NewAGV uses PostgreSQL as the product database for workflow, sync, audit,
command, and map metadata persistence. The local Harness SQLite database remains
separate operational state and is not the NewAGV product database.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0008-product-database-postgresql.md`

## Acceptance Criteria

- A durable decision record states that PostgreSQL is the product database.
- The decision distinguishes product PostgreSQL from Harness `harness.db`.
- Follow-up work is clear: migrations, missing product tables, and runbook.

## Design Notes

- Commands: none.
- Queries: none.
- API: current PostgreSQL path remains accepted.
- Tables: no schema changes in this story.
- Domain rules: no behavior changes.
- UI surfaces: none.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Not applicable; decision-only story. |
| Integration | Not applicable; no runtime change. |
| E2E | Not applicable. |
| Platform | Not applicable. |
| Release | Review accepted ADR before database implementation stories. |

## Harness Delta

Recorded product database direction before implementation.

## Evidence

- `docs/decisions/0008-product-database-postgresql.md`
- Durable decision `0008-product-database-postgresql`.
