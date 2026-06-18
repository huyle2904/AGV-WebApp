using NewAGV.Contracts;
using NewAGV.Worker;
using NewAGV.Worker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SeerRobotOptions>(builder.Configuration.GetSection(SeerRobotOptions.SectionName));
builder.Services.AddHttpClient<ApiSyncClient>();
builder.Services.AddSingleton<SeerTcpClient>();
builder.Services.AddSingleton<SeerRobotMapper>();
builder.Services.AddSingleton<SeerCommandService>();
builder.Services.AddHostedService<SeerIntegrationWorker>();

var app = builder.Build();

app.MapPost("/internal/commands/dispatch", async (
    WorkerMissionCommandRequest request,
    SeerCommandService commandService,
    CancellationToken cancellationToken) =>
{
    var result = await commandService.DispatchAsync(request.Request, request.RequestedByRole, cancellationToken);
    return Results.Json(result);
});

app.MapPost("/internal/commands/relocate", async (
    SeerRelocationRequest request,
    SeerCommandService commandService,
    CancellationToken cancellationToken) =>
{
    var result = await commandService.RelocateAsync(request, cancellationToken);
    return Results.Json(result);
});

app.Run();
