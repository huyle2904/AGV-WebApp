using NewAGV.Api.Services;
using NewAGV.Api.Tests.TestSupport;
using NewAGV.Contracts;
using NewAGV.Persistence;

namespace NewAGV.Api.Tests;

public sealed class MapSnapshotServiceTests
{
    [Fact]
    public async Task ReplaceSnapshotAsync_PersistsEntitiesAndReturnsThemFromPublicRead()
    {
        await using var dbContext = DbContextFactory.Create();
        var plantStore = PlantStoreFactory.Create();
        var service = new MapSnapshotService(dbContext, plantStore);

        await service.ReplaceSnapshotAsync(new InternalMapSnapshot(
            "MAP-A",
        [
            new MapEntity("ST-A", MapEntityType.Station, "Station A", 1, 2, 3, 4, "#111111", 1)
        ]), CancellationToken.None);

        var entities = await service.GetEntitiesAsync(CancellationToken.None);

        var entity = Assert.Single(entities);
        Assert.Equal("ST-A", entity.EntityId);
        Assert.Equal("Station A", entity.Name);
    }

    [Fact]
    public async Task ReplaceSnapshotAsync_MarksAbsentEntitiesMissingAndExcludesThemFromPublicReads()
    {
        await using var dbContext = DbContextFactory.Create();
        var plantStore = PlantStoreFactory.Create();
        var service = new MapSnapshotService(dbContext, plantStore);

        await service.ReplaceSnapshotAsync(new InternalMapSnapshot(
            "MAP-A",
        [
            new MapEntity("ST-A", MapEntityType.Station, "Station A", 1, 2, 3, 4, "#111111", 1)
        ]), CancellationToken.None);

        await service.ReplaceSnapshotAsync(new InternalMapSnapshot(
            "MAP-A",
        [
            new MapEntity("ST-B", MapEntityType.Station, "Station B", 5, 6, 3, 4, "#222222", 1)
        ]), CancellationToken.None);

        var snapshots = dbContext.MapEntitySnapshots.OrderBy(item => item.EntityId).ToList();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal("Missing", snapshots[0].SourceState);
        Assert.NotNull(snapshots[0].MissingSince);

        var publicEntities = await service.GetEntitiesAsync(CancellationToken.None);
        var entity = Assert.Single(publicEntities);
        Assert.Equal("ST-B", entity.EntityId);
    }

    [Fact]
    public async Task ReplaceSnapshotAsync_IncrementsChangedEntityVersionAndKeepsUnchangedVersion()
    {
        await using var dbContext = DbContextFactory.Create();
        var plantStore = PlantStoreFactory.Create();
        var service = new MapSnapshotService(dbContext, plantStore);

        await service.ReplaceSnapshotAsync(new InternalMapSnapshot(
            "MAP-A",
        [
            new MapEntity("ST-A", MapEntityType.Station, "Station A", 1, 2, 3, 4, "#111111", 1),
            new MapEntity("ST-B", MapEntityType.Station, "Station B", 5, 6, 3, 4, "#222222", 1)
        ]), CancellationToken.None);

        await service.ReplaceSnapshotAsync(new InternalMapSnapshot(
            "MAP-A",
        [
            new MapEntity("ST-A", MapEntityType.Station, "Station A Updated", 1, 2, 3, 4, "#111111", 1),
            new MapEntity("ST-B", MapEntityType.Station, "Station B", 5, 6, 3, 4, "#222222", 1)
        ]), CancellationToken.None);

        var changed = await dbContext.MapEntitySnapshots.SingleAsync(item => item.EntityId == "ST-A", CancellationToken.None);
        var unchanged = await dbContext.MapEntitySnapshots.SingleAsync(item => item.EntityId == "ST-B", CancellationToken.None);

        Assert.Equal(2, changed.Version);
        Assert.Equal(1, unchanged.Version);
    }
}
