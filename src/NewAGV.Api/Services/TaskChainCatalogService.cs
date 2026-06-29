using Microsoft.EntityFrameworkCore;
using NewAGV.Contracts;
using NewAGV.Persistence;

namespace NewAGV.Api.Services;

public sealed class TaskChainCatalogService(NewAgvDbContext dbContext)
{
    private const string Available = "Available";
    private const string MissingFromSource = "MissingFromSource";
    private const string Stale = "Stale";
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<SeerTaskChainSummary>> SyncAsync(
        IReadOnlyList<SeerTaskChainSummary> taskChains,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.TaskChainSnapshots.ToListAsync(cancellationToken);
        var existingByName = existing.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var taskChain in taskChains)
        {
            seenNames.Add(taskChain.Name);

            if (!existingByName.TryGetValue(taskChain.Name, out var snapshot))
            {
                snapshot = new TaskChainSnapshotEntity
                {
                    Name = taskChain.Name
                };
                dbContext.TaskChainSnapshots.Add(snapshot);
                existingByName[taskChain.Name] = snapshot;
            }

            snapshot.Availability = Available;
            snapshot.SourceState = "Synced";
            snapshot.LastKnownStatus = NormalizeStatus(taskChain.LastKnownStatus);
            snapshot.CreatedOnSource = taskChain.CreatedOn;
            snapshot.LastSeenAt = now;
            snapshot.LastSyncedAt = now;
            snapshot.MissingSince = null;
        }

        foreach (var snapshot in existing.Where(item => !seenNames.Contains(item.Name)))
        {
            snapshot.Availability = MissingFromSource;
            snapshot.SourceState = "Missing";
            snapshot.LastSyncedAt = now;
            snapshot.MissingSince ??= now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTaskChainsAsync(cancellationToken);
    }

    public async Task UpdateTaskChainStatusAsync(string taskChainName, TaskChainStatus? status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskChainName))
        {
            return;
        }

        var snapshot = await dbContext.TaskChainSnapshots
            .FirstOrDefaultAsync(item => item.Name == taskChainName, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        snapshot.LastKnownStatus = NormalizeStatus(status);
        snapshot.LastSyncedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SeerTaskChainSummary>> GetTaskChainsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = await dbContext.TaskChainSnapshots
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return snapshots.Select(item => MapSummary(item, now)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, SeerTaskChainSummary>> GetTaskChainLookupAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = await dbContext.TaskChainSnapshots
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return snapshots.ToDictionary(
            item => item.Name,
            item => MapSummary(item, now),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeStatus(TaskChainStatus? status)
        => status?.ToString();

    private static TaskChainStatus? ParseStatus(string? value)
        => Enum.TryParse<TaskChainStatus>(value, true, out var status) ? status : null;

    private static SeerTaskChainSummary MapSummary(TaskChainSnapshotEntity snapshot, DateTimeOffset now)
    {
        var availability = ResolveAvailability(snapshot, now);
        var sourceState = availability == Available ? "Synced" : availability;

        return new SeerTaskChainSummary(
            snapshot.Name,
            snapshot.CreatedOnSource,
            ParseStatus(snapshot.LastKnownStatus),
            availability,
            sourceState,
            snapshot.LastSyncedAt,
            snapshot.MissingSince,
            snapshot.ExternalId);
    }

    private static string ResolveAvailability(TaskChainSnapshotEntity snapshot, DateTimeOffset now)
    {
        if (string.Equals(snapshot.Availability, MissingFromSource, StringComparison.OrdinalIgnoreCase))
        {
            return MissingFromSource;
        }

        if (snapshot.LastSyncedAt <= now - StaleThreshold)
        {
            return Stale;
        }

        return Available;
    }
}
