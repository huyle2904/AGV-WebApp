# AGENTS.md

## Purpose

NewAGV is AGV monitoring and control for SEER robots.
Runtime flow: `NewAGV.Web -> NewAGV.Api -> NewAGV.Worker -> SEER AGV`.
Prefer small, reviewable changes over broad refactors.

## Source Of Truth

- `README.md` for local run flow.
- `docs/ARCHITECTURE.md` for architecture, SEER integration, and safety.
- `docs/ROADMAP.md` for current scope.
- `docs/CHANGELOG.md` for history.

## Working Style

- Be concise and token-efficient.
- Read the minimum necessary context first.
- Prefer targeted reads over broad scans.
- Avoid logs, large docs, generated files, and unrelated files unless needed.
- Stop once there is enough context to make a safe change.

## High Risk

Treat these as high-risk:

- command safety and dispatch policy
- Teleop, relocation, goto station, and control-owner behavior
- SEER payload shape and TCP integration details
- map data, station validity, and route readiness

Raw AGV station data is not a validated route target unless code proves it.

## Code Changes

- Make the smallest safe change; prefer correctness over minimalism.
- Check related contracts, config, and affected Web/API/Worker flow when behavior changes.
- Do not add unnecessary comments, docs, or refactors.
- Do not revert user changes you did not make.

## Validation

- Run the smallest relevant verification.
- Do not skip important validation for risky changes.
- If verification was not possible, say so briefly.

## Response Style

- Respond briefly, but with enough detail to act on.
- Use short paragraphs.
- Ask at most one short clarifying question if blocked or if consequences matter.
