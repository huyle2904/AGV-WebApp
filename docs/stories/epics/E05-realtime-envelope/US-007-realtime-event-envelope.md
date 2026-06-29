# US-007 Realtime Event Envelope

## Status

implemented

## Lane

normal

## Product Contract

SignalR realtime events are not source of truth by themselves. Event envelopes
must expose sequence/schema metadata so Web can detect gaps and refresh API
snapshots when realtime delivery is incomplete or reconnects happen.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`

## Acceptance Criteria

- Realtime events include sequence metadata.
- Realtime events include schema version metadata.
- Web refreshes authoritative API snapshots when a sequence gap is detected.
- Web refreshes authoritative API snapshots after telemetry reconnect.
- UI does not treat SignalR as sole source of truth.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Deferred until test project exists. |
| Integration | Build plus API/Web code path showing sequence/schema envelope and gap-triggered snapshot refresh. |
| E2E | Deferred. |
| Platform | `dotnet build NewAGV.sln` |

## Evidence

Slice 1 result:

- Added `Sequence` and `SchemaVersion` to `RealtimeEvent`.
- Added singleton `TelemetryEventPublisher` in API so all hub emissions share
  one monotonic sequence source and common schema version.
- Routed API realtime emissions through `TelemetryEventPublisher` from
  internal sync, command, taskchain, workflow, and simulation paths.
- Updated `PlantStateService` to detect telemetry sequence/schema gaps and
  refresh full API snapshots instead of trusting incomplete SignalR state.
- Updated `Workflow.razor` runtime listener to refresh workflow snapshot from
  API on telemetry gap or reconnect.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 2 result:

- Added internal runtime proof hook `POST /internal/sync/debug/skip-sequence`
  so the next realtime event is emitted with an intentional sequence gap.
- This makes the missed-event validation path reproducible without changing
  public API routes or user-visible contracts.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 3 result:

- Ran API, Worker, and Web together, opened the Web UI to establish a live
  telemetry circuit, then called `POST /internal/sync/debug/skip-sequence?count=1`.
- Worker health telemetry emitted the next realtime envelope with the skipped
  sequence, and Web log output showed `PlantStateService` detect the gap and
  refresh authoritative API snapshots instead of trusting the missed SignalR
  delta.
- Reconnect refresh path is wired through `TelemetryClientService.Reconnected`
  into both `PlantStateService` and `Workflow.razor`, so reconnects also force
  API-backed resync rather than leaving SignalR as source of truth.
- Validation: local runtime proof via `.codex-logs/web.out.log`, plus
  `dotnet build NewAGV.sln`.
