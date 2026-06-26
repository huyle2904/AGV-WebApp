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
            "CREATE INDEX IF NOT EXISTS ix_taskchain_snapshots_availability_last_synced ON app.taskchain_snapshots (\"Availability\", \"LastSyncedAt\");"
        };

        foreach (var statement in statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
