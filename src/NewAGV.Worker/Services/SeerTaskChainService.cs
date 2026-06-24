using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NewAGV.Contracts;

namespace NewAGV.Worker.Services;

public sealed class SeerTaskChainService(
    SeerTcpClient tcpClient,
    IOptions<SeerRobotOptions> options,
    ILogger<SeerTaskChainService> logger)
{
    public async Task<IReadOnlyList<SeerTaskChainSummary>> GetTaskChainsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tcpClient.SendAsync(options.Value.NavigationPort, 3115, null, cancellationToken);
            EnsureSuccess(response, "load task chains");

            var createdOn = response.DateTimeOffsetValue("create_on");
            return response.StringArrayValue("tasklists")
                .Select(name => new SeerTaskChainSummary(name, createdOn, null))
                .OrderBy(item => item.Name)
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to load task chains from SEER. Returning an empty catalog.");
            return [];
        }
    }

    public async Task<SeerTaskChainStatus?> GetTaskChainStatusAsync(string taskChainName, bool withRobotStatus, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskChainName))
        {
            throw new InvalidOperationException("Task chain name is required.");
        }

        try
        {
            var response = await tcpClient.SendAsync(
                options.Value.NavigationPort,
                3101,
                new Dictionary<string, object?>
                {
                    ["task_list_name"] = taskChainName,
                    ["with_robot_status"] = withRobotStatus
                },
                cancellationToken);

            EnsureSuccess(response, $"query task chain '{taskChainName}'");

            var status = response.ObjectValue("tasklist_status")
                ?? throw new InvalidOperationException($"SEER did not return task chain details for '{taskChainName}'.");
            var robotStatus = response.ObjectValue("robot_status");

            return new SeerTaskChainStatus(
                status.StringValue("taskListName") ?? taskChainName,
                ToTaskChainStatus(status.IntValue("taskListStatus")),
                NormalizeTaskId(status.StringValue("taskId")),
                status.BoolValue("loop"),
                status.IntValue("actionGroupId"),
                status.IntArrayValue("actionIds"),
                ResolveBatteryPercent(robotStatus));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to query task chain status for {TaskChainName}. Returning no task chain status.", taskChainName);
            return null;
        }
    }

    public async Task<TaskChainRunResult> ExecuteTaskChainAsync(TaskChainRunRequest request, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        try
        {
            var response = await tcpClient.SendAsync(
                options.Value.NavigationPort,
                3106,
                new Dictionary<string, object?>
                {
                    ["name"] = request.TaskChainName
                },
                cancellationToken);

            var retCode = response.IntValue("ret_code") ?? 0;
            var error = response.StringValue("err_msg");
            return new TaskChainRunResult(
                runId,
                request.RobotId,
                request.TaskChainName,
                retCode == 0 ? TaskChainRunStatus.Accepted : TaskChainRunStatus.Rejected,
                retCode == 0
                    ? $"TaskChain '{request.TaskChainName}' accepted by SEER AGV."
                    : $"TaskChain '{request.TaskChainName}' rejected by SEER AGV. ret_code={retCode}. {error}",
                startedAt,
                retCode == 0 ? null : DateTimeOffset.UtcNow,
                null);
        }
        catch (Exception exception)
        {
            return new TaskChainRunResult(
                runId,
                request.RobotId,
                request.TaskChainName,
                TaskChainRunStatus.Rejected,
                exception.Message,
                startedAt,
                DateTimeOffset.UtcNow,
                null);
        }
    }

    public async Task<IReadOnlyList<SeerTaskRuntimeStatus>> GetTaskRuntimeStatusesAsync(string? taskId, CancellationToken cancellationToken)
    {
        object? payload = null;
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            payload = new Dictionary<string, object?>
            {
                ["task_ids"] = new[] { taskId }
            };
        }

        try
        {
            var response = await tcpClient.SendAsync(options.Value.StatusPort, 1110, payload, cancellationToken);
            EnsureSuccess(response, "query task runtime status");

            var package = response.ObjectValue("task_status_package");
            if (package is null)
            {
                return [];
            }

            var closestTarget = package.StringValue("closest_target");
            var sourceName = package.StringValue("source_name");
            var targetName = package.StringValue("target_name");
            var percentage = package.DoubleValue("percentage");
            var distance = package.DoubleValue("distance");
            var info = package.StringValue("info");

            return package.ArrayValue("task_status_list")?
                .OfType<JsonObject>()
                .Select(item => ToRuntimeStatus(item, closestTarget, sourceName, targetName, percentage, distance, info))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to query SEER task runtime status. Returning no runtime rows.");
            return [];
        }
    }

    public Task<MissionCommandResult> PauseAsync(CancellationToken cancellationToken)
        => SendControlAsync(MissionCommandType.Pause, 3001, cancellationToken);

    public Task<MissionCommandResult> ResumeAsync(CancellationToken cancellationToken)
        => SendControlAsync(MissionCommandType.Resume, 3002, cancellationToken);

    public Task<MissionCommandResult> CancelAsync(CancellationToken cancellationToken)
        => SendControlAsync(MissionCommandType.Cancel, 3003, cancellationToken);

    private async Task<MissionCommandResult> SendControlAsync(
        MissionCommandType commandType,
        ushort apiNumber,
        CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var commandId = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        try
        {
            var response = await tcpClient.SendAsync(options.Value.NavigationPort, apiNumber, null, cancellationToken);
            var retCode = response.IntValue("ret_code") ?? 0;
            var error = response.StringValue("err_msg");

            return new MissionCommandResult(
                commandId,
                options.Value.RobotIdFallback,
                commandType,
                retCode == 0 ? MissionCommandStatus.Accepted : MissionCommandStatus.Rejected,
                retCode == 0
                    ? $"{commandType} accepted by SEER AGV."
                    : $"{commandType} rejected by SEER AGV. ret_code={retCode}. {error}",
                requestedAt,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            return new MissionCommandResult(
                commandId,
                options.Value.RobotIdFallback,
                commandType,
                MissionCommandStatus.Rejected,
                exception.Message,
                requestedAt,
                DateTimeOffset.UtcNow);
        }
    }

    private static SeerTaskRuntimeStatus? ToRuntimeStatus(
        JsonObject item,
        string? closestTarget,
        string? sourceName,
        string? targetName,
        double? percentage,
        double? distance,
        string? info)
    {
        var taskId = item.StringValue("task_id");
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return null;
        }

        return new SeerTaskRuntimeStatus(
            taskId,
            ToTaskChainStatus(item.IntValue("status")),
            item.IntValue("type"),
            closestTarget,
            sourceName,
            targetName,
            percentage,
            distance,
            info);
    }

    private static TaskChainStatus ToTaskChainStatus(int? status)
        => status switch
        {
            1 => TaskChainStatus.Waiting,
            2 => TaskChainStatus.Running,
            3 => TaskChainStatus.Suspended,
            4 => TaskChainStatus.Completed,
            5 => TaskChainStatus.Failed,
            6 => TaskChainStatus.Canceled,
            7 => TaskChainStatus.OverTime,
            404 => TaskChainStatus.NotFound,
            _ => TaskChainStatus.None
        };

    private static string? NormalizeTaskId(string? taskId)
        => string.IsNullOrWhiteSpace(taskId) || taskId == "0"
            ? null
            : taskId;

    private static int? ResolveBatteryPercent(JsonObject? robotStatus)
    {
        if (robotStatus is null)
        {
            return null;
        }

        var batteryLevel = robotStatus.DoubleValue("battery_level") ?? robotStatus.DoubleValue("electric_quantity");
        if (batteryLevel is null)
        {
            return null;
        }

        return batteryLevel > 1
            ? (int)Math.Round(batteryLevel.Value)
            : Math.Clamp((int)Math.Round(batteryLevel.Value * 100), 0, 100);
    }

    private static void EnsureSuccess(JsonObject response, string action)
    {
        var retCode = response.IntValue("ret_code") ?? 0;
        if (retCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"SEER failed to {action}. ret_code={retCode}. {response.StringValue("err_msg")}");
    }
}
