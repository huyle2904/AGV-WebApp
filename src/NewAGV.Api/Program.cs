using NewAGV.Api.Hubs;
using NewAGV.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AgvPlantStore>();
builder.Services.AddSingleton<TaskChainStore>();
builder.Services.AddSingleton<CommandDispatcher>();
builder.Services.AddSingleton<TaskChainCoordinator>();
builder.Services.AddHttpClient<AgvGatewayClient>();
builder.Services.AddHttpClient<SeerWorkerClient>();
builder.Services.AddHostedService<TaskChainMonitorService>();

if (builder.Configuration.GetValue<bool>("Integration:EnableSimulation"))
{
    builder.Services.AddHostedService<TelemetrySimulationService>();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(origin => origin.Contains("localhost", StringComparison.OrdinalIgnoreCase));
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();
