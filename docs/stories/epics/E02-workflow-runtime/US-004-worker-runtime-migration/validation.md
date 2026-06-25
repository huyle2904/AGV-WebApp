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

- Solution builds.
- New contract DTOs compile.
- Worker exposes internal workflow endpoint skeletons.
- Public Web/API behavior is unchanged.
- No runtime command is routed through the new skeleton by default.

Future acceptance:

- Each moved behavior has evidence in this file or the slice story.
