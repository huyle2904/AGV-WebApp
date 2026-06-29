# US-010 Validation Foundation

## Status

implemented

## Lane

normal

## Product Contract

NewAGV must have an initial automated test foundation for the first durable
workflow, taskchain catalog, and map snapshot rules so future SPEC work does
not continue with weak proof only.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`

## Acceptance Criteria

- A `tests/NewAGV.Api.Tests` project exists in `NewAGV.sln` targeting
  `net8.0`.
- Focused automated tests cover durable taskchain catalog freshness and missing
  behavior, workflow validation against the durable catalog, and map snapshot
  persistence/versioning behavior.
- Harness story verification for `US-010` runs `dotnet test NewAGV.sln`.
- Prior stories whose behavior is now materially covered by unit tests have
  their proof matrix updated.

## Design Notes

- Commands:
  No AGV command path changes.
- Queries:
  No public route changes.
- API:
  Test-only changes; no controller contract changes.
- Tables:
  No schema changes; tests use EF Core InMemory.
- Domain rules:
  Cover taskchain availability/staleness, workflow validation blocking, and map
  snapshot version transitions.
- UI surfaces:
  None.

## Validation

When updating durable proof status, use numeric booleans:
`scripts/bin/harness-cli story update --id <id> --unit 1 --integration 1 --e2e 0 --platform 0`.

| Layer | Expected proof |
| --- | --- |
| Unit | `dotnet test NewAGV.sln` proves service-level validation and snapshot behavior. |
| Integration | Existing runtime proof from earlier stories remains unchanged. |
| E2E | Deferred. |
| Platform | `dotnet restore NewAGV.sln`; `.\scripts\bin\harness-cli.exe story verify US-010`. |
| Release | Deferred. |

## Harness Delta

Adds the first persistent automated proof surface so later SPEC slices can stop
deferring unit coverage for foundational API services.

## Evidence

- Added `tests/NewAGV.Api.Tests` to `NewAGV.sln` with xUnit, EF Core InMemory,
  and local test helpers for `NewAgvDbContext`, `AgvPlantStore`, and workflow
  DTO setup.
- Added 10 automated tests covering `TaskChainCatalogService`,
  `WorkflowValidationService`, and `MapSnapshotService`.
- Test-driven follow-up fixed `MapSnapshotService` so empty properties and null
  properties normalize the same way; unchanged map entities no longer receive a
  false version increment during sync.
- Validation: `dotnet restore NewAGV.sln` passed using repo-local NuGet and
  CLI caches after networked package restore approval.
- Validation: `dotnet test NewAGV.sln --no-restore` passed with 10 tests, 0
  failures.
