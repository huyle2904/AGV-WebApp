using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuditController(AgvPlantStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<MissionAuditEntry>> GetAudits()
    {
        return Ok(store.GetAudits());
    }
}
