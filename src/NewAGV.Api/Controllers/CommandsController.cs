using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Infrastructure;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CommandsController(
    AgvPlantStore store,
    CommandDispatcher dispatcher,
    SeerWorkerClient workerClient,
    AuditLogService auditLogService) : ControllerBase
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
            var result = new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                request.RobotId,
                request.CommandType,
                MissionCommandStatus.Rejected,
                exception.Message,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            var role = Request.ResolveRole();
            await auditLogService.RecordAuditAsync(ToAuditEntry(request.RobotId, request.CommandType, role, result), cancellationToken);
            await auditLogService.RecordCommandAttemptAsync(request, role, result, "api.commands.dispatch", cancellationToken);
            return BadRequest(result);
        }
    }

    [HttpPost("relocate")]
    public async Task<ActionResult<MissionCommandResult>> Relocate([FromBody] SeerRelocationRequest request, CancellationToken cancellationToken)
    {
        var result = await workerClient.RelocateAsync(request, cancellationToken);
        var role = Request.ResolveRole();
        var commandRequest = new MissionCommandRequest(result.RobotId, result.CommandType, null, null, null, true);
        await auditLogService.RecordAuditAsync(ToAuditEntry(result.RobotId, result.CommandType, role, result), cancellationToken);
        await auditLogService.RecordCommandAttemptAsync(commandRequest, role, result, "api.commands.relocate", cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    [HttpPost("teleop")]
    public async Task<ActionResult<MissionCommandResult>> Teleop([FromBody] TeleopRequest request, CancellationToken cancellationToken)
    {
        var result = await workerClient.TeleopDriveAsync(request, cancellationToken);
        var role = Request.ResolveRole();
        var commandRequest = new MissionCommandRequest(result.RobotId, MissionCommandType.Teleop, null, request.VelocityX, request.VelocityY, true);
        await auditLogService.RecordAuditAsync(ToAuditEntry(result.RobotId, MissionCommandType.Teleop, role, result), cancellationToken);
        await auditLogService.RecordCommandAttemptAsync(commandRequest, role, result, "api.commands.teleop", cancellationToken);
        return result.Status == MissionCommandStatus.Rejected ? BadRequest(result) : Ok(result);
    }

    private static MissionAuditEntry ToAuditEntry(
        string robotId,
        MissionCommandType commandType,
        UserRole requestedByRole,
        MissionCommandResult result)
        => new(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            robotId,
            commandType,
            requestedByRole,
            result.Message,
            result.Status,
            result.CompletedAt);
}
