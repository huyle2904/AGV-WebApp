# US-003 Workflow Runtime Owned by Worker Decision

## Status

implemented

## Lane

high-risk

## Product Contract

NewAGV workflow execution progression belongs in Worker because Worker owns the
SEER AGV TCP connection. API remains the public facade, safety gate, query
surface, and realtime bridge.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0009-workflow-runtime-owned-by-worker.md`

## Acceptance Criteria

- A durable decision record states that workflow runtime ownership moves to
  Worker.
- The decision names the API responsibilities that remain.
- The decision names Worker responsibilities for progression, retry, fallback,
  and reconciliation.
- Follow-up implementation must be sliced before code changes.

## Design Notes

- Commands: future API-to-Worker workflow runtime commands.
- Queries: API remains public query surface.
- API: will no longer own runtime progression after migration.
- Tables: future execution/attempt/event tables still in product database.
- Domain rules: runtime state transitions should move behind a Worker-owned
  module/interface.
- UI surfaces: existing Web behavior should remain stable during migration.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Not applicable; decision-only story. |
| Integration | Not applicable; no runtime change. |
| E2E | Not applicable. |
| Platform | Not applicable. |
| Release | Review accepted ADR before workflow runtime migration stories. |

## Harness Delta

Recorded workflow runtime ownership before refactor.

## Evidence

- `docs/decisions/0009-workflow-runtime-owned-by-worker.md`
- Durable decision `0009-workflow-runtime-owned-by-worker`.
