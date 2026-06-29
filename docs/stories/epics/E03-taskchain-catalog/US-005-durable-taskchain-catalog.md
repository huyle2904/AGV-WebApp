# US-005 Durable Taskchain Catalog

## Status

implemented

## Lane

normal

## Product Contract

Taskchain catalog data synced from AGV must be stored durably in PostgreSQL
instead of existing only in API memory. API taskchain listing and workflow
validation should be able to rely on a stable catalog snapshot with sync
metadata, and missing taskchains must not silently disappear.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0008-product-database-postgresql.md`

## Acceptance Criteria

- A durable taskchain snapshot model exists in the product database.
- Sync writes current taskchain catalog rows and preserves sync metadata.
- A taskchain that disappears from the source is marked missing/stale instead of
  being silently removed from the catalog.
- API taskchain list can read from the durable catalog instead of only
  `TaskChainStore`.
- Public taskchain routes and `NewAGV.Web` remain compatible in the first slice.

## Design Notes

- Commands:
  `POST /api/taskchains/sync` is not introduced in Slice 1; current list query
  can continue to trigger sync until the explicit sync command story is chosen.
- Queries:
  `GET /api/taskchains` should eventually read durable taskchain snapshots.
- API:
  Keep `TaskChainsController` route shape compatible in Slice 1.
- Tables:
  Add a taskchain snapshot table with name, source timestamps, availability, and
  sync timestamps.
- Domain rules:
  Missing taskchains remain queryable and identifiable as missing.
- UI surfaces:
  Existing TaskChains and Workflow pages must continue to load.

## Validation

When updating durable proof status, use numeric booleans:
`scripts/bin/harness-cli story update --id <id> --unit 1 --integration 1 --e2e 0 --platform 0`.

| Layer | Expected proof |
| --- | --- |
| Unit | Deferred until dedicated test project exists. |
| Integration | Build plus API code path reading/writing durable taskchain catalog. |
| E2E | Not required in Slice 1. |
| Platform | `dotnet build NewAGV.sln` |
| Release | Deferred. |

## Harness Delta

Record US-005 in the durable story matrix and keep slice evidence here until a
dedicated validation report becomes necessary.

## Evidence

Slice 1 target:

- Add durable taskchain snapshot persistence and sync metadata.
- Keep current taskchain public DTO shape compatible.
- Validate with `dotnet build NewAGV.sln`.

Slice 1 result:

- Added `TaskChainSnapshotEntity` to `NewAGV.Persistence` with durable sync
  metadata: availability, source state, source timestamp, sync timestamp,
  missing timestamp, and last known status.
- Added PostgreSQL schema creation/index statements for
  `app.taskchain_snapshots`.
- Added `TaskChainCatalogService` in API to sync Worker taskchain list into
  PostgreSQL and mark unseen taskchains as `MissingFromSource` instead of
  deleting them.
- Updated `TaskChainCoordinator.GetTaskChainsAsync` to return the durable
  catalog after sync, while keeping the existing `SeerTaskChainSummary` public
  DTO shape compatible.
- Updated `TaskChainCoordinator.GetTaskChainStatusAsync` to persist last known
  taskchain status back into the durable catalog.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 2 result:

- Extended `SeerTaskChainSummary` additively with `Availability`,
  `SourceState`, `LastSyncedAt`, `MissingSince`, and `ExternalId`.
- `TaskChainCatalogService` now computes `Available`, `MissingFromSource`, and
  `Stale` views from durable catalog data.
- `WorkflowValidationService` now reads the durable taskchain catalog instead of
  forcing a live Worker fetch during validation.
- Workflow validation now fails with clear errors when a referenced taskchain is
  missing from source or the durable taskchain catalog is stale.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 3 result:

- Added explicit `POST /api/taskchains/sync` command in
  `TaskChainsController`.
- `TaskChainCoordinator` now separates `SyncTaskChainsAsync` from list queries
  and serializes sync calls with a local lock.
- `GET /api/taskchains` remains compatible by auto-syncing only when the
  durable catalog is empty; otherwise it reads the persisted catalog.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 4 result:

- Updated `NewAGV.Web` TaskChains page to surface durable catalog availability,
  source state, sync time, missing time, and external ID without changing
  public API routes.
- Updated `NewAGV.Web` Workflow step preview to show `MissingFromSource` and
  `Stale` catalog states directly from the durable taskchain catalog metadata
  already returned by API.
- Kept `GET /api/taskchains/{name}` behavior unchanged; the Web layer now
  combines durable catalog metadata from `GET /api/taskchains` with the
  existing live detail call when available.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Runtime follow-up:

- Fixed API dependency injection lifetime mismatch introduced by the durable
  taskchain catalog wiring: `TaskChainCoordinator` is now scoped and
  `TaskChainMonitorService` resolves it through a per-iteration scope instead
  of holding a singleton dependency.
- Local runtime proof now passes for startup wiring: API serves Swagger on
  `http://localhost:5222`, Worker serves internal endpoints on
  `http://localhost:5230`, and Web root responds on `http://localhost:5209`.

US-010 validation follow-up:

- Added automated unit coverage in `tests/NewAGV.Api.Tests` for
  `TaskChainCatalogService` sync-add, missing-from-source, and stale-threshold
  behavior.
- Added automated unit coverage in `WorkflowValidationService` proving durable
  taskchain catalog validation blocks missing and stale taskchain references.
