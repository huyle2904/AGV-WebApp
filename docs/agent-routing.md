# Agent Routing Guide

Status: active  
Applies to: NewAGV development with Codex Desktop and OpenCode/Mimo v2.5

## Purpose

This guide defines how to route work between:

- Codex Desktop / GPT 5.5 for reasoning, architecture, planning, review, and
  Harness records.
- OpenCode / Mimo v2.5 for bounded code-editing slices.

The goal is to keep code changes fast without letting implementation models make
architecture decisions implicitly.

## Default Roles

| Agent | Use for | Do not use for |
| --- | --- | --- |
| Codex Desktop / GPT 5.5 | SPEC analysis, gap analysis, ADRs, story slicing, high-risk design, review, Harness trace/matrix updates | Large mechanical edits when a bounded implementation slice is already clear |
| OpenCode / Mimo v2.5 | Small scoped code edits, additive DTOs, endpoint skeletons, UI/CSS fixes, mechanical refactors, simple tests | Architecture decisions, broad refactors, public contract changes without a story, Harness ownership |

## Routing Rules

Use Codex Desktop / GPT 5.5 when the work touches:

- Architecture direction.
- Database design or migrations.
- Workflow runtime ownership.
- API/Worker responsibility.
- Safety gates or SEER command behavior.
- Public API contract.
- Multi-file refactor with uncertain behavior.
- Story creation, ADR creation, or Harness records.

Use Mimo v2.5 only when:

- A story packet or handoff file already exists.
- The allowed and forbidden files are listed.
- The goal is one bounded slice.
- Validation command is specified.
- Public behavior changes are either forbidden or explicitly described.

## Standard Flow

1. Codex creates or updates the story packet.
2. Codex writes a `mimo-handoff.md` file for the current slice.
3. User opens OpenCode and selects Mimo v2.5.
4. Mimo reads the handoff and implements only that slice.
5. Mimo runs the requested build/test command.
6. Mimo updates only the allowed evidence section, usually `validation.md`.
7. User returns to Codex Desktop and asks for review.
8. Codex reviews the diff, checks scope, runs validation if needed, updates
   Harness records, and decides the next slice.

## Mimo Prompt Template

Use this pattern in OpenCode:

```text
Read <path-to-mimo-handoff.md> and implement only the requested slice.

Do not make architecture decisions.
Do not change public behavior unless the handoff explicitly says so.
Do not edit files outside the allowed list.
Run the validation command from the handoff.

After coding:
- update only the evidence location named in the handoff
- do not run harness-cli
- do not mark the story complete

Report:
- files changed
- validation command
- validation result
- skipped work
- any uncertainty
```

## Mimo Output Requirements

Mimo should report:

- Files changed.
- Build/test command run.
- Build/test result.
- Any warnings or errors.
- Work intentionally not implemented.
- Any ambiguity or blocked point.

Mimo may update:

- The slice's `validation.md` acceptance evidence section, if the handoff allows
  it.

Mimo must not update:

- Harness CLI records.
- Story final status.
- ADR status.
- Gap analysis conclusions.
- Product source-of-truth docs, unless the handoff explicitly allows it.

## Codex Review Checklist

After Mimo finishes, Codex should check:

- Did Mimo edit only allowed files?
- Did public Web/API behavior remain stable when required?
- Did Mimo avoid architecture decisions?
- Did build/test pass?
- Did Mimo update evidence in the allowed place only?
- Does the patch match the story acceptance criteria?
- Should the story remain `in_progress`, move to `implemented`, or spawn the
  next slice?

Codex then updates:

- `validation.md` if evidence needs correction.
- Harness story status/proof via `scripts/bin/harness-cli`.
- Harness trace.
- Backlog if friction or missing capability appears.

## Current Active Handoff

Current story:

```text
docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/
```

Current Mimo handoff:

```text
docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/mimo-handoff.md
```

Current OpenCode prompt:

```text
Read docs/stories/epics/E02-workflow-runtime/US-004-worker-runtime-migration/mimo-handoff.md and implement only Slice 1.

Do not change public Web/API behavior.
Do not move runtime logic.
Only add additive contracts and Worker internal endpoint skeletons.
Run dotnet build NewAGV.sln and report files changed plus result.
```

## Stop Conditions

Stop and return to Codex Desktop before coding if:

- The handoff is missing or unclear.
- The slice requires editing forbidden files.
- The implementation needs a new database migration not listed in the handoff.
- The implementation would change public routes, DTO meanings, or UI behavior.
- There is a risk of sending duplicate commands to SEER AGV.
- Build/test requires new external services or credentials.
