using NewAGV.Api.Services;
using NewAGV.Api.Tests.TestSupport;
using NewAGV.Contracts;
using NewAGV.Persistence;

namespace NewAGV.Api.Tests;

public sealed class TaskChainCatalogServiceTests
{
    [Fact]
    public async Task SyncAsync_AddsAvailableTaskChains()
    {
        await using var dbContext = DbContextFactory.Create();
        var service = new TaskChainCatalogService(dbContext);

        var summaries = await service.SyncAsync(
        [
            new SeerTaskChainSummary("TC-A", DateTimeOffset.UtcNow, TaskChainStatus.Running)
        ], CancellationToken.None);

        var summary = Assert.Single(summaries);
        Assert.Equal("TC-A", summary.Name);
        Assert.Equal("Available", summary.Availability);
        Assert.Equal("Synced", summary.SourceState);
        Assert.Equal(TaskChainStatus.Running, summary.LastKnownStatus);
    }

    [Fact]
    public async Task SyncAsync_MarksMissingTaskChainsAsMissingFromSource()
    {
        await using var dbContext = DbContextFactory.Create();
        var service = new TaskChainCatalogService(dbContext);

        await service.SyncAsync(
        [
            new SeerTaskChainSummary("TC-MISSING", DateTimeOffset.UtcNow, TaskChainStatus.Completed)
        ], CancellationToken.None);

        var summaries = await service.SyncAsync([], CancellationToken.None);

        var summary = Assert.Single(summaries);
        Assert.Equal("MissingFromSource", summary.Availability);
        Assert.Equal("MissingFromSource", summary.SourceState);
        Assert.NotNull(summary.MissingSince);
    }

    [Fact]
    public async Task GetTaskChainsAsync_ReportsStaleWhenSnapshotIsOlderThanThreshold()
    {
        await using var dbContext = DbContextFactory.Create();
        dbContext.TaskChainSnapshots.Add(new TaskChainSnapshotEntity
        {
            Name = "TC-STALE",
            Availability = "Available",
            SourceState = "Synced",
            LastKnownStatus = TaskChainStatus.Waiting.ToString(),
            LastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-16)
        });
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var service = new TaskChainCatalogService(dbContext);
        var summaries = await service.GetTaskChainsAsync(CancellationToken.None);

        var summary = Assert.Single(summaries);
        Assert.Equal("Stale", summary.Availability);
        Assert.Equal("Stale", summary.SourceState);
        Assert.Equal(TaskChainStatus.Waiting, summary.LastKnownStatus);
    }
}
