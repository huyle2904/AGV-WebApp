using Microsoft.EntityFrameworkCore;
using NewAGV.Persistence;

namespace NewAGV.Api.Services;

public sealed class DatabaseInitializationService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NewAgvDbContext>();

        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            await UpgradeWorkflowSchemaAsync(dbContext, cancellationToken);
            logger.LogInformation("PostgreSQL schema is ready.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize PostgreSQL schema.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task UpgradeWorkflowSchemaAsync(NewAgvDbContext dbContext, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            "ALTER TABLE IF EXISTS app.workflow_definitions ADD COLUMN IF NOT EXISTS \"AssignedRobotId\" character varying(80);",
            "ALTER TABLE IF EXISTS app.workflow_definitions ADD COLUMN IF NOT EXISTS \"ExecutionMode\" character varying(40) NOT NULL DEFAULT 'Sequential';",
            "ALTER TABLE IF EXISTS app.workflow_definitions ADD COLUMN IF NOT EXISTS \"RequiresConfirmation\" boolean NOT NULL DEFAULT FALSE;",
            "ALTER TABLE IF EXISTS app.workflow_definitions ADD COLUMN IF NOT EXISTS \"StopOnFailure\" boolean NOT NULL DEFAULT TRUE;",
            "ALTER TABLE IF EXISTS app.workflow_definitions ADD COLUMN IF NOT EXISTS \"ManualResume\" boolean NOT NULL DEFAULT FALSE;",

            "ALTER TABLE IF EXISTS app.workflow_steps ADD COLUMN IF NOT EXISTS \"TimeoutSeconds\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_steps ADD COLUMN IF NOT EXISTS \"RetryCount\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_steps ADD COLUMN IF NOT EXISTS \"FailurePolicy\" character varying(40) NOT NULL DEFAULT 'StopWorkflow';",
            "ALTER TABLE IF EXISTS app.workflow_steps ADD COLUMN IF NOT EXISTS \"Note\" character varying(1000);",

            "ALTER TABLE IF EXISTS app.workflow_runs ADD COLUMN IF NOT EXISTS \"CurrentStepSequence\" integer;",
            "ALTER TABLE IF EXISTS app.workflow_runs ADD COLUMN IF NOT EXISTS \"CanceledBy\" character varying(80);",
            "ALTER TABLE IF EXISTS app.workflow_runs ADD COLUMN IF NOT EXISTS \"ValidationSnapshotJson\" jsonb;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_runs_active_robot ON app.workflow_runs (\"RobotId\") WHERE \"Status\" IN ('Pending', 'Validating', 'Ready', 'Starting', 'Running', 'Paused');",
            "DROP INDEX IF EXISTS app.ux_workflow_definitions_name;",
            "DROP INDEX IF EXISTS ux_workflow_definitions_name;",

            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"DisplayName\" character varying(160);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"TimeoutSeconds\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"RetryCount\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"FailurePolicy\" character varying(40) NOT NULL DEFAULT 'StopWorkflow';",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"Note\" character varying(1000);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"TaskChainRunId\" character varying(80);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"ProgressPercent\" double precision;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"Info\" character varying(1000);",

            """
            CREATE TABLE IF NOT EXISTS app.taskchain_snapshots (
                "Id" uuid PRIMARY KEY,
                "Name" character varying(120) NOT NULL,
                "ExternalId" character varying(120),
                "Availability" character varying(40) NOT NULL,
                "LastKnownStatus" character varying(40),
                "SourceState" character varying(40),
                "CreatedOnSource" timestamptz,
                "LastSyncedAt" timestamptz NOT NULL,
                "LastSeenAt" timestamptz,
                "MissingSince" timestamptz
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_taskchain_snapshots_name ON app.taskchain_snapshots (\"Name\");",
            "CREATE INDEX IF NOT EXISTS ix_taskchain_snapshots_availability_last_synced ON app.taskchain_snapshots (\"Availability\", \"LastSyncedAt\");",

            """
            CREATE TABLE IF NOT EXISTS app.audit_entries (
                "Id" uuid PRIMARY KEY,
                "AuditId" character varying(40) NOT NULL,
                "RobotId" character varying(80) NOT NULL,
                "CommandType" character varying(40),
                "RequestedByRole" character varying(40) NOT NULL,
                "Message" character varying(1000) NOT NULL,
                "Status" character varying(40) NOT NULL,
                "OccurredAt" timestamptz NOT NULL,
                "Operation" character varying(160)
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_audit_entries_audit_id ON app.audit_entries (\"AuditId\");",
            "CREATE INDEX IF NOT EXISTS ix_audit_entries_occurred_at ON app.audit_entries (\"OccurredAt\");",
            "CREATE INDEX IF NOT EXISTS ix_audit_entries_robot_occurred_at ON app.audit_entries (\"RobotId\", \"OccurredAt\");",

            """
            CREATE TABLE IF NOT EXISTS app.command_attempts (
                "Id" uuid PRIMARY KEY,
                "CommandId" character varying(80) NOT NULL,
                "RobotId" character varying(80) NOT NULL,
                "CommandType" character varying(40) NOT NULL,
                "RequestedByRole" character varying(40) NOT NULL,
                "Status" character varying(40) NOT NULL,
                "Message" character varying(1000) NOT NULL,
                "RequestedAt" timestamptz NOT NULL,
                "CompletedAt" timestamptz NOT NULL,
                "Source" character varying(80) NOT NULL,
                "TargetEntityId" character varying(120),
                "VelocityX" double precision,
                "VelocityY" double precision,
                "Confirmed" boolean NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_command_attempts_command_id ON app.command_attempts (\"CommandId\");",
            "CREATE INDEX IF NOT EXISTS ix_command_attempts_robot_completed_at ON app.command_attempts (\"RobotId\", \"CompletedAt\");",

            """
            CREATE TABLE IF NOT EXISTS app.map_entity_snapshots (
                "Id" uuid PRIMARY KEY,
                "EntityId" character varying(120) NOT NULL,
                "MapName" character varying(160),
                "Type" character varying(40) NOT NULL,
                "Name" character varying(160) NOT NULL,
                "X" double precision NOT NULL,
                "Y" double precision NOT NULL,
                "Width" double precision NOT NULL,
                "Height" double precision NOT NULL,
                "Color" character varying(40) NOT NULL,
                "Version" integer NOT NULL,
                "SourceState" character varying(40) NOT NULL,
                "LastSyncedAt" timestamptz NOT NULL,
                "MissingSince" timestamptz,
                "PropertiesJson" jsonb
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_map_entity_snapshots_entity_id ON app.map_entity_snapshots (\"EntityId\");",
            "CREATE INDEX IF NOT EXISTS ix_map_entity_snapshots_map_last_synced ON app.map_entity_snapshots (\"MapName\", \"LastSyncedAt\");"
        };

        foreach (var statement in statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
