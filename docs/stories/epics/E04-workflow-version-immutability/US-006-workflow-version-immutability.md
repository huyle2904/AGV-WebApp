# US-006 Workflow Version Immutability

## Status

implemented

## Lane

normal

## Product Contract

Published workflow versions must not be mutated in place. Editing a published
workflow must create a new draft/version, and workflow executions must keep
referencing the exact version snapshot they started with.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`
- `docs/decisions/0008-product-database-postgresql.md`

## Acceptance Criteria

- Published workflow records cannot be mutated in place through update/step
  replace paths.
- Editing a published workflow creates a new draft/version while preserving
  the original published record.
- Executions continue to reference the workflow version snapshot they started
  with.
- Existing Web routes and controller shapes remain compatible.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Deferred until test project exists. |
| Integration | Build plus workflow service/controller code path proving publish/edit immutability. |
| E2E | Not required for this slice. |
| Platform | `dotnet build NewAGV.sln` |

## Evidence

Slice 1 result:

- Removed unique index from workflow name model so published workflow draft
  cloning can reuse the same name.
- Updated workflow persistence schema bootstrap to drop the old unique index at
  startup.
- Updated `WorkflowDefinitionService` so published workflows are cloned into
  new draft records on edit/step replace instead of being mutated in place.
- Updated `WorkflowDefinitionService` duplicate flow to create new versioned
  drafts.
- Updated `NewAGV.Web` save path to follow the new workflow id returned by API
  when a published workflow is edited.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.

Slice 2 result:

- Confirmed `WorkflowExecutionService` already persists workflow definition id
  and version on execution snapshots, so exact-version reference remains intact
  after workflow cloning.
- Validation: `dotnet build NewAGV.sln` passed with 6 projects, 0 errors, 0
  warnings.
