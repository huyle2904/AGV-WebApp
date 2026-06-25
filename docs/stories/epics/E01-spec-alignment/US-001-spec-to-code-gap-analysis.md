# US-001 SPEC-to-Code Gap Analysis

## Status

implemented

## Lane

normal

## Product Contract

NewAGV must have a documented bridge from `SPEC.md` to the current C# Blazor
codebase so future work can classify existing modules as keep, fix, add,
refactor, split, delete, or defer before implementation begins.

## Relevant Product Docs

- `SPEC.md`
- `docs/product/spec-gap-analysis.md`

## Acceptance Criteria

- The analysis identifies current Web, API, Worker, Contracts, and database
  modules relevant to `SPEC.md`.
- The analysis maps major SPEC requirements to current implementation status.
- The analysis names concrete next stories before code refactor begins.
- The analysis distinguishes existing useful UI from deeper system-design gaps.

## Design Notes

- Commands: none.
- Queries: current code audit and Harness matrix.
- API: no route changes.
- Tables: no product schema changes.
- Domain rules: no behavior changes.
- UI surfaces: no UI changes.

## Validation

When updating durable proof status, use numeric booleans:
`scripts/bin/harness-cli story update --id <id> --unit 1 --integration 1 --e2e 0 --platform 0`.

| Layer | Expected proof |
| --- | --- |
| Unit | Not applicable; documentation audit only. |
| Integration | Not applicable; no code execution path changed. |
| E2E | Not applicable. |
| Platform | Not applicable. |
| Release | Review `docs/product/spec-gap-analysis.md` before slicing implementation stories. |

## Harness Delta

Added a product planning artifact that can seed future story rows in the Harness
matrix.

## Evidence

- Created `docs/product/spec-gap-analysis.md`.
- Audited representative files across `NewAGV.Web`, `NewAGV.Api`,
  `NewAGV.Worker`, and `NewAGV.Contracts`.
