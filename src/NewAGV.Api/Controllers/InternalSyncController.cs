using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("internal/sync")]
public sealed class InternalSyncController(
    AgvPlantStore store,
    TelemetryEventPublisher telemetryPublisher,
    MapSnapshotService mapSnapshotService) : ControllerBase
{
    [HttpPost("robot")]
    public async Task<ActionResult> UpsertRobot([FromBody] InternalRobotStateUpdate update, CancellationToken cancellationToken)
    {
        store.UpdateRobot(update.Robot);
        if (update.Detail is not null)
        {
            store.UpdateRobotDetail(update.Detail);
        }

        await telemetryPublisher.PublishAsync(
            new RealtimeEvent("robot.updated", DateTimeOffset.UtcNow, Robot: update.Robot, Detail: update.Detail),
            cancellationToken);

        return Accepted();
    }

    [HttpPost("map")]
    public async Task<ActionResult> ReplaceMap([FromBody] InternalMapSnapshot snapshot, CancellationToken cancellationToken)
    {
        var entities = await mapSnapshotService.ReplaceSnapshotAsync(snapshot, cancellationToken);

        foreach (var entity in entities)
        {
            await telemetryPublisher.PublishAsync(
                new RealtimeEvent("map.updated", DateTimeOffset.UtcNow, MapEntity: entity),
                cancellationToken);
        }

        return Accepted();
    }

    [HttpPost("health")]
    public async Task<ActionResult> UpdateHealth([FromBody] SiteHealth health, CancellationToken cancellationToken)
    {
        store.UpdateHealth(health);

        await telemetryPublisher.PublishAsync(
            new RealtimeEvent("health.updated", DateTimeOffset.UtcNow, Health: health),
            cancellationToken);

        return Accepted();
    }

    [HttpPost("workflow")]
    public async Task<ActionResult> UpdateWorkflow([FromBody] InternalWorkflowRunUpdate update, CancellationToken cancellationToken)
    {
        await telemetryPublisher.PublishAsync(
            new RealtimeEvent(
                update.EventType,
                DateTimeOffset.UtcNow,
                WorkflowRun: update.WorkflowRun,
                Message: update.Message),
            cancellationToken);

        return Accepted();
    }

    [HttpPost("debug/skip-sequence")]
    public ActionResult<object> SkipRealtimeSequence([FromQuery] long count = 1)
    {
        try
        {
            var sequence = telemetryPublisher.SkipSequence(count);
            return Accepted(new { Sequence = sequence, Skipped = count });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
