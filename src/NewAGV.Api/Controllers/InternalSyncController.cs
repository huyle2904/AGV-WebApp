using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NewAGV.Api.Hubs;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("internal/sync")]
public sealed class InternalSyncController(
    AgvPlantStore store,
    IHubContext<TelemetryHub> hubContext) : ControllerBase
{
    [HttpPost("robot")]
    public async Task<ActionResult> UpsertRobot([FromBody] InternalRobotStateUpdate update, CancellationToken cancellationToken)
    {
        store.UpdateRobot(update.Robot);
        if (update.Detail is not null)
        {
            store.UpdateRobotDetail(update.Detail);
        }

        await hubContext.Clients.All.SendAsync(
            "ReceiveTelemetry",
            new RealtimeEvent("robot.updated", DateTimeOffset.UtcNow, Robot: update.Robot, Detail: update.Detail),
            cancellationToken);

        return Accepted();
    }

    [HttpPost("map")]
    public async Task<ActionResult> ReplaceMap([FromBody] InternalMapSnapshot snapshot, CancellationToken cancellationToken)
    {
        store.ReplaceMapEntities(snapshot.Entities);

        foreach (var entity in snapshot.Entities)
        {
            await hubContext.Clients.All.SendAsync(
                "ReceiveTelemetry",
                new RealtimeEvent("map.updated", DateTimeOffset.UtcNow, MapEntity: entity),
                cancellationToken);
        }

        return Accepted();
    }

    [HttpPost("health")]
    public async Task<ActionResult> UpdateHealth([FromBody] SiteHealth health, CancellationToken cancellationToken)
    {
        store.UpdateHealth(health);

        await hubContext.Clients.All.SendAsync(
            "ReceiveTelemetry",
            new RealtimeEvent("health.updated", DateTimeOffset.UtcNow, Health: health),
            cancellationToken);

        return Accepted();
    }

    [HttpPost("workflow")]
    public async Task<ActionResult> UpdateWorkflow([FromBody] InternalWorkflowRunUpdate update, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync(
            "ReceiveTelemetry",
            new RealtimeEvent(
                update.EventType,
                DateTimeOffset.UtcNow,
                WorkflowRun: update.WorkflowRun,
                Message: update.Message),
            cancellationToken);

        return Accepted();
    }
}
