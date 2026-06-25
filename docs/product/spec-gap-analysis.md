# SPEC-to-Code Gap Analysis

Status: initial audit  
Source spec: `SPEC.md`  
Codebase snapshot: current `src/NewAGV.*` projects  
Harness story: `US-001`

## Purpose

This document turns `SPEC.md` into an actionable map of the current codebase.
It answers three questions for each product area:

- What already exists and should be kept?
- What exists but needs refactor before it becomes reliable product behavior?
- What is missing and should become a future story?

The goal is not to restart the project. The current UI and AGV integration have
useful work already. The goal is to protect that work by aligning it with a
clear system shape.

## Classification

Use these labels when creating follow-up stories:

| Label | Meaning |
| --- | --- |
| Keep | Good enough for the next phase; do not churn it without a concrete reason. |
| Fix | Narrow bug or mismatch that can be corrected in place. |
| Add | Missing behavior from `SPEC.md`. |
| Refactor | Behavior exists, but the module/interface is in the wrong shape or place. |
| Split | A module is too wide and needs smaller product-facing modules. |
| Delete | Remove obsolete or misleading behavior. |
| Defer | Valid future work, but outside the current MVP phase. |

Architectural vocabulary used below:

- Module: anything with an interface and implementation.
- Interface: everything callers must know to use a module correctly.
- Seam: the place where a module's interface lives.
- Adapter: a concrete implementation at a seam.
- Depth: leverage behind a small interface.
- Locality: change and verification concentrated in one place.

## Executive Summary

NewAGV already has more than a prototype:

- Blazor pages exist for Map, TaskChains, Workflow, Commands, and Home.
- API controllers exist for fleet, map, taskchains, workflows, commands, audit,
  integration, and internal worker sync.
- Worker process exists and talks to SEER over TCP through `SeerTcpClient`.
- SignalR telemetry exists through `TelemetryHub` and `TelemetryClientService`.
- Workflow persistence exists for definitions, steps, runs, and run steps.
- Workflow builder and runtime UI are already substantial.

The main gaps are not visual. They are system-design gaps:

1. Database use is partial. Workflow data is persisted, but map metadata,
   taskchain catalog/sync state, audit, command attempts, and product events are
   still mostly in memory.
2. Workflow runtime currently lives in the API module. `SPEC.md` wants Worker to
   host runtime ownership for AGV execution. This is the largest architectural
   mismatch.
3. Taskchain and map sync lack durable snapshots, versions, stale/missing
   states, and sync history.
4. Published workflow versions are mutable today. `SPEC.md` expects published
   workflow versions to be immutable.
5. Realtime events exist, but lack sequence numbers, schema versioning, and a
   strong reconnect/snapshot contract.
6. Contracts are broad and mix product DTOs, SEER payloads, realtime events, and
   workflow models in one module. This lowers locality.
7. No test projects are present. Domain workflow behavior and SEER protocol
   parsing are hard to trust without tests.

## Current Module Inventory

| Area | Current modules/files | Notes |
| --- | --- | --- |
| Web shell | `src/NewAGV.Web/Components/Layout/*`, `NavMenu.razor`, `App.razor` | Existing operations shell is usable. |
| Map UI | `Map.razor`, `MapCanvas.razor`, `wwwroot/js/agvMap.js`, `MapBackgroundService` | Good visual base; data model needs hardening. |
| TaskChain UI | `TaskChains.razor`, `PlantStateService`, `AgvApiClient` | Strong operational surface; catalog is not durable. |
| Workflow UI | `Workflow.razor`, `AgvApiClient`, `TelemetryClientService` | Good builder/runtime surface; needs version/fallback model support. |
| Public API | `FleetController`, `MapController`, `TaskChainsController`, `WorkflowsController`, `CommandsController`, `AuditController` | Surfaces mostly exist, but some routes and responsibilities differ from SPEC. |
| Internal sync | `InternalSyncController`, `ApiSyncClient` | Worker pushes state to API; useful seam. |
| Realtime | `TelemetryHub`, `RealtimeEvent`, `PlantStateService` | Works as event push, but lacks sequence/schema contract. |
| Workflow persistence | `NewAgvDbContext`, `WorkflowDefinitionService`, `WorkflowExecutionService`, `WorkflowValidationService` | Good start; needs version immutability and runtime ownership decision. |
| Taskchain runtime | `TaskChainCoordinator`, `TaskChainStore`, `SeerWorkerClient`, `SeerTaskChainService` | Useful adapter chain; current store is in memory. |
| AGV TCP | `SeerTcpClient`, `SeerRobotMapper`, `SeerCommandService`, `SeerTaskChainService` | Worker owns TCP; keep this direction. |
| Contracts | `DomainModels.cs`, `WorkflowContracts.cs` | Too broad; split after behavior is stabilized. |

## Gap Matrix

| ID | SPEC requirement | Current implementation | Status | Action | Risk | Suggested story |
| --- | --- | --- | --- | --- | --- | --- |
| ARCH-001 | Web must never talk directly to AGV; Web calls API only. | Web uses `AgvApiClient`; Worker owns TCP through `SeerTcpClient`. | Implemented | Keep | normal | Keep as invariant in future stories. |
| ARCH-002 | API is facade and safety gate. | `TaskChainCoordinator` and `CommandDispatcher` perform checks; `CommandsController` also exposes direct relocate/teleop via `SeerWorkerClient`. | Partial | Refactor | high-risk | Audit command safety gate and standardize all command paths. |
| ARCH-003 | Worker is single AGV connection owner. | Worker owns TCP, but workflow runtime is in API and calls Worker per step. | Partial | Refactor | high-risk | Decide API-vs-Worker workflow runtime ownership. |
| ARCH-004 | Domain/application rules separated from infrastructure. | Workflow rules live in API services with EF, SignalR, stores, and Worker calls mixed together. | Partial | Refactor | normal | Extract workflow rule module with a smaller test surface. |
| DB-001 | Database is required for MVP workflow behavior. | `NewAgvDbContext` exists and persists workflow definitions/runs. | Partial | Keep + Add | normal | Add missing product tables in phases. |
| DB-002 | SQLite for single-machine pilot, PostgreSQL/SQL Server for production. | API currently uses Npgsql and `DatabaseInitializationService` emits PostgreSQL-specific SQL. | Mismatch | Decide/Fix | normal | ADR: database provider for pilot and migration path. |
| DB-003 | Persist workflow definitions, versions, executions, step executions, events. | Definitions, steps, runs, run steps exist. Events/attempts are not first-class tables. | Partial | Add | normal | Add execution event/attempt model after workflow runtime decision. |
| DB-004 | Persist taskchain cache/snapshot and sync history. | `TaskChainStore` keeps catalog/recent runs in memory. | Missing | Add | normal | Durable taskchain catalog and sync status. |
| DB-005 | Persist map metadata, map versions, station snapshots. | `AgvPlantStore` keeps map entities in memory; map background is local web config. | Missing | Add | normal | Durable map/station snapshot model. |
| DB-006 | Persist audit/operator actions. | `AgvPlantStore` keeps last 250 audit entries in memory. | Partial | Add | normal | Durable audit/event log. |
| MAP-001 | Display map, stations, and robot pose. | `Map.razor`, `MapCanvas.razor`, `agvMap.js` render map entities and robots. | Partial | Keep + Fix | normal | Stabilize map data contract and render fixtures. |
| MAP-002 | Load maps from RoboshopPRO/AGV and distinguish static map from realtime pose. | Worker polls map/stations via SEER commands 1300/1301 and pushes station entities; no durable map package/version. | Partial | Add | normal | Map sync pipeline with metadata/version. |
| MAP-003 | Show route/current path realtime. | `RobotTelemetryDetail.Navigation` has finished/unfinished path; Map UI currently shows route as draft and `ShowPaths=false`. | Partial | Add | normal | Render current path from telemetry. |
| MAP-004 | Coordinate transform and map asset handling. | `MapBackgroundService` and JS render exist, but no documented transform/fixture proof. | Partial | Refactor | normal | Add map transform contract and test fixture. |
| TC-001 | Load taskchains from AGV. | `SeerTaskChainService.GetTaskChainsAsync` calls SEER 3115 through Worker. | Implemented | Keep | normal | Keep adapter; add durability. |
| TC-002 | Taskchain catalog should have stale/missing/sync states. | Catalog is replaced in memory; missing taskchains are not marked durable `MissingFromSource`. | Missing | Add | normal | Durable taskchain sync state. |
| TC-003 | Execute single taskchains with safety gate. | `TaskChainCoordinator` checks robot, confirmation, active run, e-stop, control owner, alarm, localization. | Partial | Keep + Fix | high-risk | Safety gate review and tests. |
| WF-001 | Create/edit workflow from taskchains. | `Workflow.razor`, `WorkflowsController`, `WorkflowDefinitionService` support CRUD and steps. | Partial | Keep + Fix | normal | Align workflow definition model with SPEC. |
| WF-002 | Published workflow versions must be immutable. | `UpdateWorkflowAsync` and `ReplaceStepsAsync` can mutate an existing workflow regardless of `IsPublished`. | Missing | Refactor | normal | Versioned workflow definitions. |
| WF-003 | Workflow execution history must be durable. | `WorkflowRunEntity` and `WorkflowRunStepEntity` persist run/step state. | Partial | Keep + Add | normal | Add attempt/event rows before advanced failure handling. |
| WF-004 | Failure policy includes retry, timeout, manual intervention, fallback sequence. | Retry/timeout/manual-pause-ish behavior exists; fallback sequence does not. Retry overwrites same step state. | Partial | Add + Refactor | normal | Explicit attempt/fallback model. |
| WF-005 | Only one active workflow per AGV. | `WorkflowExecutionService` checks active run before start. No DB-level unique/lock constraint is visible. | Partial | Fix | high-risk | Add transactional active-run guard. |
| WF-006 | Workflow runtime hosted by Worker. | Runtime state machine and monitor are in API (`WorkflowExecutionService`, `WorkflowMonitorService`). | Mismatch | Decide/Refactor | high-risk | ADR: workflow runtime ownership. |
| WF-007 | Reconciliation after Worker restart/disconnect. | No explicit `AwaitingReconciliation` status; Worker restart behavior is not persisted as a workflow state. | Missing | Add | high-risk | Reconciliation state and protocol. |
| RT-001 | Realtime updates via SignalR plus REST snapshot after reconnect. | SignalR exists; `PlantStateService.EnsureInitializedAsync` fetches REST snapshot before hooking events. | Partial | Keep + Fix | normal | Define reconnect contract and sequence. |
| RT-002 | Realtime events have sequence number and timestamp. | `RealtimeEvent` has timestamp but no sequence/schema version. | Missing | Add | normal | Realtime event envelope v1. |
| API-001 | Public routes should match command/query shape from SPEC. | Existing routes are close but differ: `api/map/entities`, `api/workflows/{id}/execute`, global pause/resume/cancel. | Partial | Fix | normal | API convention alignment. |
| API-002 | Parse-first boundary rule. | Worker parses JSON payloads into contracts, but raw SEER mapping and API validation are mostly inline. | Partial | Refactor | normal | Boundary parser/mapper tests. |
| SEC-001 | Auth/RBAC eventually required. | Uses `X-Demo-Role` via `RequestRoleExtensions`; no auth. | Deferred | Defer | high-risk | Phase 7 RBAC/auth. |
| OBS-001 | Product audit and operational logs separated. | Audit entries are product-like but in memory; logging exists via .NET logging. | Partial | Add | normal | Durable audit/event log. |
| TEST-001 | Unit/integration/E2E tests. | No test projects found. | Missing | Add | normal | Add test projects starting with workflow validation. |

## Keep List

Keep these unless a follow-up story proves they block a SPEC requirement:

- `NewAGV.Web` page shell and visual surfaces. The Map, TaskChains, and Workflow
  pages already give operators a useful experience.
- `PlantStateService` as the Web state module. It centralizes REST snapshot plus
  SignalR updates and has good locality for UI state.
- `SeerTcpClient` as the Worker TCP adapter. The Worker is already the only
  module opening SEER TCP connections.
- `SeerRobotMapper` as the first SEER-to-contract adapter. It should gain tests,
  not be replaced wholesale.
- `TaskChainCoordinator` safety checks as initial policy. They should be moved
  behind a deeper interface or tested, not thrown away.
- Existing workflow tables as a starting point. They need versioning/events, but
  the current model is a useful seed.

## Refactor Candidates

### 1. Deepen the Workflow Runtime Module

Recommendation: Strong

Files:

- `src/NewAGV.Api/Services/WorkflowExecutionService.cs`
- `src/NewAGV.Api/Services/WorkflowMonitorService.cs`
- `src/NewAGV.Api/Data/NewAgvDbContext.cs`
- `src/NewAGV.Contracts/WorkflowContracts.cs`

Problem:

`WorkflowExecutionService` is a shallow module with a wide interface to EF,
SignalR, taskchain coordination, plant state, validation, retry rules, and run
state transitions. Bugs in retry, fallback, reconciliation, and one-active-run
logic will spread across API/database/Worker concerns.

Solution:

Create a deeper workflow runtime interface after an ADR decides ownership:

- If runtime stays in API for the next phase, extract pure transition logic and
  execution persistence into testable modules.
- If runtime moves to Worker per `SPEC.md`, API becomes command/query facade and
  Worker owns execution progression.

Expected leverage:

- One test surface for workflow state transitions.
- Attempt/fallback/reconciliation can be added without editing UI/API callers.
- One place to enforce terminal state and retry rules.

### 2. Make Sync Catalogs Durable

Recommendation: Strong

Files:

- `src/NewAGV.Api/Services/AgvPlantStore.cs`
- `src/NewAGV.Api/Services/TaskChainStore.cs`
- `src/NewAGV.Api/Controllers/InternalSyncController.cs`
- `src/NewAGV.Worker/SeerIntegrationWorker.cs`
- `src/NewAGV.Worker/Services/SeerTaskChainService.cs`

Problem:

Map/station/taskchain source data is synchronized from AGV but held mostly in
memory. This makes workflow validation depend on runtime cache state and makes
restart behavior weak.

Solution:

Add durable modules for:

- `MapSnapshot`
- `StationSnapshot`
- `TaskChainSnapshot`
- `SyncRun`
- `SourceState` (`Synced`, `Stale`, `Unknown`, `Missing`)

Expected leverage:

- Workflow validation can reference a stable snapshot.
- UI can show sync freshness accurately.
- Restart does not erase catalog/history.

### 3. Split Contracts by Product Surface

Recommendation: Worth exploring

Files:

- `src/NewAGV.Contracts/DomainModels.cs`
- `src/NewAGV.Contracts/WorkflowContracts.cs`

Problem:

`DomainModels.cs` mixes user roles, robot state, map entities, SEER details,
mission commands, taskchains, internal sync payloads, and realtime events. This
module has low locality: changing one product concept risks touching many
callers.

Solution:

Keep the project, but split files by interface:

- `RobotContracts.cs`
- `MapContracts.cs`
- `TaskChainContracts.cs`
- `CommandContracts.cs`
- `RealtimeContracts.cs`
- `WorkflowContracts.cs`
- `InternalSyncContracts.cs`

Expected leverage:

- Easier review and prompt targeting.
- Cleaner contract ownership.
- Lower chance of accidental cross-domain edits.

### 4. Standardize Command Safety Gate

Recommendation: Strong

Files:

- `src/NewAGV.Api/Controllers/CommandsController.cs`
- `src/NewAGV.Api/Services/CommandDispatcher.cs`
- `src/NewAGV.Api/Services/TaskChainCoordinator.cs`
- `src/NewAGV.Worker/Services/SeerCommandService.cs`
- `src/NewAGV.Worker/Services/SeerTaskChainService.cs`

Problem:

Some command paths go through `CommandDispatcher`, while relocate/teleop call
`SeerWorkerClient` directly from the controller. Taskchain execution has strong
safety checks in `TaskChainCoordinator`, but this policy is not a shared command
module.

Solution:

Define one command safety module in API and force every product command through
it before Worker dispatch.

Expected leverage:

- One place to audit command permissions and safety rules.
- Easier to add durable command attempts and write-before-send behavior.
- Fewer accidental bypasses.

### 5. Add a Realtime Envelope

Recommendation: Worth exploring

Files:

- `src/NewAGV.Contracts/DomainModels.cs`
- `src/NewAGV.Api/Controllers/InternalSyncController.cs`
- `src/NewAGV.Api/Services/WorkflowExecutionService.cs`
- `src/NewAGV.Web/Services/PlantStateService.cs`
- `src/NewAGV.Web/Services/TelemetryClientService.cs`

Problem:

Realtime events carry type and timestamp, but no sequence number, schema
version, source, aggregate id, or replay/reconnect hint.

Solution:

Introduce `RealtimeEnvelope<T>` or extend `RealtimeEvent` with:

- `Sequence`
- `SchemaVersion`
- `Source`
- `AggregateType`
- `AggregateId`
- `SnapshotRequired`

Expected leverage:

- Web can detect missed/out-of-order events.
- Reconnect behavior can be tested.
- SignalR remains push transport, not source of truth.

## Phase 1 Recommended Work

Do these before implementing new advanced features.

### US-002: Database Provider and Persistence Decision

Decision:

- Accepted in `docs/decisions/0008-product-database-postgresql.md`.
- NewAGV will use PostgreSQL as the product database.

Why first:

- Current code is already PostgreSQL-specific.
- Future map/taskchain/audit tables depend on this.

Validation:

- ADR recorded.
- Follow-up implementation should replace ad hoc schema upgrade SQL with a
  documented migration flow.

### US-003: Workflow Runtime Ownership ADR

Decision:

- Accepted in `docs/decisions/0009-workflow-runtime-owned-by-worker.md`.
- Workflow runtime will move to Worker to match `SPEC.md`.

Why first:

- This determines where retry/fallback/reconciliation belongs.
- Moving later would be more expensive after new failure policies are added.

Validation:

- ADR recorded.
- Follow-up implementation must migrate runtime behavior in small slices while
  keeping current Web/API behavior stable.

### US-004: Workflow Runtime Migration Plan

Build:

- High-risk story packet for moving workflow runtime ownership from API to
  Worker.
- Slice plan for code-editing model execution.
- Mimo handoff prompt for the first implementation slice.

Validation:

- Story packet names current behavior, target behavior, stop conditions, and
  validation.
- First code slice is bounded and does not change public Web/API behavior.

### US-005: Durable Taskchain Catalog

Build:

- Taskchain snapshot table.
- Sync timestamp/source state.
- Validation reads durable catalog, not only memory.

Validation:

- Sync updates catalog.
- Missing taskchain marks stale/missing instead of silently disappearing.
- Workflow validation fails with a clear issue when a referenced taskchain is
  missing or stale.

### US-006: Workflow Version Immutability

Build:

- Published versions cannot be mutated in place.
- Editing a published workflow creates a new draft/version.
- Executions reference the exact version snapshot.

Validation:

- Existing execution remains unchanged after editing a workflow.
- API rejects in-place mutation of a published version.

### US-007: Realtime Event Envelope

Build:

- Add sequence/schema metadata.
- Web refreshes snapshot when sequence gap is detected.

Validation:

- Simulated missed event triggers snapshot refresh.
- UI does not treat SignalR as the source of truth.

## Work Not Recommended Yet

Do not start these until Phase 1 decisions are done:

- Full fallback routing UI.
- General BPMN-like workflow engine.
- Multi-AGV ownership/leases.
- Full auth/RBAC.
- High-rate pose history storage.
- Large visual redesign of existing Web pages.

## Validation Gaps

No test projects were found. The first tests should target logic where mistakes
can send wrong commands or corrupt execution state:

1. Workflow validation.
2. Workflow status transitions.
3. Retry/timeout policy.
4. Taskchain safety gate.
5. SEER TCP framing and mapper parsing.
6. Map coordinate transform fixture.

Recommended project shape:

```text
tests/
  NewAGV.Contracts.Tests/
  NewAGV.Api.Tests/
  NewAGV.Worker.Tests/
  NewAGV.Web.Tests/
```

## Suggested Harness Story Matrix

| Story | Lane | Product docs | Proof target |
| --- | --- | --- | --- |
| US-001 SPEC-to-code gap analysis | normal | `docs/product/spec-gap-analysis.md` | Docs review |
| US-002 Database provider decision | high-risk | `docs/decisions/0008-product-database-postgresql.md` | ADR accepted |
| US-003 Workflow runtime ownership ADR | high-risk | `docs/decisions/0009-workflow-runtime-owned-by-worker.md` | ADR accepted |
| US-004 Workflow runtime migration plan | high-risk | `docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/` | Story packet review |
| US-005 Durable taskchain catalog | normal | taskchains product doc | Unit + integration |
| US-006 Workflow version immutability | normal | workflows product doc | Unit + integration |
| US-007 Realtime event envelope | normal | realtime product doc | Unit + UI reconnect test |
| US-008 Durable audit and command attempts | high-risk | commands/audit product doc | Integration |
| US-009 Map snapshot and station versioning | normal | map product doc | Unit + UI fixture |

## Next Action

`US-002` and `US-003` are now decided. `US-004` should prepare the high-risk
migration packet for moving workflow runtime ownership from API to Worker, then
hand only the first bounded implementation slice to the code-editing model.
