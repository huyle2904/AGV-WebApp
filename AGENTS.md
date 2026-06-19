# AGENTS.md

## Purpose

NewAGV is an AGV monitoring and control system for SEER robots.

Main runtime flow:

```text
NewAGV.Web -> NewAGV.Api -> NewAGV.Worker -> SEER AGV
```

The repo should favor safe, reviewable changes over broad refactors.

## Source Of Truth

- `README.md`: repo entry point and local run flow
- `docs/ARCHITECTURE.md`: architecture, SEER integration, safety principles
- `docs/ROADMAP.md`: current scope and next priorities
- `docs/CHANGELOG.md`: change history

## Working Style

- Be concise and token-efficient by default.
- Read the minimum necessary context first, then expand only if needed for correctness.
- Prefer targeted file reads over broad repository scans.
- Avoid reading logs, large docs, generated files, and unrelated files unless directly relevant.
- Stop exploring once there is enough context to make a safe, correct change.

## MCP Worker Routing

Do not use Codex for:

- bulk markdown or text reformatting
- simple extraction of dates, fields, lists, or identifiers
- low-risk summarization that will be manually reviewed
- straightforward text cleanup or rewriting with clear constraints
- classification or grouping tasks where output is easy to verify by eye

Use the `ask_worker` MCP tool for bounded, low-risk, reviewable text tasks.

Keep Codex for:

- planning and execution of real code changes
- shipped code and runtime behavior
- unfamiliar code paths
- multi-file logic changes
- contract changes across Web, Api, Worker, and Contracts
- safety-critical behavior

## Code Change Rules

- Make the smallest safe change, but prefer correctness over minimalism.
- Inspect related contracts, configuration, and affected UI/API/Worker flow when behavior changes.
- Do not add unnecessary comments, docs, or refactors unless requested.
- Do not revert user changes you did not make.

## Risk Areas

Treat these areas as high-risk:

- command safety and dispatch policy
- Teleop, relocation, goto station, and control-owner behavior
- SEER payload shape and TCP integration details
- assumptions about map data, station validity, and route readiness

Raw station data from AGV must not be assumed to be a validated route target unless the code clearly establishes that.

## Validation

- Run the smallest relevant verification possible.
- Do not skip important validation for risky changes.
- If verification was not possible, say so briefly.

## Response Style

- Respond briefly, but include enough information to be useful.
- Use short paragraphs by default.
- Ask at most one short clarifying question if blocked or if a choice has meaningful consequences.
