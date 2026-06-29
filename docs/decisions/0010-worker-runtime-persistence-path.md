# 0010 Worker Runtime Persistence Path

Date: 2026-06-26

## Status

Accepted

## Context

ADR-0009 moves workflow runtime ownership to Worker because Worker owns the
SEER TCP connection and side-effecting AGV command path.

The remaining persistence question is how Worker records workflow runtime state
without weakening public API compatibility or risking duplicate SEER commands.

Current state:

- API owns `NewAgvDbContext` and PostgreSQL configuration.
- API owns workflow definition CRUD, validation, run queries, and SignalR
  emission.
- Worker owns SEER command services but has no product database access.
- Worker already pushes robot/map/health snapshots back to API through
  `ApiSyncClient`.
- Existing workflow run tables live in PostgreSQL under the `app` schema.

The runtime path needs a durable active-run guard before Worker dispatches a
TaskChain command. That guard must be colocated with run creation/progression so
Worker does not send a side-effecting SEER command before the run intent is
durably recorded.

## Decision

Worker workflow runtime will use direct PostgreSQL persistence through a shared,
neutral persistence layer, not through API-owned internal persistence endpoints.

The target ownership split is:

- API remains the public facade for Web and external callers.
- API owns workflow definition CRUD, public validation responses, public run
  queries, and realtime fan-out to Web.
- Worker owns workflow runtime writes for run creation, step progression,
  pause/resume/cancel state changes, failure handling, and reconciliation.
- API and Worker share the same PostgreSQL schema and EF model through a
  non-API project introduced in a future slice.

Worker must not reference `NewAGV.Api` to access `NewAgvDbContext`. Runtime
persistence types should move to or be duplicated into a neutral shared project
before Worker writes product workflow state.

## Alternatives Considered

1. Worker calls API internal persistence endpoints.
   - Pros: smaller immediate dependency change; API remains the only process
     with database access.
   - Rejected because the runtime owner would still depend on API availability
     for durable state transitions and active-run guards before sending SEER
     commands. It also keeps runtime failure policy split across processes.

2. Worker directly references `NewAGV.Api` to reuse `NewAgvDbContext`.
   - Pros: smallest code movement.
   - Rejected because it creates an incorrect dependency from Worker to the API
     interface layer and makes API implementation details part of Worker
     runtime.

3. Keep API as the runtime persistence owner while Worker only sends SEER
   commands.
   - Rejected because it contradicts ADR-0009 and preserves split ownership for
     retries, fallback, and uncertain command state.

## Consequences

Positive:

- Worker can record run intent and acquire the active-run guard before sending
  a SEER TaskChain command.
- Runtime progression, failure policy, and reconciliation can live in the same
  process that owns side-effecting AGV commands.
- API can keep the existing public Web/API shape while querying the shared
  product database.

Tradeoffs:

- A future slice must extract or introduce a neutral persistence project shared
  by API and Worker.
- Worker deployment must receive the `NewAgvDb` connection string.
- Schema initialization/migration ownership must be clarified before Worker
  becomes the default runtime path.
- SignalR fan-out still needs an API-facing sync path or polling bridge for
  Worker runtime updates.

## Follow-Up

- Add a neutral persistence project for workflow definitions, runs, and steps.
- Move or share `NewAgvDbContext` without making Worker depend on API.
- Add a durable active-execution guard for one active workflow per AGV.
- Keep database schema changes in a separate migration slice.
- Keep `Integration:UseWorkerWorkflowRuntime` disabled by default until Worker
  runtime persistence and start orchestration are proven.
