# Design

## Product Contract

Audit logs are product records, not just operational logs. A command attempt
record captures what was requested, by which role, what result was returned,
and when the attempt completed.

## Slice Plan

1. Add PostgreSQL-backed `audit_entries` and `command_attempts` entities.
2. Add an API-side audit service that writes durable rows and keeps the current
   in-memory store updated for existing realtime/UI flows.
3. Route existing command/taskchain audit writes through the audit service.
4. Keep `GET /api/audit` response type unchanged.

## Data Shape

`audit_entries` stores the current public audit row shape:

- `AuditId`
- `RobotId`
- `CommandType`
- `RequestedByRole`
- `Message`
- `Status`
- `OccurredAt`
- `Operation`

`command_attempts` stores command-level proof:

- `CommandId`
- `RobotId`
- `CommandType`
- `RequestedByRole`
- `Status`
- `Message`
- `RequestedAt`
- `CompletedAt`
- `Source`
- `TargetEntityId`
- `VelocityX`
- `VelocityY`
- `Confirmed`

## Safety Notes

This story does not make new AGV side-effecting calls. Persistence happens
after existing validation/dispatch paths produce their current result.
