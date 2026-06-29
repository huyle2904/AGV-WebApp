# US-009 Map Snapshot And Station Versioning

## Status

implemented

## Lane

normal

## Product Contract

Map and station data synced from SEER/RoboshopPRO must survive API restarts.
NewAGV stores durable map entity snapshots with map name, version, sync
freshness, and source state while keeping the existing public API shape stable.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`

## Acceptance Criteria

- `GET /api/map/entities` returns map entities from durable snapshot storage
  when available.
- `POST /internal/sync/map` persists the current map/station snapshot and
  updates the in-memory plant state used by current realtime/UI flows.
- Manual engineer map entity upsert/delete keeps durable storage and in-memory
  state consistent.
- Existing public route shape and `MapEntity` response shape do not change.
- Build and Harness verification pass.

## Design Notes

- Commands: no AGV command path changes in this story slice.
- Queries: `GET /api/map/entities` remains the public map query.
- API: internal sync map endpoint remains `POST /internal/sync/map`.
- Tables: additive PostgreSQL table for map entity snapshots.
- Domain rules: station identity is `EntityId`; version increments when a
  station/entity changes or is manually edited.
- UI surfaces: no Web page changes in slice 1.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Deferred until test project exists. |
| Integration | API starts, initializes map snapshot table, sync endpoint persists snapshot, public map query returns it. |
| E2E | Deferred. |
| Platform | `dotnet build NewAGV.sln`; `harness-cli story verify US-009`. |

## Harness Delta

None planned.

## Evidence

Slice 1 result:

- Added PostgreSQL-backed `map_entity_snapshots` persistence with entity id,
  map name, type, coordinates, version, source state, sync timestamp, missing
  timestamp, and properties JSON.
- Added `MapSnapshotService` so `GET /api/map/entities`, manual map
  upsert/delete, internal map sync, and command target station validation can
  read or update durable map snapshots while keeping current in-memory UI flows
  synchronized.
- Kept public API route shape and `MapEntity` response shape unchanged.
- Runtime proof: started `NewAGV.Api`, schema initialization log showed
  `CREATE TABLE IF NOT EXISTS app.map_entity_snapshots`; posted
  `POST /internal/sync/map` with station `US009-ST-01`; log showed
  `INSERT INTO app.map_entity_snapshots`; `GET /api/map/entities` returned the
  synced station.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.
- Harness verification: `.\scripts\bin\harness-cli.exe story verify US-009`
  passed.

US-010 validation follow-up:

- Added automated unit coverage in `tests/NewAGV.Api.Tests` for
  `MapSnapshotService` durable snapshot persistence, missing-entity exclusion,
  and versioning behavior.
- Test coverage exposed and fixed a normalization bug where empty properties
  could cause unchanged map entities to receive a false version increment on
  resync.
