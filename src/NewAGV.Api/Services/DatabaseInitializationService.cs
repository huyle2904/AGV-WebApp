using Microsoft.EntityFrameworkCore;
using NewAGV.Api.Data;

namespace NewAGV.Api.Services;

public sealed class DatabaseInitializationService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NewAgvDbContext>();

        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("PostgreSQL schema is ready.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize PostgreSQL schema.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
