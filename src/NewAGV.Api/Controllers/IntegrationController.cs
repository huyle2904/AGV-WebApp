using Microsoft.AspNetCore.Mvc;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IntegrationController(AgvGatewayClient gatewayClient) : ControllerBase
{
    [HttpGet("gateway")]
    public async Task<ActionResult<GatewayHealth>> GetGatewayHealth(CancellationToken cancellationToken)
    {
        return Ok(await gatewayClient.GetHealthAsync(cancellationToken));
    }
}
