using Microsoft.EntityFrameworkCore;

namespace NewAGV.Persistence;

public sealed class NewAgvDbContext(DbContextOptions<NewAgvDbContext> options) : DbContext(options)
{
    public const string ActiveWorkflowRunStatusFilter =
        "\"Status\" IN ('Pending', 'Validating', 'Ready', 'Starting', 'Running', 'Paused')";

    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowStepEntity> WorkflowSteps => Set<WorkflowStepEntity>();
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<WorkflowRunStepEntity> WorkflowRunSteps => Set<WorkflowRunStepEntity>();
    public DbSet<TaskChainSnapshotEntity> TaskChainSnapshots => Set<TaskChainSnapshotEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<CommandAttemptEntity> CommandAttempts => Set<CommandAttemptEntity>();
    public DbSet<MapEntitySnapshotEntity> MapEntitySnapshots => Set<MapEntitySnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.ToTable("workflow_definitions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.AssignedRobotId).HasMaxLength(80);
            entity.Property(item => item.ExecutionMode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasMany(item => item.Steps)
                .WithOne(item => item.WorkflowDefinition)
                .HasForeignKey(item => item.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Runs)
                .WithOne(item => item.WorkflowDefinition)
                .HasForeignKey(item => item.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowStepEntity>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkflowDefinitionId, item.Sequence }).IsUnique();
            entity.Property(item => item.StepType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TaskChainName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160);
            entity.Property(item => item.FailurePolicy).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.Property(item => item.ParametersJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<WorkflowRunEntity>(entity =>
        {
            entity.ToTable("workflow_runs");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkflowDefinitionId, item.StartedAt });
            entity.HasIndex(item => item.RobotId)
                .IsUnique()
                .HasDatabaseName("ux_workflow_runs_active_robot")
                .HasFilter(ActiveWorkflowRunStatusFilter);
            entity.Property(item => item.RobotId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TriggeredBy).HasMaxLength(80);
            entity.Property(item => item.ErrorMessage).HasMaxLength(1000);
            entity.Property(item => item.CanceledBy).HasMaxLength(80);
            entity.Property(item => item.ValidationSnapshotJson).HasColumnType("jsonb");
            entity.HasMany(item => item.Steps)
                .WithOne(item => item.WorkflowRun)
                .HasForeignKey(item => item.WorkflowRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowRunStepEntity>(entity =>
        {
            entity.ToTable("workflow_run_steps");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkflowRunId, item.Sequence }).IsUnique();
            entity.Property(item => item.StepType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TaskChainName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160);
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.TaskChainRunId).HasMaxLength(80);
            entity.Property(item => item.SeerTaskId).HasMaxLength(80);
            entity.Property(item => item.Message).HasMaxLength(1000);
            entity.Property(item => item.FailurePolicy).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.Property(item => item.Info).HasMaxLength(1000);
        });

        modelBuilder.Entity<TaskChainSnapshotEntity>(entity =>
        {
            entity.ToTable("taskchain_snapshots");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => new { item.Availability, item.LastSyncedAt });
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.ExternalId).HasMaxLength(120);
            entity.Property(item => item.Availability).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LastKnownStatus).HasMaxLength(40);
            entity.Property(item => item.SourceState).HasMaxLength(40);
        });

        modelBuilder.Entity<AuditEntryEntity>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.AuditId).IsUnique();
            entity.HasIndex(item => item.OccurredAt);
            entity.HasIndex(item => new { item.RobotId, item.OccurredAt });
            entity.Property(item => item.AuditId).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RobotId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CommandType).HasMaxLength(40);
            entity.Property(item => item.RequestedByRole).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Operation).HasMaxLength(160);
        });

        modelBuilder.Entity<CommandAttemptEntity>(entity =>
        {
            entity.ToTable("command_attempts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CommandId).IsUnique();
            entity.HasIndex(item => new { item.RobotId, item.CompletedAt });
            entity.Property(item => item.CommandId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.RobotId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CommandType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RequestedByRole).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TargetEntityId).HasMaxLength(120);
        });

        modelBuilder.Entity<MapEntitySnapshotEntity>(entity =>
        {
            entity.ToTable("map_entity_snapshots");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.EntityId).IsUnique();
            entity.HasIndex(item => new { item.MapName, item.LastSyncedAt });
            entity.Property(item => item.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.MapName).HasMaxLength(160);
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Color).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SourceState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.PropertiesJson).HasColumnType("jsonb");
        });
    }
}

public sealed class WorkflowDefinitionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool IsPublished { get; set; }
    public string? AssignedRobotId { get; set; }
    public string ExecutionMode { get; set; } = "Sequential";
    public bool RequiresConfirmation { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public bool ManualResume { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<WorkflowStepEntity> Steps { get; set; } = [];
    public List<WorkflowRunEntity> Runs { get; set; } = [];
}

public sealed class WorkflowStepEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinitionEntity WorkflowDefinition { get; set; } = null!;
    public int Sequence { get; set; }
    public string StepType { get; set; } = "TaskChain";
    public string TaskChainName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public string FailurePolicy { get; set; } = "StopWorkflow";
    public string? Note { get; set; }
    public bool StopOnFailure { get; set; } = true;
    public string? ParametersJson { get; set; }
}

public sealed class WorkflowRunEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinitionEntity WorkflowDefinition { get; set; } = null!;
    public string RobotId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? TriggeredBy { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int? CurrentStepSequence { get; set; }
    public string? CanceledBy { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ValidationSnapshotJson { get; set; }
    public List<WorkflowRunStepEntity> Steps { get; set; } = [];
}

public sealed class WorkflowRunStepEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowRunId { get; set; }
    public WorkflowRunEntity WorkflowRun { get; set; } = null!;
    public int Sequence { get; set; }
    public string StepType { get; set; } = "TaskChain";
    public string TaskChainName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public string FailurePolicy { get; set; } = "StopWorkflow";
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public string? TaskChainRunId { get; set; }
    public string? SeerTaskId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? ProgressPercent { get; set; }
    public string? Info { get; set; }
    public string? Message { get; set; }
}

public sealed class TaskChainSnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Availability { get; set; } = "Available";
    public string? LastKnownStatus { get; set; }
    public string? SourceState { get; set; }
    public DateTimeOffset? CreatedOnSource { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? MissingSince { get; set; }
}

public sealed class AuditEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AuditId { get; set; } = string.Empty;
    public string RobotId { get; set; } = string.Empty;
    public string? CommandType { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Operation { get; set; }
}

public sealed class CommandAttemptEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommandId { get; set; } = string.Empty;
    public string RobotId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public string RequestedByRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string? TargetEntityId { get; set; }
    public double? VelocityX { get; set; }
    public double? VelocityY { get; set; }
    public bool Confirmed { get; set; }
}

public sealed class MapEntitySnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityId { get; set; } = string.Empty;
    public string? MapName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Color { get; set; } = "#38bdf8";
    public int Version { get; set; }
    public string SourceState { get; set; } = "Synced";
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? MissingSince { get; set; }
    public string? PropertiesJson { get; set; }
}
