# Mimo v2.5 Handoff

Use this file when launching OpenCode with Mimo v2.5 for the first code slice.

## Important

You are Mimo v2.5 acting as a code-editing model. Do not redesign architecture.
Follow the story and limits exactly.

Before editing, read these files:

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0008-product-database-postgresql.md`
- `docs/decisions/0009-workflow-runtime-owned-by-worker.md`
- `docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/overview.md`
- `docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/execplan.md`
- `docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/design.md`
- `docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/validation.md`
- `src/NewAGV.Contracts/WorkflowContracts.cs`
- `src/NewAGV.Worker/Program.cs`
- `src/NewAGV.Api/Services/SeerWorkerClient.cs`
- `src/NewAGV.Api/Controllers/WorkflowsController.cs`

## Slice 1 Goal

Add internal workflow runtime contract DTOs and Worker endpoint skeletons.

This slice must not change runtime behavior.

## Allowed Changes

You may edit:

- `src/NewAGV.Contracts/WorkflowContracts.cs`
- `src/NewAGV.Worker/Program.cs`
- optionally one new Worker service file if needed, for example:
  `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs`

You may add small DTO records in `WorkflowContracts.cs`, such as:

- `WorkerWorkflowStartRequest`
- `WorkerWorkflowControlRequest`
- `WorkerWorkflowRuntimeResult`
- `WorkerWorkflowRuntimeStatus`

You may add Worker internal endpoints:

```text
POST /internal/workflows/{workflowId}/start
POST /internal/workflows/pause
POST /internal/workflows/resume
POST /internal/workflows/cancel
GET  /internal/workflows/active-run?robotId=...
```

These endpoints should return a clear rejected/not-implemented runtime result.

## Forbidden Changes

Do not edit:

- `src/NewAGV.Web/**`
- public API route behavior in `WorkflowsController`
- `WorkflowExecutionService`
- database schema/entities
- existing SEER command behavior
- appsettings
- Docker files

Do not:

- move workflow execution logic yet
- route public API calls to the new Worker endpoints
- change public DTOs used by the Web unless strictly additive
- delete existing code
- add auth/RBAC
- add fallback/retry behavior

## Expected Result

After your patch:

- The project compiles.
- Public workflow behavior is unchanged.
- Worker has internal workflow endpoint skeletons ready for a later slice.
- New DTOs are additive and do not break existing serialization.

## Validation Command

Run:

```powershell
dotnet build NewAGV.sln
```

Report:

- files changed
- build result
- any warnings/errors
- anything you intentionally did not implement

## Suggested Prompt for OpenCode

```text
Read docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/mimo-handoff.md and implement only Slice 1.

Do not change public Web/API behavior.
Do not move runtime logic.
Only add additive contracts and Worker internal endpoint skeletons.
Run dotnet build NewAGV.sln and report files changed plus result.
```
