using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TaskChainsController(TaskChainCoordinator coordinator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeerTaskChainSummary>>> GetTaskChains(CancellationToken cancellationToken)
    {
        var result = await coordinator.GetTaskChainsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<IReadOnlyList<SeerTaskChainSummary>>> SyncTaskChains(CancellationToken cancellationToken)
    {
        var result = await coordinator.SyncTaskChainsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("active-run")]
    public ActionResult<TaskChainRunSnapshot?> GetActiveRun([FromQuery] string? robotId)
    {
        var result = coordinator.GetActiveRun(robotId);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("history")]
    public ActionResult<IReadOnlyList<TaskChainRunSnapshot>> GetHistory([FromQuery] string? robotId)
        => Ok(coordinator.GetRecentRuns(robotId));

    [HttpGet("{name}")]
    public async Task<ActionResult<SeerTaskChainStatus>> GetTaskChain(string name, CancellationToken cancellationToken)
    {
        var result = await coordinator.GetTaskChainStatusAsync(name, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("execute")]
    public async Task<ActionResult<TaskChainRunResult>> Execute([FromBody] TaskChainRunRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.ExecuteAsync(request, Request.ResolveRole(), cancellationToken);
        return result.Status == TaskChainRunStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("pause")]
    public async Task<ActionResult<MissionCommandResult>> Pause([FromBody] TaskChainControlRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.PauseAsync(request, Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("resume")]
    public async Task<ActionResult<MissionCommandResult>> Resume([FromBody] TaskChainControlRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.ResumeAsync(request, Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<MissionCommandResult>> Cancel([FromBody] TaskChainControlRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.CancelAsync(request, Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }
}
