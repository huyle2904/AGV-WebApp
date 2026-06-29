using Microsoft.EntityFrameworkCore;
using NewAGV.Contracts;
using NewAGV.Persistence;

namespace NewAGV.Api.Services;

public sealed class AuditLogService(
    NewAgvDbContext dbContext,
    AgvPlantStore plantStore,
    ILogger<AuditLogService> logger)
{
    public async Task<IReadOnlyList<MissionAuditEntry>> GetRecentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await dbContext.AuditEntries
                .AsNoTracking()
                .OrderByDescending(entry => entry.OccurredAt)
                .Take(250)
                .ToListAsync(cancellationToken);

            if (entries.Count > 0)
            {
                return entries.Select(ToContract).ToList();
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to read durable audit entries; falling back to memory store.");
        }

        return plantStore.GetAudits();
    }

    public async Task RecordAuditAsync(MissionAuditEntry entry, CancellationToken cancellationToken)
    {
        plantStore.AddAudit(entry);

        try
        {
            dbContext.AuditEntries.Add(ToEntity(entry));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Failed to persist audit entry {AuditId}.", entry.AuditId);
        }
    }

    public async Task RecordCommandAttemptAsync(
        MissionCommandRequest request,
        UserRole requestedByRole,
        MissionCommandResult result,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.CommandAttempts.Add(new CommandAttemptEntity
            {
                CommandId = result.CommandId,
                RobotId = result.RobotId,
                CommandType = result.CommandType.ToString(),
                RequestedByRole = requestedByRole.ToString(),
                Status = result.Status.ToString(),
                Message = result.Message,
                RequestedAt = result.RequestedAt,
                CompletedAt = result.CompletedAt,
                Source = source,
                TargetEntityId = request.TargetEntityId,
                VelocityX = request.VelocityX,
                VelocityY = request.VelocityY,
                Confirmed = request.Confirmed
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Failed to persist command attempt {CommandId}.", result.CommandId);
        }
    }

    private static AuditEntryEntity ToEntity(MissionAuditEntry entry)
        => new()
        {
            AuditId = entry.AuditId,
            RobotId = entry.RobotId,
            CommandType = entry.CommandType?.ToString(),
            RequestedByRole = entry.RequestedByRole.ToString(),
            Message = entry.Message,
            Status = entry.Status.ToString(),
            OccurredAt = entry.OccurredAt,
            Operation = entry.Operation
        };

    private static MissionAuditEntry ToContract(AuditEntryEntity entry)
        => new(
            entry.AuditId,
            entry.RobotId,
            Enum.TryParse<MissionCommandType>(entry.CommandType, out var commandType) ? commandType : null,
            Enum.TryParse<UserRole>(entry.RequestedByRole, out var role) ? role : UserRole.Operator,
            entry.Message,
            Enum.TryParse<MissionCommandStatus>(entry.Status, out var status) ? status : MissionCommandStatus.Rejected,
            entry.OccurredAt,
            entry.Operation);
}
