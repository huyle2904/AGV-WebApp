using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CommandsController(AgvPlantStore store, CommandDispatcher dispatcher, SeerWorkerClient workerClient) : ControllerBase
{
    [HttpGet("policies")]
    public ActionResult<IReadOnlyList<ControlPolicy>> GetPolicies()
    {
        return Ok(store.GetPolicies());
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult<MissionCommandResult>> Dispatch([FromBody] MissionCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await dispatcher.DispatchAsync(request, Request.ResolveRole(), cancellationToken);
            return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                request.RobotId,
                request.CommandType,
                MissionCommandStatus.Rejected,
                exception.Message,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
        }
    }

    [HttpPost("relocate")]
    public async Task<ActionResult<MissionCommandResult>> Relocate([FromBody] SeerRelocationRequest request, CancellationToken cancellationToken)
    {
        var result = await workerClient.RelocateAsync(request, cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }
}
