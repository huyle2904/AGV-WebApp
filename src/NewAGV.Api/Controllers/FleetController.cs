using Microsoft.AspNetCore.Mvc;
using NewAGV.Contracts;
using NewAGV.Api.Services;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FleetController(AgvPlantStore store) : ControllerBase
{
    [HttpGet("robots")]
    public ActionResult<IReadOnlyList<RobotSummary>> GetRobots()
    {
        return Ok(store.GetRobots());
    }

    [HttpGet("health")]
    public ActionResult<SiteHealth> GetHealth()
    {
        return Ok(store.Health);
    }

    [HttpGet("robots/{robotId}/detail")]
    public ActionResult<RobotTelemetryDetail> GetRobotDetail(string robotId)
    {
        var detail = store.GetRobotDetail(robotId);
        return detail is null ? NotFound() : Ok(detail);
    }
}
