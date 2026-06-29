# Overview

## Current Behavior

Workflow runtime progression currently lives in the API process.

Relevant modules:

- `src/NewAGV.Api/Controllers/WorkflowsController.cs`
- `src/NewAGV.Api/Services/WorkflowExecutionService.cs`
- `src/NewAGV.Api/Services/WorkflowMonitorService.cs`
- `src/NewAGV.Api/Services/WorkflowValidationService.cs`
- `src/NewAGV.Api/Services/TaskChainCoordinator.cs`
- `src/NewAGV.Api/Services/SeerWorkerClient.cs`
- `src/NewAGV.Worker/Program.cs`
- `src/NewAGV.Worker/Services/SeerTaskChainService.cs`

Current API-owned runtime responsibilities:

- Accept a public workflow execute request.
- Validate workflow definition and robot assignment.
- Check active workflow/taskchain state.
- Create `WorkflowRunEntity` and `WorkflowRunStepEntity` rows.
- Start the next pending workflow step.
- Call Worker through `SeerWorkerClient` to execute a SEER TaskChain.
- Poll active runs through `WorkflowMonitorService`.
- Apply timeout, retry, continue, and manual-resume behavior.
- Emit workflow SignalR events.

This works as a prototype, but it contradicts
`docs/decisions/0009-workflow-runtime-owned-by-worker.md`.

## Target Behavior

Worker owns workflow runtime progression.

API remains the public facade. API should:

- Keep current public Web-facing routes stable during migration.
- Validate public requests and role/safety preconditions.
- Query workflow definitions/runs for Web.
- Send internal workflow runtime commands to Worker.
- Bridge Worker runtime updates to Web through existing API/SignalR seams.

Worker should:

- Accept internal workflow runtime commands from API.
- Start workflow runs after API accepts a user request.
- Dispatch taskchain steps to SEER.
- Monitor taskchain runtime state.
- Advance workflow steps.
- Apply timeout/retry/fallback/manual-intervention policies.
- Own reconciliation after uncertain SEER state, Worker restart, API restart, or
  TCP disconnect.

## Affected Users

- Operator: existing Run/Pause/Resume/Cancel behavior must remain stable.
- Supervisor: workflow definition and validation behavior must remain stable.
- Administrator/Engineer: system design becomes safer for future fallback and
  reconciliation work.

## Affected Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0008-product-database-postgresql.md`
- `docs/decisions/0009-workflow-runtime-owned-by-worker.md`

## Non-Goals

- Do not redesign the Blazor Workflow page in this story.
- Do not add full fallback UI in the first migration slice.
- Do not implement BPMN, parallel branches, arbitrary loops, or scripting.
- Do not change the public Web-facing workflow routes in the first slice.
- Do not replace PostgreSQL.
- Do not remove the current API runtime until Worker runtime path is proven.
