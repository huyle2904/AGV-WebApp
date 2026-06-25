# Exec Plan

## Goal

Move NewAGV workflow runtime ownership from API to Worker in small, verifiable
slices while keeping the current Blazor Web experience and public API behavior
stable.

## Scope

In scope:

- Define internal API-to-Worker workflow runtime contracts.
- Add Worker internal workflow endpoints.
- Gradually move execution progression from `WorkflowExecutionService` to Worker.
- Keep workflow definitions and run history in PostgreSQL.
- Keep Web calling API, not Worker.
- Preserve current workflow UI behavior during migration.
- Add validation and tests as slices become executable.

Out of scope:

- Full fallback routing UI.
- Multi-AGV Worker lease model.
- Auth/RBAC replacement for `X-Demo-Role`.
- Database provider change.
- Large visual redesign.
- General workflow engine/BPMN features.

## Risk Classification

Risk flags:

- Public contracts: public workflow run/pause/resume/cancel behavior must remain
  stable.
- Existing behavior: current UI/API flow already exists and must not regress.
- Data model: workflow run state is persisted.
- External systems: Worker sends side-effecting commands to SEER AGV.
- Audit/security: command ownership and runtime state must stay traceable.
- Weak proof: no test projects exist yet.
- Multi-domain: API, Worker, Contracts, database, and Web runtime state are all
  affected.

Hard gates:

- External provider behavior.
- Public command behavior.
- Data/state migration.

## Work Phases

1. Discovery.
   - Re-read `SPEC.md`, `docs/product/spec-gap-analysis.md`, and ADRs 0008/0009.
   - Inspect current workflow modules before each slice.

2. Design.
   - Define internal workflow runtime commands and responses in
     `NewAGV.Contracts`.
   - Decide the first persistence path: Worker may call API internal endpoints or
     use direct database access. Direct database access needs a separate story.

3. Validation planning.
   - Start with build-only proof for skeleton contracts/endpoints.
   - Add unit/integration tests when runtime behavior begins moving.

4. Implementation.
   - Slice 1: Add internal workflow runtime contract DTOs and Worker endpoint
     skeletons. No behavior change.
   - Slice 2: Add API internal client methods and feature flag/path selection.
     Still no behavior change by default.
   - Slice 3: Move workflow start orchestration for happy path into Worker behind
     the feature flag.
   - Slice 4: Move pause/resume/cancel runtime commands into Worker runtime
     module.
   - Slice 5: Move active step monitoring/progression into Worker.
   - Slice 6: Add reconciliation states and no-blind-retry behavior.
   - Slice 7: Remove or retire API-owned runtime progression after Worker path is
     proven.

5. Verification.
   - Build solution after each slice.
   - Run existing app smoke where feasible.
   - Add tests before or during slices that move behavior, not after the whole
     migration.

6. Harness update.
   - Update story status/proof after each slice.
   - Record friction or missing tooling in Harness backlog.

## Stop Conditions

Pause for human confirmation if:

- A slice would change public route shape used by `NewAGV.Web`.
- A slice would delete or migrate existing workflow run data.
- Worker needs direct database access and connection string/config ownership is
  unclear.
- Current behavior cannot be preserved behind a feature flag or compatibility
  path.
- Any command could be sent twice to SEER AGV because of uncertain runtime state.
- Validation has to be weakened.
- The implementation requires touching auth/RBAC or deployment topology.
