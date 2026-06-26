# Design

## Domain Model

Existing model seeds:

- `WorkflowDefinitionEntity`
- `WorkflowStepEntity`
- `WorkflowRunEntity`
- `WorkflowRunStepEntity`
- `WorkflowExecutionStatus`
- `WorkflowStepExecutionStatus`
- `WorkflowFailurePolicy`

Target runtime concepts:

- Workflow runtime command: start, pause, resume, cancel, reconcile.
- Workflow runtime result: accepted, rejected, already-active, not-found,
  validation-failed, unknown.
- Workflow execution owner: Worker.
- Workflow step attempt: future table/model for retry/fallback evidence.
- Workflow event: future table/model for timeline and audit.

Rules:

- One AGV can have at most one active workflow execution.
- A public run request must not send a SEER command before the run intent is
  durably recorded.
- Worker must not blindly retry a side-effecting command when it is unknown
  whether SEER accepted the previous command.
- API remains the Web-facing query and realtime facade.

## Application Flow

Current flow:

```text
Web
  -> API WorkflowsController
  -> API WorkflowExecutionService
  -> API TaskChainCoordinator
  -> API SeerWorkerClient
  -> Worker SeerTaskChainService
  -> SEER TCP
```

Target flow:

```text
Web
  -> API WorkflowsController
  -> API safety/validation facade
  -> API internal Worker client
  -> Worker workflow runtime module
  -> Worker SeerTaskChainService
  -> SEER TCP
```

Runtime updates:

```text
Worker runtime update
  -> API internal sync endpoint or shared persistence
  -> API SignalR TelemetryHub
  -> Web PlantStateService / Workflow page
```

## Interface Contract

First implementation slice should add contracts only. Suggested DTO names:

- `WorkerWorkflowStartRequest`
- `WorkerWorkflowControlRequest`
- `WorkerWorkflowRuntimeResult`
- `WorkerWorkflowRuntimeStatus`

Suggested Worker internal endpoints:

```text
POST /internal/workflows/{workflowId}/start
POST /internal/workflows/pause
POST /internal/workflows/resume
POST /internal/workflows/cancel
GET  /internal/workflows/active-run?robotId=...
```

Slice 1 rule:

- Endpoints may return `501 Not Implemented` or a rejected result with a clear
  message.
- Do not wire public API to these endpoints yet.
- Do not remove `WorkflowExecutionService`.
- Do not change `NewAGV.Web`.

## Data Model

Current PostgreSQL workflow tables remain the source for definitions and run
history during migration.

No schema change in Slice 1.

Future slices may add:

- `workflow_step_attempts`
- `workflow_execution_events`
- active execution uniqueness guard/index
- command attempts

Any schema change must be a separate implementation slice with migration proof.

## UI / Platform Impact

The first migration slice must have no UI impact.

During later slices:

- Web should keep calling existing public API routes.
- Runtime status shape returned to Web should remain compatible with
  `WorkflowRunDto`.
- Any new Worker path should be hidden behind API compatibility.

## Observability

Slice 1:

- Log when Worker receives an internal workflow runtime request.
- Return clear "not implemented" messages.

Future slices:

- Log workflow run id, robot id, step sequence, taskchain name, and correlation
  id.
- Emit durable workflow execution events.
- Preserve product audit for operator actions.

## Alternatives Considered

1. Move the entire runtime in one patch.
   - Rejected because the current UI and API already work, and a large move
     would be hard for a code-editing model to verify safely.

2. Keep runtime in API.
   - Rejected by `docs/decisions/0009-workflow-runtime-owned-by-worker.md`.

3. Give Worker direct PostgreSQL access immediately.
   - Deferred. It may be correct, but it should be decided in a separate slice
     because it affects deployment/configuration and persistence ownership.

## Persistence Decision

Slice 5A accepted `docs/decisions/0010-worker-runtime-persistence-path.md`.

Worker workflow runtime will persist directly to PostgreSQL through a future
neutral persistence layer shared by API and Worker. Worker must not reference
`NewAGV.Api` to reuse `NewAgvDbContext`.

Ownership split after that extraction:

- API keeps workflow definition CRUD, public run queries, and Web-facing
  realtime fan-out.
- Worker owns workflow runtime writes for run creation, step progression,
  pause/resume/cancel, failure policy, and reconciliation.
- Database schema changes, including the active-execution guard, remain separate
  implementation slices.
