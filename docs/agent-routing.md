# Codex Model Routing Guide

Status: active  
Applies to: NewAGV development in Codex Desktop

## Purpose

This guide defines how to choose a Codex model tier for NewAGV work.

All work now happens inside Codex. Do not hand off code edits to external
models such as OpenCode/Mimo by default. The routing decision is only about how
much reasoning power to use for the current task.

The goal is to spend strong reasoning where mistakes would be expensive, while
using smaller models for bounded edits, validation, and documentation cleanup.

## Model Tiers

| Tier | Use for | Avoid for |
| --- | --- | --- |
| GPT 5.5 xhigh | Architecture changes, safety-critical AGV behavior, database/persistence direction, public API contract changes, workflow runtime ownership, complex refactors with unclear blast radius, incident/debugging work where root cause is unknown | Mechanical edits after the design is already locked |
| GPT 5.5 high | Story slicing, ADRs, SPEC/gap reasoning, high-risk implementation planning, review of broad diffs, migration sequencing, test strategy for behavior-moving slices | Tiny copy/docs edits, simple one-file fixes |
| GPT 5.4 low | Bounded implementation slices with a clear story, allowed files, forbidden files, and validation command; simple refactors; adding DTO/client methods; narrow UI fixes; routine build-fix loops | Architecture decisions, safety gates, DB migrations, ambiguous behavior changes |
| GPT 5.4 mini | Formatting, typo fixes, small markdown updates, simple search/summarize tasks, mechanical rename after an exact plan is written, low-risk validation reruns | Multi-file behavior changes, anything touching AGV commands, workflow runtime, database, or public API behavior |

Model names can change over time. If the exact model is unavailable, choose the
closest available model with the same reasoning posture.

## Default Routing Rules

Use GPT 5.5 xhigh when the task touches any of these hard gates:

- SEER command safety, duplicate command prevention, or AGV motion behavior.
- Workflow runtime ownership or runtime progression.
- Database schema, migrations, persistence ownership, or data loss risk.
- Public API route shape, response meaning, or Web-visible behavior.
- Auth, authorization, audit/security, or operator safety.
- A failed build/test where the cause is unclear after one quick inspection.

Use GPT 5.5 high when the task is not a hard gate but still needs design:

- Creating or updating ADRs.
- Turning SPEC intent into stories or implementation slices.
- Reviewing model-generated or broad diffs.
- Planning tests for behavior-moving workflow slices.
- Deciding whether a slice is safe to implement.
- Updating Harness policy or routing rules.

Use GPT 5.4 low when the task is already bounded:

- The relevant story exists.
- The intended behavior is clear.
- Allowed and forbidden files are known.
- Validation command is known.
- The patch should not introduce new architecture.

Use GPT 5.4 mini only for tiny tasks:

- Markdown spelling or wording cleanup.
- Renaming headings.
- Running simple commands and reporting output.
- Updating evidence text after stronger-model review.
- Searching for file locations or exact symbols.

## Harness Lane Mapping

| Harness lane | Default model | Escalate when |
| --- | --- | --- |
| Tiny | GPT 5.4 mini or GPT 5.4 low | The tiny task unexpectedly touches code behavior, public contracts, or persistence |
| Normal | GPT 5.4 low | The story is underspecified, spans multiple domains, or changes validation expectations |
| High-risk | GPT 5.5 high | Use GPT 5.5 xhigh for hard gates, safety, database, public API, or unclear runtime behavior |

Harness lane is the process risk. Model tier is the reasoning budget. If they
disagree, prefer the stronger model.

## Standard Codex Flow

1. Read `AGENTS.md` and the Harness documents listed there.
2. Record intake with `scripts/bin/harness-cli`.
3. Check the story matrix.
4. Choose the model tier from this guide.
5. For normal/high-risk work, keep the story packet current before coding.
6. Implement directly in Codex, using the repo's existing patterns.
7. Run the validation command appropriate to the slice.
8. Update validation evidence and Harness records.
9. Record a trace.

## Bounded Slice Checklist

A slice is safe for GPT 5.4 low only when all items are true:

- The expected change can be described in one paragraph.
- The files to edit are known before editing starts.
- The files not to edit are also clear.
- No new ADR is needed.
- No database migration is needed.
- No public behavior changes unless explicitly requested.
- The validation command is known.
- The fallback if validation fails is to stop and escalate, not to improvise.

If any item is false, use GPT 5.5 high or GPT 5.5 xhigh.

## Stop And Escalate Conditions

Escalate to GPT 5.5 high or xhigh before continuing if:

- A small edit reveals hidden architecture coupling.
- A build failure points to unclear ownership between API, Worker, Web, or
  Contracts.
- A patch might send duplicate commands to the AGV.
- A fix requires changing PostgreSQL schema or runtime persistence ownership.
- Existing behavior must change to satisfy the SPEC.
- The current story packet no longer matches the code reality.
- Validation proof would need to be weakened.

## New Session Prompt

Use this when starting a fresh Codex session:

```text
Work in C:\Users\TD-997\Documents\NewAGV.

Read AGENTS.md and follow Harness. Also read docs/agent-routing.md before
choosing a model tier.

All work happens in Codex now. Do not hand off to OpenCode/Mimo. Choose model
tier by risk:
- GPT 5.5 xhigh/high for architecture, workflow runtime, AGV safety, DB,
  public API, ADRs, story slicing, and review.
- GPT 5.4 low for bounded code slices with clear files and validation.
- GPT 5.4 mini for tiny docs/search/mechanical work.

Start with Harness intake, query matrix, then continue the selected story or
task.
```

## Current Project Bias

NewAGV currently has several high-risk areas:

- Workflow runtime is being moved from API to Worker.
- PostgreSQL is the accepted product database.
- The Worker owns SEER TCP integration and side-effecting AGV commands.
- Public Web/API behavior should remain stable during migration slices.

Default to GPT 5.5 high or xhigh for workflow runtime migration planning and
review. Use GPT 5.4 low only for tightly bounded implementation slices after
the plan is explicit.
