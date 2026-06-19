using Microsoft.AspNetCore.SignalR;
using NewAGV.Api.Hubs;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class TaskChainCoordinator(
    TaskChainStore store,
    AgvPlantStore plantStore,
    SeerWorkerClient workerClient,
    IHubContext<TelemetryHub> hubContext)
{
    private static readonly TimeSpan UnknownTaskIdThreshold = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<SeerTaskChainSummary>> GetTaskChainsAsync(CancellationToken cancellationToken)
    {
        var taskChains = await workerClient.GetTaskChainsAsync(cancellationToken);
        store.ReplaceTaskChains(taskChains);
        return store.GetTaskChains();
    }

    public async Task<SeerTaskChainStatus?> GetTaskChainStatusAsync(string taskChainName, CancellationToken cancellationToken)
    {
        var status = await workerClient.GetTaskChainAsync(taskChainName, true, cancellationToken);
        if (status is not null)
        {
            store.UpdateTaskChainStatus(taskChainName, status.TaskListStatus);
        }

        return status;
    }

    public TaskChainRunSnapshot? GetActiveRun(string? robotId = null)
        => store.GetActiveRun(robotId);

    public async Task<TaskChainRunResult> ExecuteAsync(TaskChainRunRequest request, UserRole requestedByRole, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var robot = plantStore.GetRobot(request.RobotId);
        if (robot is null)
        {
            return BuildRejectedRun(request, "Robot was not found.", requestedAt);
        }

        if (!request.Confirmed)
        {
            return BuildRejectedRun(request, "TaskChain execution requires explicit confirmation.", requestedAt);
        }

        var safetyRejection = BuildSafetyRejection(robot, request.RobotId);
        if (safetyRejection is not null)
        {
            return BuildRejectedRun(request, safetyRejection, requestedAt);
        }

        if (store.GetActiveRun(request.RobotId) is not null)
        {
            return BuildRejectedRun(request, "Another TaskChain is already active on this robot.", requestedAt);
        }

        var result = await workerClient.ExecuteTaskChainAsync(request, cancellationToken);
        AddAudit(
            request.RobotId,
            requestedByRole,
            result.Message,
            result.Status == TaskChainRunStatus.Rejected ? MissionCommandStatus.Rejected : MissionCommandStatus.Accepted,
            $"TaskChainExecute:{request.TaskChainName}");

        if (result.Status == TaskChainRunStatus.Rejected)
        {
            return result;
        }

        var snapshot = new TaskChainRunSnapshot(result, null, null, requestedByRole, DateTimeOffset.UtcNow);
        if (!store.TryStartRun(snapshot))
        {
            return BuildRejectedRun(request, "Another TaskChain became active before this run started.", requestedAt);
        }

        await EmitAsync("taskchain.started", snapshot, result.Message, cancellationToken);
        return result;
    }

    public async Task<MissionCommandResult> PauseAsync(UserRole requestedByRole, CancellationToken cancellationToken)
        => await SendControlAsync(
            MissionCommandType.Pause,
            requestedByRole,
            "TaskChainPause",
            workerClient.PauseTaskChainAsync,
            requireFullSafety: true,
            cancellationToken);

    public async Task<MissionCommandResult> ResumeAsync(UserRole requestedByRole, CancellationToken cancellationToken)
        => await SendControlAsync(
            MissionCommandType.Resume,
            requestedByRole,
            "TaskChainResume",
            workerClient.ResumeTaskChainAsync,
            requireFullSafety: true,
            cancellationToken);

    public async Task<MissionCommandResult> CancelAsync(UserRole requestedByRole, CancellationToken cancellationToken)
        => await SendControlAsync(
            MissionCommandType.Cancel,
            requestedByRole,
            "TaskChainCancel",
            workerClient.CancelTaskChainAsync,
            requireFullSafety: false,
            cancellationToken);

    public async Task PollActiveRunsAsync(CancellationToken cancellationToken)
    {
        foreach (var snapshot in store.GetActiveRuns())
        {
            await RefreshActiveRunAsync(snapshot, cancellationToken);
        }
    }

    public async Task RefreshActiveRunAsync(TaskChainRunSnapshot snapshot, CancellationToken cancellationToken)
    {
        var chainStatus = await workerClient.GetTaskChainAsync(snapshot.Run.TaskChainName, true, cancellationToken);
        var runtimeStatuses = await workerClient.GetTaskRuntimeStatusesAsync(snapshot.Run.TaskId, cancellationToken);
        var runtimeStatus = SelectRuntimeStatus(snapshot.Run.TaskId, runtimeStatuses);
        var discoveredTaskId = snapshot.Run.TaskId
            ?? runtimeStatus?.TaskId
            ?? chainStatus?.TaskId;

        if (chainStatus is not null && discoveredTaskId is not null && chainStatus.TaskId != discoveredTaskId)
        {
            chainStatus = chainStatus with { TaskId = discoveredTaskId };
        }

        if (runtimeStatus is null && discoveredTaskId is not null)
        {
            runtimeStatus = runtimeStatuses.FirstOrDefault(item => item.TaskId == discoveredTaskId);
        }

        var status = ResolveRunStatus(snapshot.Run.StartedAt, chainStatus, runtimeStatus, discoveredTaskId);
        var completedAt = IsTerminal(status) ? DateTimeOffset.UtcNow : null;
        var message = BuildRunMessage(snapshot.Run.TaskChainName, status, discoveredTaskId, runtimeStatus?.Info);
        var updatedRun = snapshot.Run with
        {
            Status = status,
            TaskId = discoveredTaskId,
            Message = message,
            CompletedAt = completedAt
        };

        if (updatedRun == snapshot.Run && chainStatus == snapshot.TaskChainStatus && runtimeStatus == snapshot.RuntimeStatus)
        {
            return;
        }

        var updated = new TaskChainRunSnapshot(
            updatedRun,
            chainStatus,
            runtimeStatus,
            snapshot.RequestedByRole,
            DateTimeOffset.UtcNow);

        if (IsTerminal(status))
        {
            store.CompleteRun(updated);
            AddAudit(
                updated.Run.RobotId,
                updated.RequestedByRole,
                updated.Run.Message,
                ToAuditStatus(status),
                $"TaskChainExecute:{updated.Run.TaskChainName}");
            await EmitAsync(ToRealtimeEvent(status), updated, updated.Run.Message, cancellationToken);
            return;
        }

        store.UpdateActiveRun(updated);
        await EmitAsync("taskchain.updated", updated, updated.Run.Message, cancellationToken);
    }

    private async Task<MissionCommandResult> SendControlAsync(
        MissionCommandType commandType,
        UserRole requestedByRole,
        string operation,
        Func<CancellationToken, Task<MissionCommandResult>> action,
        bool requireFullSafety,
        CancellationToken cancellationToken)
    {
        var activeRun = store.GetActiveRun();
        if (activeRun is null)
        {
            return new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                string.Empty,
                MissionCommandType.Cancel,
                MissionCommandStatus.Rejected,
                "No active TaskChain run was found.",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        }

        var robot = plantStore.GetRobot(activeRun.Run.RobotId);
        if (robot is null)
        {
            return new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                activeRun.Run.RobotId,
                MissionCommandType.Cancel,
                MissionCommandStatus.Rejected,
                "Robot was not found.",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        }

        var rejection = BuildSafetyRejection(robot, activeRun.Run.RobotId);
        if (rejection is not null && requireFullSafety)
        {
            return new MissionCommandResult(
                Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                activeRun.Run.RobotId,
                MissionCommandType.Cancel,
                MissionCommandStatus.Rejected,
                rejection,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        }

        var result = await action(cancellationToken);
        AddAudit(activeRun.Run.RobotId, requestedByRole, result.Message, result.Status, operation);

        if (result.Status == MissionCommandStatus.Accepted)
        {
            await RefreshActiveRunAsync(activeRun, cancellationToken);
        }

        return result;
    }

    private TaskChainRunResult BuildRejectedRun(TaskChainRunRequest request, string message, DateTimeOffset requestedAt)
        => new(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            request.RobotId,
            request.TaskChainName,
            TaskChainRunStatus.Rejected,
            message,
            requestedAt,
            DateTimeOffset.UtcNow,
            null);

    private string? BuildSafetyRejection(RobotSummary robot, string robotId)
    {
        if (robot.Connectivity != ConnectivityStatus.Online)
        {
            return "Robot TCP link is not online.";
        }

        var detail = plantStore.GetRobotDetail(robotId);
        if (detail is null)
        {
            return "Live robot telemetry is not available yet.";
        }

        if (detail.Estop?.Emergency == true ||
            detail.Estop?.DriverEmergency == true ||
            detail.Estop?.SoftEmergency == true)
        {
            return "E-stop or driver emergency is active.";
        }

        if (detail.ControlOwner?.Locked == true)
        {
            var owner = detail.ControlOwner.NickName ?? detail.ControlOwner.Ip ?? "another controller";
            return $"Robot control is locked by {owner}.";
        }

        if (detail.Alarm is { FatalCount: > 0 } or { ErrorCount: > 0 })
        {
            return "Robot has active fatal/error alarms.";
        }

        if (detail.Localization?.ReadyForNavigation != true)
        {
            return $"Localization is not ready: {detail.Localization?.StatusLabel ?? "unknown"}.";
        }

        return null;
    }

    private void AddAudit(
        string robotId,
        UserRole requestedByRole,
        string message,
        MissionCommandStatus status,
        string operation)
    {
        plantStore.AddAudit(new MissionAuditEntry(
            Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            robotId,
            null,
            requestedByRole,
            message,
            status,
            DateTimeOffset.UtcNow,
            operation));
    }

    private async Task EmitAsync(
        string eventType,
        TaskChainRunSnapshot snapshot,
        string message,
        CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync(
            "ReceiveTelemetry",
            new RealtimeEvent(eventType, DateTimeOffset.UtcNow, TaskChainRun: snapshot, Message: message),
            cancellationToken);
    }

    private static SeerTaskRuntimeStatus? SelectRuntimeStatus(string? currentTaskId, IReadOnlyList<SeerTaskRuntimeStatus> runtimeStatuses)
    {
        if (!string.IsNullOrWhiteSpace(currentTaskId))
        {
            var matched = runtimeStatuses.FirstOrDefault(item => item.TaskId == currentTaskId);
            if (matched is not null)
            {
                return matched;
            }
        }

        var preferred = runtimeStatuses.FirstOrDefault(item => item.Status is TaskChainStatus.Running or TaskChainStatus.Waiting or TaskChainStatus.Suspended);
        return preferred ?? runtimeStatuses.FirstOrDefault();
    }

    private static TaskChainRunStatus ResolveRunStatus(
        DateTimeOffset startedAt,
        SeerTaskChainStatus? chainStatus,
        SeerTaskRuntimeStatus? runtimeStatus,
        string? taskId)
    {
        if (runtimeStatus is not null)
        {
            return ToRunStatus(runtimeStatus.Status);
        }

        if (chainStatus is not null && chainStatus.TaskListStatus is not TaskChainStatus.None and not TaskChainStatus.NotFound)
        {
            return ToRunStatus(chainStatus.TaskListStatus);
        }

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            return TaskChainRunStatus.Accepted;
        }

        return DateTimeOffset.UtcNow - startedAt >= UnknownTaskIdThreshold
            ? TaskChainRunStatus.UnknownTaskId
            : TaskChainRunStatus.Accepted;
    }

    private static TaskChainRunStatus ToRunStatus(TaskChainStatus status)
        => status switch
        {
            TaskChainStatus.Waiting => TaskChainRunStatus.Waiting,
            TaskChainStatus.Running => TaskChainRunStatus.Running,
            TaskChainStatus.Suspended => TaskChainRunStatus.Suspended,
            TaskChainStatus.Completed => TaskChainRunStatus.Completed,
            TaskChainStatus.Failed => TaskChainRunStatus.Failed,
            TaskChainStatus.Canceled => TaskChainRunStatus.Canceled,
            TaskChainStatus.OverTime => TaskChainRunStatus.OverTime,
            _ => TaskChainRunStatus.Accepted
        };

    private static bool IsTerminal(TaskChainRunStatus status)
        => status is TaskChainRunStatus.Completed
            or TaskChainRunStatus.Failed
            or TaskChainRunStatus.Canceled
            or TaskChainRunStatus.OverTime
            or TaskChainRunStatus.Rejected;

    private static string ToRealtimeEvent(TaskChainRunStatus status)
        => status switch
        {
            TaskChainRunStatus.Completed => "taskchain.completed",
            TaskChainRunStatus.Failed => "taskchain.failed",
            TaskChainRunStatus.Canceled => "taskchain.canceled",
            TaskChainRunStatus.OverTime => "taskchain.failed",
            _ => "taskchain.updated"
        };

    private static MissionCommandStatus ToAuditStatus(TaskChainRunStatus status)
        => status switch
        {
            TaskChainRunStatus.Completed => MissionCommandStatus.Completed,
            TaskChainRunStatus.Failed => MissionCommandStatus.Rejected,
            TaskChainRunStatus.Canceled => MissionCommandStatus.Completed,
            TaskChainRunStatus.OverTime => MissionCommandStatus.Rejected,
            TaskChainRunStatus.Rejected => MissionCommandStatus.Rejected,
            _ => MissionCommandStatus.Accepted
        };

    private static string BuildRunMessage(string taskChainName, TaskChainRunStatus status, string? taskId, string? info)
    {
        var suffix = string.IsNullOrWhiteSpace(taskId) ? string.Empty : $" task_id={taskId}.";
        return status switch
        {
            TaskChainRunStatus.Accepted => $"TaskChain '{taskChainName}' accepted by SEER AGV. Waiting for runtime state.",
            TaskChainRunStatus.UnknownTaskId => $"TaskChain '{taskChainName}' accepted, but runtime task id is not available yet.",
            TaskChainRunStatus.Waiting => $"TaskChain '{taskChainName}' is waiting to run.{suffix}",
            TaskChainRunStatus.Running => $"TaskChain '{taskChainName}' is running.{suffix} {info}".Trim(),
            TaskChainRunStatus.Suspended => $"TaskChain '{taskChainName}' is suspended.{suffix}",
            TaskChainRunStatus.Completed => $"TaskChain '{taskChainName}' completed successfully.{suffix}",
            TaskChainRunStatus.Failed => $"TaskChain '{taskChainName}' failed.{suffix} {info}".Trim(),
            TaskChainRunStatus.Canceled => $"TaskChain '{taskChainName}' was canceled.{suffix}",
            TaskChainRunStatus.OverTime => $"TaskChain '{taskChainName}' exceeded its allowed runtime.{suffix}",
            _ => $"TaskChain '{taskChainName}' state changed.{suffix}"
        };
    }
}


