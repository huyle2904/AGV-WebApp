using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MapController(MapSnapshotService mapSnapshotService) : ControllerBase
{
    [HttpGet("entities")]
    public async Task<ActionResult<IReadOnlyList<MapEntity>>> GetEntities(CancellationToken cancellationToken)
    {
        return Ok(await mapSnapshotService.GetEntitiesAsync(cancellationToken));
    }

    [HttpPost("entities")]
    public async Task<ActionResult<MapEntity>> UpsertEntity([FromBody] MapEntity entity, CancellationToken cancellationToken)
    {
        var role = Request.ResolveRole();
        if (role < UserRole.Engineer)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalized = string.IsNullOrWhiteSpace(entity.EntityId)
            ? entity with { EntityId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() }
            : entity;

        return Ok(await mapSnapshotService.UpsertEntityAsync(normalized, null, cancellationToken));
    }

    [HttpDelete("entities/{entityId}")]
    public async Task<ActionResult> DeleteEntity(string entityId, CancellationToken cancellationToken)
    {
        var role = Request.ResolveRole();
        if (role < UserRole.Engineer)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await mapSnapshotService.DeleteEntityAsync(entityId, cancellationToken) ? NoContent() : NotFound();
    }
}
