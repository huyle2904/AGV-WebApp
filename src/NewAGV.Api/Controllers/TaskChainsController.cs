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

    [HttpGet("active-run")]
    public ActionResult<TaskChainRunSnapshot?> GetActiveRun()
    {
        return Ok(coordinator.GetActiveRun());
    }

    [HttpPost("pause")]
    public async Task<ActionResult<MissionCommandResult>> Pause(CancellationToken cancellationToken)
    {
        var result = await coordinator.PauseAsync(Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("resume")]
    public async Task<ActionResult<MissionCommandResult>> Resume(CancellationToken cancellationToken)
    {
        var result = await coordinator.ResumeAsync(Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<MissionCommandResult>> Cancel(CancellationToken cancellationToken)
    {
        var result = await coordinator.CancelAsync(Request.ResolveRole(), cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }
}
