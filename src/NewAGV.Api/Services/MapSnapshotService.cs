using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewAGV.Contracts;
using NewAGV.Persistence;

namespace NewAGV.Api.Services;

public sealed class MapSnapshotService(NewAgvDbContext dbContext, AgvPlantStore plantStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MapEntity>> GetEntitiesAsync(CancellationToken cancellationToken)
    {
        var snapshots = await dbContext.MapEntitySnapshots
            .AsNoTracking()
            .Where(item => item.SourceState != "Missing")
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return plantStore.GetMapEntities();
        }

        var entities = snapshots.Select(ToContract).ToList();
        plantStore.ReplaceMapEntities(entities);
        return entities;
    }

    public async Task<MapEntity?> GetEntityAsync(string entityId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.MapEntitySnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EntityId == entityId && item.SourceState != "Missing", cancellationToken);

        if (snapshot is not null)
        {
            return ToContract(snapshot);
        }

        return plantStore.GetMapEntity(entityId);
    }

    public async Task<MapEntity> UpsertEntityAsync(MapEntity entity, string? mapName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = await dbContext.MapEntitySnapshots
            .FirstOrDefaultAsync(item => item.EntityId == entity.EntityId, cancellationToken);

        var nextVersion = snapshot is null ? Math.Max(1, entity.Version) : snapshot.Version + 1;
        var normalized = entity with
        {
            Version = nextVersion,
            Properties = entity.Properties ?? new Dictionary<string, string>()
        };

        if (snapshot is null)
        {
            dbContext.MapEntitySnapshots.Add(ToEntity(normalized, mapName, now));
        }
        else
        {
            Apply(snapshot, normalized, mapName, now, "Synced");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        plantStore.UpsertMapEntity(normalized);
        return normalized;
    }

    public async Task<bool> DeleteEntityAsync(string entityId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.MapEntitySnapshots
            .FirstOrDefaultAsync(item => item.EntityId == entityId, cancellationToken);

        if (snapshot is null)
        {
            return plantStore.DeleteMapEntity(entityId);
        }

        snapshot.SourceState = "Missing";
        snapshot.MissingSince ??= DateTimeOffset.UtcNow;
        snapshot.LastSyncedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        plantStore.DeleteMapEntity(entityId);
        return true;
    }

    public async Task<IReadOnlyList<MapEntity>> ReplaceSnapshotAsync(InternalMapSnapshot mapSnapshot, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var incoming = mapSnapshot.Entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.EntityId))
            .GroupBy(entity => entity.EntityId)
            .Select(group => group.Last())
            .ToList();
        var incomingIds = incoming.Select(entity => entity.EntityId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await dbContext.MapEntitySnapshots.ToListAsync(cancellationToken);

        foreach (var entity in incoming)
        {
            var snapshot = existing.FirstOrDefault(item => string.Equals(item.EntityId, entity.EntityId, StringComparison.OrdinalIgnoreCase));
            var nextVersion = snapshot is null ? Math.Max(1, entity.Version) : ResolveVersion(snapshot, entity);
            var normalized = entity with
            {
                Version = nextVersion,
                Properties = entity.Properties ?? new Dictionary<string, string>()
            };

            if (snapshot is null)
            {
                dbContext.MapEntitySnapshots.Add(ToEntity(normalized, mapSnapshot.CurrentMap, now));
            }
            else
            {
                Apply(snapshot, normalized, mapSnapshot.CurrentMap, now, "Synced");
            }
        }

        foreach (var snapshot in existing.Where(item => !incomingIds.Contains(item.EntityId)))
        {
            snapshot.SourceState = "Missing";
            snapshot.MissingSince ??= now;
            snapshot.LastSyncedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        plantStore.ReplaceMapEntities(incoming);
        return incoming;
    }

    private static int ResolveVersion(MapEntitySnapshotEntity snapshot, MapEntity entity)
        => HasChanged(snapshot, entity) ? snapshot.Version + 1 : snapshot.Version;

    private static bool HasChanged(MapEntitySnapshotEntity snapshot, MapEntity entity)
        => snapshot.Type != entity.Type.ToString()
           || snapshot.Name != entity.Name
           || snapshot.X != entity.X
           || snapshot.Y != entity.Y
           || snapshot.Width != entity.Width
           || snapshot.Height != entity.Height
           || snapshot.Color != entity.Color
           || snapshot.PropertiesJson != SerializeProperties(entity.Properties);

    private static void Apply(
        MapEntitySnapshotEntity snapshot,
        MapEntity entity,
        string? mapName,
        DateTimeOffset now,
        string sourceState)
    {
        snapshot.MapName = mapName;
        snapshot.Type = entity.Type.ToString();
        snapshot.Name = entity.Name;
        snapshot.X = entity.X;
        snapshot.Y = entity.Y;
        snapshot.Width = entity.Width;
        snapshot.Height = entity.Height;
        snapshot.Color = entity.Color;
        snapshot.Version = entity.Version;
        snapshot.SourceState = sourceState;
        snapshot.LastSyncedAt = now;
        snapshot.MissingSince = sourceState == "Missing" ? snapshot.MissingSince ?? now : null;
        snapshot.PropertiesJson = SerializeProperties(entity.Properties);
    }

    private static MapEntitySnapshotEntity ToEntity(MapEntity entity, string? mapName, DateTimeOffset now)
        => new()
        {
            EntityId = entity.EntityId,
            MapName = mapName,
            Type = entity.Type.ToString(),
            Name = entity.Name,
            X = entity.X,
            Y = entity.Y,
            Width = entity.Width,
            Height = entity.Height,
            Color = entity.Color,
            Version = entity.Version,
            SourceState = "Synced",
            LastSyncedAt = now,
            PropertiesJson = SerializeProperties(entity.Properties)
        };

    private static MapEntity ToContract(MapEntitySnapshotEntity snapshot)
        => new(
            snapshot.EntityId,
            Enum.TryParse<MapEntityType>(snapshot.Type, out var type) ? type : MapEntityType.Station,
            snapshot.Name,
            snapshot.X,
            snapshot.Y,
            snapshot.Width,
            snapshot.Height,
            snapshot.Color,
            snapshot.Version,
            DeserializeProperties(snapshot.PropertiesJson));

    private static string? SerializeProperties(Dictionary<string, string>? properties)
        => properties is null || properties.Count == 0
            ? null
            : JsonSerializer.Serialize(
                properties
                    .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(item => item.Key, item => item.Value),
                JsonOptions);

    private static Dictionary<string, string>? DeserializeProperties(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
}
