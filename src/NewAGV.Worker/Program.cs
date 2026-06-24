using NewAGV.Contracts;
using NewAGV.Worker;
using NewAGV.Worker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SeerRobotOptions>(builder.Configuration.GetSection(SeerRobotOptions.SectionName));
builder.Services.AddHttpClient<ApiSyncClient>();
builder.Services.AddSingleton<SeerTcpClient>();
builder.Services.AddSingleton<SeerRobotMapper>();
builder.Services.AddSingleton<SeerCommandService>();
builder.Services.AddSingleton<SeerTaskChainService>();
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

app.MapPost("/internal/commands/teleop", async (
    TeleopRequest request,
    SeerCommandService commandService,
    CancellationToken cancellationToken) =>
{
    var result = await commandService.TeleopDriveAsync(request, cancellationToken);
    return Results.Json(result);
});

app.MapGet("/internal/taskchains", async (
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.GetTaskChainsAsync(cancellationToken);
    return Results.Json(result);
});

app.MapGet("/internal/taskchains/{name}", async (
    string name,
    bool? withRobotStatus,
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.GetTaskChainStatusAsync(name, withRobotStatus ?? true, cancellationToken);
    return Results.Json(result);
});

app.MapPost("/internal/taskchains/execute", async (
    TaskChainRunRequest request,
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.ExecuteTaskChainAsync(request, cancellationToken);
    return Results.Json(result);
});

app.MapGet("/internal/task-runtime", async (
    string? taskId,
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.GetTaskRuntimeStatusesAsync(taskId, cancellationToken);
    return Results.Json(result);
});

app.MapPost("/internal/taskchains/pause", async (
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.PauseAsync(cancellationToken);
    return Results.Json(result);
});

app.MapPost("/internal/taskchains/resume", async (
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.ResumeAsync(cancellationToken);
    return Results.Json(result);
});

app.MapPost("/internal/taskchains/cancel", async (
    SeerTaskChainService taskChainService,
    CancellationToken cancellationToken) =>
{
    var result = await taskChainService.CancelAsync(cancellationToken);
    return Results.Json(result);
});

app.Run();
