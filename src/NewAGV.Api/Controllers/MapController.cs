using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MapController(AgvPlantStore store) : ControllerBase
{
    [HttpGet("entities")]
    public ActionResult<IReadOnlyList<MapEntity>> GetEntities()
    {
        return Ok(store.GetMapEntities());
    }

    [HttpPost("entities")]
    public ActionResult<MapEntity> UpsertEntity([FromBody] MapEntity entity)
    {
        var role = Request.ResolveRole();
        if (role < UserRole.Engineer)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalized = string.IsNullOrWhiteSpace(entity.EntityId)
            ? entity with { EntityId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() }
            : entity;

        return Ok(store.UpsertMapEntity(normalized));
    }

    [HttpDelete("entities/{entityId}")]
    public ActionResult DeleteEntity(string entityId)
    {
        var role = Request.ResolveRole();
        if (role < UserRole.Engineer)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return store.DeleteMapEntity(entityId) ? NoContent() : NotFound();
    }
}
