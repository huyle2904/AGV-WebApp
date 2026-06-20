using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAGV.Api.Data;
using NewAGV.Api.Services;
using NewAGV.Contracts;

namespace NewAGV.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IntegrationController(
    AgvGatewayClient gatewayClient,
    NewAgvDbContext dbContext) : ControllerBase
{
    [HttpGet("gateway")]
    public async Task<ActionResult<GatewayHealth>> GetGatewayHealth(CancellationToken cancellationToken)
    {
        return Ok(await gatewayClient.GetHealthAsync(cancellationToken));
    }

    [HttpGet("database")]
    public async Task<ActionResult<object>> GetDatabaseHealth(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        var provider = dbContext.Database.ProviderName ?? "unknown";
        var defaultSchema = dbContext.Model.GetDefaultSchema() ?? "public";

        return Ok(new
        {
            connected = canConnect,
            provider,
            defaultSchema
        });
    }
}
