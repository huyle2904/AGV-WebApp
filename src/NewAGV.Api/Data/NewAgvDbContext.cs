using Microsoft.EntityFrameworkCore;

namespace NewAGV.Api.Data;

public sealed class NewAgvDbContext(DbContextOptions<NewAgvDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowStepEntity> WorkflowSteps => Set<WorkflowStepEntity>();
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<WorkflowRunStepEntity> WorkflowRunSteps => Set<WorkflowRunStepEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.ToTable("workflow_definitions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Name).IsUnique();
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
            entity.Property(item => item.SeerTaskId).HasMaxLength(80);
            entity.Property(item => item.Message).HasMaxLength(1000);
            entity.Property(item => item.FailurePolicy).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.Property(item => item.Info).HasMaxLength(1000);
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
    public string? SeerTaskId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double? ProgressPercent { get; set; }
    public string? Info { get; set; }
    public string? Message { get; set; }
}
