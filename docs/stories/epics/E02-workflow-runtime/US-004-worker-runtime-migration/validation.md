# Validation

## Proof Strategy

This migration must be proven slice by slice. Early slices may be build-only
because they add contracts and endpoint skeletons without changing behavior.
Slices that move runtime behavior must add tests or a repeatable smoke path.

## Test Plan

| Layer | Cases |
| --- | --- |
| Unit | Workflow transition rules once extracted or moved. |
| Integration | API calls Worker internal workflow endpoints; Worker returns accepted/rejected runtime results. |
| E2E | Web run workflow still works through public API after each behavior-moving slice. |
| Platform | API and Worker can still start with existing configuration. |
| Performance | No new high-frequency polling in API or Web. |
| Logs/Audit | Runtime commands include workflow id, robot id, operator/role, and result. |

## Fixtures

Use existing demo/simulation data where possible:

- Robot id: `AGV-01`
- Existing workflow definitions in PostgreSQL, or seeded test workflow created
  through API.
- Fake Worker endpoint response for Slice 2 if no real runtime behavior is
  enabled yet.

## Commands

Run from repo root after code slices:

```text
dotnet build NewAGV.sln
```

If tests are added:

```text
dotnet test NewAGV.sln
```

## Acceptance Evidence

Slice 1 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 2 warnings) after Codex review.
- **Files changed**:
  - `src/NewAGV.Contracts/WorkflowContracts.cs` - Added DTOs: `WorkerWorkflowRuntimeOutcome` enum, `WorkerWorkflowStartRequest`, `WorkerWorkflowControlRequest`, `WorkerWorkflowRuntimeResult`, `WorkerWorkflowRuntimeStatus`.
  - `src/NewAGV.Worker/Program.cs` - Added 5 internal workflow endpoint skeletons; Codex removed unnecessary `async` from skeleton lambdas to avoid CS1998 warnings.
- **New contract DTOs compile**: Verified via successful build.
- **Worker exposes internal workflow endpoint skeletons**:
  - `POST /internal/workflows/{workflowId}/start` - Returns rejected result.
  - `POST /internal/workflows/pause` - Returns rejected result.
  - `POST /internal/workflows/resume` - Returns rejected result.
  - `POST /internal/workflows/cancel` - Returns rejected result.
  - `GET /internal/workflows/active-run?robotId=...` - Returns empty status.
- **Public Web/API behavior is unchanged**: No edits to `NewAGV.Web`, `WorkflowsController`, or `WorkflowExecutionService`.
- **No runtime command is routed through the new skeleton by default**: Endpoints return `WorkerWorkflowRuntimeOutcome.Rejected` with clear message.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Runtime logic, routing public API to Worker endpoints, database access in Worker.

Future acceptance:

- Each moved behavior has evidence in this file or the slice story.

Slice 2 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after adding API client methods.
- **Files changed**:
  - `src/NewAGV.Api/Services/SeerWorkerClient.cs` - Added internal Worker workflow client methods for start, pause, resume, cancel, and active-run status.
- **API can call Worker workflow skeleton endpoints when a future slice enables the path**:
  - `StartWorkflowAsync(WorkerWorkflowStartRequest, CancellationToken)` posts to `POST /internal/workflows/{workflowId}/start`.
  - `PauseWorkflowAsync(WorkerWorkflowControlRequest, CancellationToken)` posts to `POST /internal/workflows/pause`.
  - `ResumeWorkflowAsync(WorkerWorkflowControlRequest, CancellationToken)` posts to `POST /internal/workflows/resume`.
  - `CancelWorkflowAsync(WorkerWorkflowControlRequest, CancellationToken)` posts to `POST /internal/workflows/cancel`.
  - `GetActiveWorkflowRunAsync(string?, CancellationToken)` gets `GET /internal/workflows/active-run?robotId=...`.
- **Fallback behavior**: null Worker runtime result payloads become `WorkerWorkflowRuntimeOutcome.Rejected`; null active-run payload returns an empty `WorkerWorkflowRuntimeStatus` with the requested robot id.
- **Public Web/API behavior is unchanged**: No edits to `NewAGV.Web`, `WorkflowsController`, or `WorkflowExecutionService`.
- **No runtime command is routed through the new Worker workflow client methods by default**: Slice 2 only adds callable client methods.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Feature-flag routing, runtime logic, Worker database access, public API behavior changes.

Slice 2.5 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after adding a disabled Worker workflow runtime flag.
- **Files changed**:
  - `src/NewAGV.Api/Services/IntegrationOptions.cs` - Added `UseWorkerWorkflowRuntime` option.
  - `src/NewAGV.Api/appsettings.json` - Set `Integration:UseWorkerWorkflowRuntime` to `false`.
  - `src/NewAGV.Api/appsettings.Development.json` - Set `Integration:UseWorkerWorkflowRuntime` to `false`.
- **Public Web/API behavior is unchanged**: The flag is not read by `WorkflowsController` or `WorkflowExecutionService` yet, so the existing API-owned runtime path remains active.
- **No runtime command is routed through Worker by default**: The flag exists for a later path-selection slice and is explicitly disabled.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Feature-flag branch execution, Worker start orchestration, pause/resume/cancel Worker routing, database changes.

Slice 3 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after adding feature-flagged Worker workflow start routing.
- **Files changed**:
  - `src/NewAGV.Api/Services/WorkflowExecutionService.cs` - Injected `IntegrationOptions` and `SeerWorkerClient`; added a `UseWorkerWorkflowRuntime` guarded start path.
- **Default public behavior is unchanged**: `Integration:UseWorkerWorkflowRuntime` remains `false` in appsettings, so `ExecuteAsync` still uses the existing API-owned runtime path by default.
- **Worker start path is feature-flagged**: When enabled, API runs the existing request validation, robot checks, active workflow check, and active TaskChain check before calling `SeerWorkerClient.StartWorkflowAsync`.
- **Worker skeleton rejection is contained**: The current Worker skeleton returns `WorkerWorkflowRuntimeOutcome.Rejected`; API maps non-accepted Worker outcomes to the existing `InvalidOperationException`/BadRequest flow.
- **No Worker runtime persistence is assumed yet**: If a future Worker implementation returns `Accepted`, API requires a `RunId` and loads the run through existing query mapping.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Worker happy-path runtime orchestration, Worker database access, pause/resume/cancel Worker routing, public route shape changes, Web changes.

Slice 4A acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after adding feature-flagged Worker workflow control routing.
- **Files changed**:
  - `src/NewAGV.Api/Services/WorkflowExecutionService.cs` - Added `UseWorkerWorkflowRuntime` guarded pause, resume, and cancel paths that call `SeerWorkerClient` workflow control methods.
- **Default public behavior is unchanged**: `Integration:UseWorkerWorkflowRuntime` remains `false` in appsettings, so pause/resume/cancel still use the existing API-owned `TaskChainCoordinator` path by default.
- **Worker control path is feature-flagged**: When enabled, API still requires an active workflow run before calling Worker pause/resume/cancel skeleton endpoints.
- **Worker skeleton rejection is contained**: The current Worker skeleton returns `WorkerWorkflowRuntimeOutcome.Rejected`; API maps non-accepted Worker outcomes to the existing `InvalidOperationException`/BadRequest flow.
- **No Worker runtime persistence is assumed yet**: On future `Accepted` control results, API reloads the existing run instead of mutating API-owned state in the Worker-runtime branch.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Worker control command runtime behavior, Worker database access, public route shape changes, Web changes, default Worker routing.

Slice 4B acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after extracting Worker workflow endpoint skeleton logic into a Worker service.
- **Files changed**:
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Added Worker workflow runtime service skeleton with start, pause, resume, cancel, and active-run methods.
  - `src/NewAGV.Worker/Program.cs` - Registered `WorkerWorkflowRuntimeService` and routed internal workflow endpoints through it.
- **Worker endpoint behavior is unchanged**: Internal workflow start/pause/resume/cancel endpoints still return `WorkerWorkflowRuntimeOutcome.Rejected` with `Workflow runtime not yet implemented in Worker.`
- **Active-run skeleton behavior is unchanged**: `GET /internal/workflows/active-run` still returns an empty `WorkerWorkflowRuntimeStatus` carrying the requested robot id.
- **Public Web/API behavior is unchanged**: No edits to `NewAGV.Web`, public routes, `WorkflowsController`, database schema, or default `Integration:UseWorkerWorkflowRuntime` setting.
- **No SEER command is sent by the Worker workflow runtime skeleton**: The new service only logs skeleton requests and returns rejected or empty status responses.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).
- **Intentionally not implemented**: Worker runtime persistence, Worker happy-path start orchestration, workflow progression, pause/resume/cancel runtime behavior, default Worker routing.

Slice 5A acceptance:

- **Decision result**: Accepted `docs/decisions/0010-worker-runtime-persistence-path.md`.
- **Persistence path**: Worker workflow runtime will write PostgreSQL through a future neutral persistence layer shared by API and Worker.
- **Ownership split**: Worker owns runtime writes; API remains the public facade for workflow definition CRUD, public run queries, Web compatibility, and realtime fan-out.
- **Dependency rule**: Worker must not reference `NewAGV.Api` to reuse `NewAgvDbContext`.
- **No product code behavior changed**: Slice 5A only updates ADR/story docs.
- **No database schema changed**: Active-run guard and persistence extraction are deferred to separate implementation slices.
- **Build result**: Not required for docs-only decision slice; previous Slice 4B build remains the latest code proof.

Slice 5B acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (0 errors, 1 warning) after adding Worker workflow start/control validation skeletons.
- **Files changed**:
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Added request validation and structured logging for workflow start, pause, resume, cancel, and active-run skeleton methods.
- **Start orchestration skeleton validates intent**:
  - Empty workflow id returns `WorkerWorkflowRuntimeOutcome.ValidationFailed`.
  - Mismatched route/payload workflow id returns `WorkerWorkflowRuntimeOutcome.ValidationFailed`.
  - Empty robot id returns `WorkerWorkflowRuntimeOutcome.ValidationFailed`.
- **Control skeleton validates intent**: Empty robot id for pause/resume/cancel returns `WorkerWorkflowRuntimeOutcome.ValidationFailed`.
- **No SEER command is sent**: `WorkerWorkflowRuntimeService` still does not call `SeerTaskChainService` or any SEER TCP command service.
- **Runtime behavior remains incomplete by design**: Valid Worker workflow start/control requests still return rejected with `Workflow runtime not yet implemented in Worker.`
- **Default public behavior is unchanged**: `Integration:UseWorkerWorkflowRuntime` remains false, and no public route/Web/database schema changed.
- **Warnings**: NU1900 NuGet vulnerability-data warning for `https://api.nuget.org/v3/index.json` (environmental/package-feed warning, not code behavior).

Slice 6 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (6 projects, 0 errors, 0 warnings) after extracting the workflow EF model to a neutral persistence project.
- **Files changed**:
  - `src/NewAGV.Persistence/NewAGV.Persistence.csproj` - Added neutral persistence project with EF Core and Npgsql dependencies.
  - `src/NewAGV.Persistence/NewAgvDbContext.cs` - Moved `NewAgvDbContext` and workflow persistence entities out of `NewAGV.Api.Data` into `NewAGV.Persistence`.
  - `src/NewAGV.Api/NewAGV.Api.csproj` - Added project reference to `NewAGV.Persistence`.
  - `src/NewAGV.Api/Program.cs`, `src/NewAGV.Api/Controllers/IntegrationController.cs`, `src/NewAGV.Api/Services/DatabaseInitializationService.cs`, `src/NewAGV.Api/Services/WorkflowDefinitionService.cs`, `src/NewAGV.Api/Services/WorkflowExecutionService.cs` - Updated API references to the neutral persistence namespace.
  - `src/NewAGV.Worker/NewAGV.Worker.csproj` - Added references needed for Worker-side PostgreSQL persistence.
  - `src/NewAGV.Worker/Program.cs` - Registered `NewAgvDbContext` and changed `WorkerWorkflowRuntimeService` to scoped for future DbContext injection.
  - `src/NewAGV.Worker/appsettings.json`, `src/NewAGV.Worker/appsettings.Development.json` - Added `ConnectionStrings:NewAgvDb`.
  - `NewAGV.sln` - Added `NewAGV.Persistence`.
- **Persistence path implemented without schema change**: API and Worker can now compile against the same neutral EF model, but Worker runtime still does not write workflow state.
- **Dependency rule preserved**: Worker references `NewAGV.Persistence`, not `NewAGV.Api`.
- **Public behavior unchanged by default**: API public routes, Web files, and `Integration:UseWorkerWorkflowRuntime=false` remain unchanged.
- **No SEER command behavior changed**: Worker workflow runtime still returns skeleton rejected/validation results and does not dispatch TaskChains.

Slice 7 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (6 projects, 0 errors, 0 warnings) after adding the durable active-run guard and Worker happy-path start.
- **Files changed**:
  - `src/NewAGV.Persistence/NewAgvDbContext.cs` - Added `ux_workflow_runs_active_robot` filtered unique index for active workflow statuses.
  - `src/NewAGV.Api/Services/DatabaseInitializationService.cs` - Added PostgreSQL `CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_runs_active_robot ... WHERE "Status" IN (...)` schema upgrade statement.
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Implemented Worker workflow start persistence, first-step dispatch, active-run status lookup, and unique-guard handling.
- **User-confirmed decision gate**: Human selected PostgreSQL partial unique index for the durable active-run guard before Worker dispatches SEER TaskChain commands.
- **Duplicate active workflow guard**: API and Worker share the same `NewAgvDbContext` model, and PostgreSQL enforces at most one active workflow run per `RobotId` for `Pending`, `Validating`, `Ready`, `Starting`, `Running`, and `Paused`.
- **Run intent is durable before SEER dispatch**: Worker creates the workflow run/steps and saves the first step as `Starting` before calling `SeerTaskChainService.ExecuteTaskChainAsync`.
- **Worker happy path exists behind feature flag**: When API `Integration:UseWorkerWorkflowRuntime` is enabled, Worker can create a run, dispatch the first TaskChain step, and return `Accepted` with the run id for API to load through the existing public DTO mapping.
- **SEER rejection semantics remain API-compatible**: If the first TaskChain dispatch is rejected after run creation, Worker marks the run failed and still returns `Accepted` with `RunId`, allowing API to return the persisted failed run instead of changing the public execute route to BadRequest.
- **Active-run internal endpoint now reads persistence**: `GET /internal/workflows/active-run` returns the current active run id/status from PostgreSQL when present.
- **Default public behavior is unchanged**: `Integration:UseWorkerWorkflowRuntime` remains false, so existing API-owned runtime remains the default path.
- **Intentionally not implemented**: Worker pause/resume/cancel runtime behavior, active step monitoring/progression, timeout/retry/failure policy migration, API runtime retirement, and Web changes.

Slice 8 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (6 projects, 0 errors, 0 warnings) after moving Worker pause/resume/cancel runtime behavior into `WorkerWorkflowRuntimeService`.
- **Files changed**:
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Implemented active-run lookup and persisted state updates for pause, resume, and cancel controls.
- **Worker controls call the SEER-owning service**:
  - Pause calls `SeerTaskChainService.PauseAsync`.
  - Resume calls `SeerTaskChainService.ResumeAsync`.
  - Cancel calls `SeerTaskChainService.CancelAsync`.
- **Control state is persisted only after SEER accepts**: Rejected SEER control results return `WorkerWorkflowRuntimeOutcome.Rejected`; accepted results update the active workflow run and current step.
- **Pause/resume/cancel state mapping**:
  - Pause sets run/current step to `Paused`.
  - Resume sets run/current step to `Running`.
  - Cancel sets run/current step to `Canceled`, sets completion timestamps, and records the message.
- **No public route shape changed**: Existing API public routes and Web calls remain unchanged; API calls these Worker control endpoints only when `Integration:UseWorkerWorkflowRuntime` is enabled.
- **Default public behavior is unchanged**: `Integration:UseWorkerWorkflowRuntime` remains false, so API-owned controls remain the default path.
- **Intentionally not implemented**: Worker monitor/progression loop, timeout/retry/failure policy migration, API runtime retirement, Web changes.

Slice 9 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (6 projects, 0 errors, 0 warnings) after adding feature-flagged Worker workflow monitoring/progression.
- **Files changed**:
  - `src/NewAGV.Worker/Services/WorkerIntegrationOptions.cs` - Added Worker-side `Integration:UseWorkerWorkflowRuntime` option.
  - `src/NewAGV.Worker/Services/WorkerWorkflowMonitorService.cs` - Added background monitor that runs only when Worker workflow runtime is enabled.
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Added active-run monitoring, TaskChain status polling, step progression, timeout handling, retry/continue/manual-resume failure policies, and dispatch of next pending steps.
  - `src/NewAGV.Worker/Program.cs` - Registered Worker workflow monitor options and hosted service.
  - `src/NewAGV.Worker/appsettings.json`, `src/NewAGV.Worker/appsettings.Development.json` - Added `Integration:UseWorkerWorkflowRuntime=false`.
  - `src/NewAGV.Api/Services/WorkflowExecutionService.cs` - API monitor returns early when API is configured to route workflow runtime to Worker.
- **Ownership guard**: API monitor does not advance workflow runs when `Integration:UseWorkerWorkflowRuntime=true`, preventing API and Worker from owning progression at the same time.
- **Worker monitor is opt-in**: Worker monitor is disabled by default to avoid touching API-owned runtime runs while the default API runtime remains active.
- **Worker progression behavior**:
  - Polls SEER TaskChain status for the active workflow step.
  - Updates step/run in-progress statuses.
  - Marks completed steps and dispatches the next pending step.
  - Applies timeout, retry, continue-workflow, manual-resume, and stop-workflow policies.
  - Avoids overwriting `Info` attempt metadata so retry accounting remains stable.
- **Default public behavior is unchanged**: Both API and Worker runtime flags remain false in appsettings, so existing API-owned runtime remains default.
- **Intentionally not implemented**: API SignalR bridge for Worker runtime updates, Worker restart reconciliation beyond persisted status polling, API-owned runtime removal, Web changes.

Slice 10 acceptance:

- **Build result**: `dotnet build NewAGV.sln` completed successfully (6 projects, 0 errors, 0 warnings) after adding the Worker-to-API workflow realtime bridge.
- **Files changed**:
  - `src/NewAGV.Contracts/WorkflowContracts.cs` - Added `InternalWorkflowRunUpdate`.
  - `src/NewAGV.Api/Controllers/InternalSyncController.cs` - Added `POST /internal/sync/workflow` to fan out Worker workflow updates through existing SignalR `ReceiveTelemetry`.
  - `src/NewAGV.Worker/Services/ApiSyncClient.cs` - Added `PushWorkflowAsync`.
  - `src/NewAGV.Worker/Services/WorkerWorkflowRuntimeService.cs` - Emits workflow started, step started, updated, step completed, completed, failed, and canceled events to API after persisted state changes.
- **Realtime compatibility**: Worker-owned runtime updates use existing `RealtimeEvent` and `WorkflowRunDto`; no Web code or public SignalR client contract changed.
- **Internal API only**: The new endpoint is under existing `internal/sync` and does not change public `WorkflowsController` route shape.
- **Runtime safety**: API sync failures are handled by existing `ApiSyncClient` logging behavior and do not cause Worker to retry side-effecting SEER commands.
- **Default public behavior is unchanged**: API and Worker runtime flags remain false in appsettings, so current API-owned runtime remains default.
- **Intentionally not implemented**: Enabling Worker runtime by default, removing API-owned runtime code, and live SEER/PostgreSQL smoke proof.

Slice 11 closure:

- **Human final activation decision**: Selected `Keep feature flag off`.
- **Build result**: `dotnet build NewAGV.sln` must remain the final validation command for closure.
- **Runtime ownership implemented behind flags**:
  - API workflow start/pause/resume/cancel can route to Worker when API `Integration:UseWorkerWorkflowRuntime=true`.
  - Worker workflow runtime can create durable runs, dispatch first and later TaskChain steps, monitor status, apply timeout/retry/continue/manual-resume policies, perform pause/resume/cancel controls, and sync workflow realtime updates back to API when Worker `Integration:UseWorkerWorkflowRuntime=true`.
- **Default runtime remains API-owned by explicit decision**:
  - `src/NewAGV.Api/appsettings.json` keeps `Integration:UseWorkerWorkflowRuntime=false`.
  - `src/NewAGV.Api/appsettings.Development.json` keeps `Integration:UseWorkerWorkflowRuntime=false`.
  - `src/NewAGV.Worker/appsettings.json` keeps `Integration:UseWorkerWorkflowRuntime=false`.
  - `src/NewAGV.Worker/appsettings.Development.json` keeps `Integration:UseWorkerWorkflowRuntime=false`.
- **Reason default is not switched in US-004**: No live PostgreSQL/SEER smoke proof was available in this session, and default activation would change workflow/public behavior.
- **Retained compatibility**: API-owned runtime remains available as the default/fallback path; public `WorkflowsController` route shape and `NewAGV.Web` behavior are unchanged.
- **Completion boundary**: Worker default activation, API-owned runtime removal, deeper restart reconciliation, and live SEER/PostgreSQL smoke validation are deferred follow-up work, not part of this completed US-004 closure.
