# 0009 Workflow Runtime Owned by Worker

Date: 2026-06-25

## Status

Accepted

## Context

`SPEC.md` states that Worker should host the workflow runtime because Worker is
the process that owns the TCP connection to the SEER AGV.

The current codebase already has a Worker process that owns SEER TCP through:

- `src/NewAGV.Worker/Services/SeerTcpClient.cs`
- `src/NewAGV.Worker/Services/SeerCommandService.cs`
- `src/NewAGV.Worker/Services/SeerTaskChainService.cs`
- `src/NewAGV.Worker/SeerIntegrationWorker.cs`

However, workflow execution currently lives in the API module:

- `src/NewAGV.Api/Services/WorkflowExecutionService.cs`
- `src/NewAGV.Api/Services/WorkflowMonitorService.cs`

This means API currently owns important runtime behavior such as:

- Selecting the next workflow step.
- Dispatching taskchains.
- Monitoring active runs.
- Applying timeout/retry/manual-resume behavior.
- Marking workflow execution terminal states.

The open question was whether to keep this runtime in API temporarily or refactor
it into Worker now to match the intended system design.

## Decision

Workflow runtime ownership will move to Worker.

API remains the public facade and safety gate for user requests. API will:

- Validate public requests.
- Enforce user-facing authorization and command preconditions.
- Persist/query workflow definitions and execution records through the product
  database or a defined internal interface.
- Expose public REST/SignalR surfaces to Web.
- Send workflow runtime commands to Worker.

Worker will own workflow execution progression. Worker will:

- Start workflow runs after API accepts a run request.
- Dispatch taskchain steps to SEER.
- Monitor taskchain runtime state.
- Apply timeout, retry, fallback, manual intervention, and reconciliation
  policies.
- Own uncertain side-effect handling and no-blind-retry behavior.
- Reconcile after Worker restart, API restart, TCP disconnect, or unknown SEER
  task result.

## Alternatives Considered

1. Keep workflow runtime in API.
   - Pros: smaller immediate code change because current implementation already
     works this way.
   - Rejected because API would continue to mix public facade, persistence,
     realtime emission, AGV runtime orchestration, and failure policy in one
     shallow module.

2. Split runtime between API and Worker.
   - Pros: allows gradual migration.
   - Rejected as the target design because split ownership creates ambiguous
     responsibility for retries, fallback, reconciliation, and active execution
     locks.

3. Build a generic BPMN-style engine.
   - Rejected because MVP only needs sequence, timeout, retry, fallback, and
     manual intervention.

## Consequences

Positive:

- Runtime behavior is colocated with the only module that can send SEER TCP
  commands.
- Retry/fallback/reconciliation can be handled with better locality.
- API becomes a clearer public facade instead of the workflow engine.
- Future Worker restart/reconciliation behavior has an obvious owner.

Tradeoffs:

- Requires a careful migration plan because current UI/API already depend on
  `WorkflowExecutionService`.
- Requires an internal API or message contract between API and Worker for
  workflow runtime commands and status updates.
- Requires persistence rules that both API and Worker can use safely without
  creating duplicate active executions.

## Follow-Up

- Create a high-risk story for the workflow runtime migration plan before code
  changes.
- Define the API-to-Worker internal workflow command contract.
- Define execution persistence ownership: direct Worker database access vs API
  internal persistence endpoints.
- Add a durable active-execution guard for one active workflow per AGV.
- Add workflow attempt/event tables before implementing advanced fallback.
- Keep the existing UI and public API behavior stable during migration.
