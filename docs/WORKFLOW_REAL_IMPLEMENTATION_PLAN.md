# Workflow Real Implementation Plan

## Objective

Replace the current demo-only Workflow page with a real, production-oriented workflow feature that:

- stores workflow definitions in PostgreSQL
- uses native SEER TaskChains as executable workflow steps
- executes workflows on real robots through the existing API -> Worker -> SEER flow
- shows real validation, runtime state, and history
- removes all fake/demo data from the Workflow UI

## Current State

### Already available in repo

- Workflow UI shell exists in `src/NewAGV.Web/Components/Pages/Workflow.razor`
- PostgreSQL is connected through `NewAgvDbContext`
- Workflow database entities already exist:
  - `WorkflowDefinitionEntity`
  - `WorkflowStepEntity`
  - `WorkflowRunEntity`
  - `WorkflowRunStepEntity`
- TaskChain runtime APIs already exist in `NewAGV.Api`:
  - `GET /api/taskchains`
  - `GET /api/taskchains/{name}`
  - `GET /api/taskchains/active-run`
  - `GET /api/taskchains/history`
  - `POST /api/taskchains/execute`
  - `POST /api/taskchains/pause`
  - `POST /api/taskchains/resume`
  - `POST /api/taskchains/cancel`
- TaskChain orchestration and polling already exist:
  - `TaskChainCoordinator`
  - `TaskChainMonitorService`
  - `TaskChainStore`

### Still missing

- real Workflow API
- real Workflow services
- workflow execution engine
- Web API client methods for Workflow
- replacement of hardcoded Workflow page data with live API data
- persistence-backed workflow history shown in UI
- removal of all Workflow demo placeholders

## Design Principles

- Keep the existing architecture:
  - `Web -> Api -> Worker -> SEER`
- Workflow execution must not bypass `TaskChainCoordinator`
- Workflow StepType in this phase is only `TaskChain`
- Only one active workflow per robot
- Only one active TaskChain per robot at any time
- No placeholder data that looks like real plant data
- Prefer explicit states over inferred UI hacks
- Keep changes small and reviewable

## Scope

### In scope

- CRUD workflow definitions
- save and load ordered workflow steps
- validate workflow against SEER TaskChain catalog
- execute workflow sequentially
- pause/resume/cancel workflow
- show active workflow run
- persist workflow run history
- use real DB data in Workflow page

### Out of scope for this implementation

- arbitrary step types beyond `TaskChain`
- branching / conditions / parallel steps
- drag-and-drop polish beyond what already exists
- full auth/user identity system
- migration from `EnsureCreated` to EF migrations unless explicitly requested

## Target Behavior

### Workflow definition

A workflow definition contains:

- name
- description
- version
- publish flag
- assigned robot or optional robot binding
- behavior flags
- ordered list of steps

Each workflow step contains:

- sequence
- step type = `TaskChain`
- task chain name
- display name
- timeout seconds
- retry count
- failure policy
- note
- stop on failure flag if still needed independently

### Workflow execution

When a workflow is executed:

1. API validates workflow definition
2. API validates robot safety/preflight
3. API creates a workflow run record
4. API creates workflow run step records
5. workflow engine starts the first step
6. each step calls `TaskChainCoordinator.ExecuteAsync`
7. workflow engine monitors the active TaskChain state
8. on step completion, next step starts
9. on failure, behavior follows step policy
10. final workflow status is persisted and emitted to UI

## Data Model Changes

## Task 1: Expand workflow entities

File:

- `src/NewAGV.Api/Data/NewAgvDbContext.cs`

### Update `WorkflowDefinitionEntity`

Add fields if missing:

- `AssignedRobotId` as `string?`
- `ExecutionMode` as `string` default `Sequential`
- `RequiresConfirmation` as `bool`
- `StopOnFailure` as `bool`
- `ManualResume` as `bool`

### Update `WorkflowStepEntity`

Add fields:

- `TimeoutSeconds` as `int`
- `RetryCount` as `int`
- `FailurePolicy` as `string`
- `Note` as `string?`

Keep:

- `Sequence`
- `StepType`
- `TaskChainName`
- `DisplayName`
- `ParametersJson`

### Update `WorkflowRunEntity`

Add fields:

- `CurrentStepSequence` as `int?`
- `CanceledBy` as `string?`
- `ValidationSnapshotJson` as `string?`

### Update `WorkflowRunStepEntity`

Add fields:

- `DisplayName` as `string?`
- `TimeoutSeconds` as `int`
- `RetryCount` as `int`
- `FailurePolicy` as `string`
- `Note` as `string?`
- `ProgressPercent` as `double?`
- `Info` as `string?`

Keep:

- `Status`
- `SeerTaskId`
- `StartedAt`
- `CompletedAt`
- `Message`

### Acceptance

- DB schema supports all UI fields without storing critical editor state inside hardcoded JSON only

## Contracts

## Task 2: Add Workflow contracts in `NewAGV.Contracts`

Create DTOs and requests/responses for:

### Definition DTOs

- `WorkflowSummaryDto`
- `WorkflowDetailDto`
- `WorkflowStepDto`

### Definition requests

- `CreateWorkflowRequest`
- `UpdateWorkflowRequest`
- `ReplaceWorkflowStepsRequest`
- `DuplicateWorkflowRequest`
- `PublishWorkflowRequest`

### Validation

- `WorkflowValidationResult`
- `WorkflowValidationIssue`

### Runtime

- `ExecuteWorkflowRequest`
- `WorkflowRunDto`
- `WorkflowRunStepDto`
- `WorkflowHistoryEntryDto`
- `WorkflowControlRequest`

### Enums

Add explicit enums where useful:

- `WorkflowExecutionStatus`
- `WorkflowStepExecutionStatus`
- `WorkflowFailurePolicy`

### Acceptance

- Web and API no longer need to use DB entities directly

## API Services

## Task 3: Create `WorkflowDefinitionService`

Suggested file:

- `src/NewAGV.Api/Services/WorkflowDefinitionService.cs`

Responsibilities:

- list workflows
- get workflow detail
- create workflow
- update workflow metadata
- replace ordered steps
- delete workflow
- duplicate workflow
- publish/unpublish workflow

Rules:

- name unique
- step sequence normalized on save
- do not allow delete if business rule later forbids deletion for active run

### Acceptance

- all workflow definition CRUD is DB-backed

## Task 4: Create `WorkflowValidationService`

Suggested file:

- `src/NewAGV.Api/Services/WorkflowValidationService.cs`

Responsibilities:

- validate basic metadata
- validate step list not empty
- validate sequence continuity
- validate each `TaskChainName` exists in current SEER catalog
- validate timeout/retry/failure policy bounds
- validate assigned robot if required

Validation categories:

- errors block publish and execute
- warnings allow save but may block publish depending on rule

### Acceptance

- Workflow page warning strip comes from real validation result

## Task 5: Create `WorkflowExecutionService`

Suggested file:

- `src/NewAGV.Api/Services/WorkflowExecutionService.cs`

Responsibilities:

- start workflow run
- create `WorkflowRunEntity`
- materialize `WorkflowRunStepEntity` rows from definition
- start first executable step
- move workflow from step to step
- complete/fail/cancel workflow
- map pause/resume/cancel to current active TaskChain run

Important dependency:

- use `TaskChainCoordinator`
- do not call Worker directly from workflow execution layer

### Suggested execution flow

For each workflow step:

1. mark step as `Starting`
2. call `TaskChainCoordinator.ExecuteAsync`
3. persist returned task chain run id / seer task id if available
4. poll or react to taskchain updates
5. when current taskchain completes:
   - mark workflow step complete
   - start next step
6. if taskchain fails:
   - evaluate retry/failure policy
7. if no more steps:
   - mark workflow run complete

### Acceptance

- a workflow can actually run on a robot as a sequence of TaskChains

## Task 6: Create workflow monitor background service

Suggested file:

- `src/NewAGV.Api/Services/WorkflowMonitorService.cs`

Responsibilities:

- poll all active workflow runs
- refresh current step status
- ask `TaskChainCoordinator` or DB for active taskchain state
- advance workflow automatically
- emit realtime events

Note:

- current `TaskChainMonitorService` monitors TaskChain runs
- this new service monitors Workflow runs

### Acceptance

- workflow execution continues without depending on the web page being open

## API Endpoints

## Task 7: Add `WorkflowsController`

Suggested file:

- `src/NewAGV.Api/Controllers/WorkflowsController.cs`

### Definition endpoints

- `GET /api/workflows`
- `GET /api/workflows/{id}`
- `POST /api/workflows`
- `PUT /api/workflows/{id}`
- `PUT /api/workflows/{id}/steps`
- `POST /api/workflows/{id}/duplicate`
- `POST /api/workflows/{id}/publish`
- `DELETE /api/workflows/{id}`
- `POST /api/workflows/{id}/validate`

### Runtime endpoints

- `POST /api/workflows/{id}/execute`
- `GET /api/workflows/active-run?robotId=...`
- `GET /api/workflows/history?robotId=...`
- `POST /api/workflows/pause`
- `POST /api/workflows/resume`
- `POST /api/workflows/cancel`

### Acceptance

- Workflow page can fully operate without any mock state

## Realtime

## Task 8: Emit realtime events for workflow runs

Use existing SignalR hub if possible:

- `TelemetryHub`

Add workflow event payloads:

- `workflow.started`
- `workflow.updated`
- `workflow.step.started`
- `workflow.step.completed`
- `workflow.completed`
- `workflow.failed`
- `workflow.canceled`

### Acceptance

- Workflow UI updates live during execution

## Web API Client

## Task 9: Add workflow client methods in Web

Find existing API client used by Web and extend it.

Add methods:

- `GetWorkflowsAsync`
- `GetWorkflowAsync`
- `CreateWorkflowAsync`
- `UpdateWorkflowAsync`
- `ReplaceWorkflowStepsAsync`
- `DuplicateWorkflowAsync`
- `DeleteWorkflowAsync`
- `ValidateWorkflowAsync`
- `ExecuteWorkflowAsync`
- `GetActiveWorkflowRunAsync`
- `GetWorkflowHistoryAsync`
- `PauseWorkflowAsync`
- `ResumeWorkflowAsync`
- `CancelWorkflowAsync`

### Acceptance

- Web page gets all data from API instead of local hardcoded lists

## Workflow Page Refactor

## Task 10: Remove all demo data from `Workflow.razor`

File:

- `src/NewAGV.Web/Components/Pages/Workflow.razor`

Remove hardcoded state:

- `SelectedWorkflow`
- `Workflows`
- `SelectedStep`
- `Steps`
- `History`
- hardcoded robot / operator / SEER bridge text where appropriate

Replace with API-backed state:

- workflow catalog
- selected workflow detail
- selected workflow step
- taskchain catalog
- active workflow run
- run history
- robot options
- validation result

### Acceptance

- page shows empty/loading/error states instead of fake content

## Task 11: Bind topbar to real state

Topbar fields should come from:

- selected robot
- selected workflow
- current workflow execution status
- real step count
- last saved timestamp
- dirty state
- gateway health

Rules:

- if no workflow selected, actions disabled
- if API unavailable, show safe disabled state

## Task 12: Bind catalog to real workflow definitions

Catalog behavior:

- list from DB
- search in-memory or API-side
- status badge from latest run or publication state
- `New`, `Duplicate`, `Delete` call real API

## Task 13: Bind center editor to real workflow definition

Center panel must show:

- real workflow title
- version
- description
- validation banner
- ordered workflow steps

Step actions:

- edit step
- duplicate step
- delete step
- add step

Save action:

- persists workflow metadata + steps

## Task 14: Bind inspector to real selected step

Inspector fields:

- display name
- taskchain selection from real `GET /api/taskchains`
- timeout
- retry
- failure policy
- note

TaskChain preview:

- show actual selected taskchain information if available

## Task 15: Bind history panel to real workflow run history

History should read from workflow run history endpoint, not TaskChain mock data.

Display:

- time
- workflow
- robot
- step
- result
- message

### Acceptance

- execution history only reflects persisted workflow runs

## Runtime Controls

## Task 16: Implement real `Run`, `Pause`, `Resume`, `Cancel`

### Run

- calls `POST /api/workflows/{id}/execute`
- requires selected robot and confirmation if policy requires
- creates active run

### Pause

- pauses currently active workflow on selected robot
- internally pauses active underlying taskchain

### Resume

- resumes current paused workflow step

### Cancel

- cancels current workflow run
- cancels active underlying taskchain

### Acceptance

- all toolbar actions affect real workflow state

## Demo Removal Checklist

## Task 17: Remove remaining fake placeholders

Must remove:

- hardcoded workflow catalog items
- hardcoded step cards
- hardcoded history entries
- hardcoded step preview runtime text
- hardcoded operator-like labels pretending to be live state

Allowed temporary placeholders:

- explicit empty state text such as `No workflows yet`
- loading skeletons
- explicit unavailable messages

## Configuration and Registration

## Task 18: Register new workflow services

Update:

- `src/NewAGV.Api/Program.cs`

Register:

- `WorkflowDefinitionService`
- `WorkflowValidationService`
- `WorkflowExecutionService`
- `WorkflowMonitorService`

### Acceptance

- app starts with new workflow services wired correctly

## Safety and Business Rules

## Task 19: Enforce execution rules

Before workflow execute:

- selected robot must exist
- workflow must pass validation
- robot must pass safety gate
- no other active workflow on same robot
- no conflicting active taskchain on same robot

During workflow execute:

- only one step active at a time
- next step cannot start until previous step terminal state reached

### Acceptance

- workflow execution is safe and deterministic

## Testing Plan

## Task 20: Add tests

### API/service tests

- create workflow
- update workflow
- replace steps
- validation success
- validation failure when taskchain missing
- execute workflow creates run and step rows
- step completion advances workflow
- step failure stops workflow when required
- pause/resume/cancel workflow
- reject execution when safety gate fails
- reject second active workflow on same robot

### Web tests if available

- loading state renders
- empty state renders
- API data replaces mock state
- run action disabled when invalid

### Manual verification

- create workflow in UI
- save
- reload page and confirm persistence
- run workflow on real robot or safe environment
- observe live updates and history

## Suggested Delivery Phases

## Phase 2A: Definition first

Deliver first:

- contracts
- DB entity expansion
- workflow CRUD service
- workflow validation service
- workflow controller definition endpoints
- web page load/save real definitions
- remove demo definition data

Goal:

- editor becomes fully real, execution still pending

## Phase 2B: Execution

Deliver second:

- workflow execution service
- workflow monitor service
- runtime endpoints
- run/pause/resume/cancel from UI
- real active run and history

Goal:

- workflow actually runs real TaskChains in sequence

## Phase 2C: Hardening

Deliver third:

- realtime event refinement
- retry/failure behavior cleanup
- edge-case handling
- test pass
- final demo placeholder audit

Goal:

- stable enough for plant testing

## Suggested Implementation Order For Agent

1. expand DB entities
2. add contracts
3. add workflow definition service
4. add workflow validation service
5. add definition controller endpoints
6. add web client methods
7. replace mock UI definition data
8. add workflow execution service
9. add workflow monitor service
10. add runtime endpoints
11. wire Run/Pause/Resume/Cancel in UI
12. wire history and realtime
13. remove last demo remnants
14. test and harden

## Completion Criteria

The Workflow feature is considered real when all of the following are true:

- no hardcoded workflow catalog remains in Web
- workflow definitions are stored in PostgreSQL
- steps are stored in PostgreSQL
- taskchain options come from SEER taskchain catalog
- workflow validation is real
- workflow run creates DB records
- workflow execution runs TaskChains sequentially on real robot
- workflow status and history shown in UI are real
- pause/resume/cancel work through API and TaskChainCoordinator
- the page can be refreshed without losing run/history state

