using Microsoft.EntityFrameworkCore;
using NewAGV.Api.Data;

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

            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"DisplayName\" character varying(160);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"TimeoutSeconds\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"RetryCount\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"FailurePolicy\" character varying(40) NOT NULL DEFAULT 'StopWorkflow';",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"Note\" character varying(1000);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"TaskChainRunId\" character varying(80);",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"ProgressPercent\" double precision;",
            "ALTER TABLE IF EXISTS app.workflow_run_steps ADD COLUMN IF NOT EXISTS \"Info\" character varying(1000);"
        };

        foreach (var statement in statements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
