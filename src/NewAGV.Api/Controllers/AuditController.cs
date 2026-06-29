using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuditController(AuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MissionAuditEntry>>> GetAudits(CancellationToken cancellationToken)
    {
        return Ok(await auditLogService.GetRecentAsync(cancellationToken));
    }
}
