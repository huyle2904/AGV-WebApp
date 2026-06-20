using System.Collections.Concurrent;
using NewAGV.Contracts;

namespace NewAGV.Api.Services;

public sealed class TaskChainStore
{
    private readonly ConcurrentDictionary<string, TaskChainRunSnapshot> _activeRuns = new();
    private readonly List<SeerTaskChainSummary> _taskChains = [];
    private readonly List<TaskChainRunSnapshot> _recentRuns = [];
    private readonly object _catalogLock = new();
    private readonly object _historyLock = new();
    private static readonly TimeSpan CompletedRunRetention = TimeSpan.FromSeconds(15);

    public IReadOnlyList<SeerTaskChainSummary> GetTaskChains()
    {
        lock (_catalogLock)
        {
            return _taskChains.OrderBy(item => item.Name).ToList();
        }
    }

    public void ReplaceTaskChains(IEnumerable<SeerTaskChainSummary> taskChains)
    {
        lock (_catalogLock)
        {
            var statuses = _taskChains.ToDictionary(item => item.Name, item => item.LastKnownStatus, StringComparer.OrdinalIgnoreCase);
            _taskChains.Clear();
            _taskChains.AddRange(taskChains.Select(item => item with
            {
                LastKnownStatus = item.LastKnownStatus ?? statuses.GetValueOrDefault(item.Name)
            }));
        }
    }

    public void UpdateTaskChainStatus(string taskChainName, TaskChainStatus? status)
    {
        if (string.IsNullOrWhiteSpace(taskChainName))
        {
            return;
        }

        lock (_catalogLock)
        {
            var index = _taskChains.FindIndex(item => string.Equals(item.Name, taskChainName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _taskChains[index] = _taskChains[index] with { LastKnownStatus = status };
            }
        }
    }

    public bool TryStartRun(TaskChainRunSnapshot snapshot)
    {
        PurgeCompletedRuns();

        if (_activeRuns.TryGetValue(snapshot.Run.RobotId, out var existing)
            && IsTerminal(existing.Run.Status))
        {
            _activeRuns.TryRemove(snapshot.Run.RobotId, out _);
        }

        return _activeRuns.TryAdd(snapshot.Run.RobotId, snapshot);
    }

    public TaskChainRunSnapshot? GetActiveRun(string? robotId = null)
    {
        PurgeCompletedRuns();

        if (!string.IsNullOrWhiteSpace(robotId))
        {
            return _activeRuns.TryGetValue(robotId, out var snapshot) ? snapshot : null;
        }

        return _activeRuns.Values
            .OrderByDescending(item => item.LastUpdated)
            .FirstOrDefault();
    }

    public IReadOnlyList<TaskChainRunSnapshot> GetActiveRuns()
    {
        PurgeCompletedRuns();
        return _activeRuns.Values.OrderBy(item => item.Run.RobotId).ToList();
    }

    public void UpdateActiveRun(TaskChainRunSnapshot snapshot)
    {
        _activeRuns[snapshot.Run.RobotId] = snapshot;
        UpdateTaskChainStatus(snapshot.Run.TaskChainName, snapshot.TaskChainStatus?.TaskListStatus);
    }

    public void CompleteRun(TaskChainRunSnapshot snapshot)
    {
        _activeRuns[snapshot.Run.RobotId] = snapshot;
        UpdateTaskChainStatus(snapshot.Run.TaskChainName, ResolveTerminalChainStatus(snapshot.Run.Status, snapshot.TaskChainStatus?.TaskListStatus));

        lock (_historyLock)
        {
            _recentRuns.Add(snapshot);
            if (_recentRuns.Count > 50)
            {
                _recentRuns.RemoveRange(0, _recentRuns.Count - 50);
            }
        }
    }

    public IReadOnlyList<TaskChainRunSnapshot> GetRecentRuns(string? robotId = null)
    {
        lock (_historyLock)
        {
            return _recentRuns
                .Where(item => string.IsNullOrWhiteSpace(robotId) || string.Equals(item.Run.RobotId, robotId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.LastUpdated)
                .ToList();
        }
    }

    private void PurgeCompletedRuns()
    {
        var cutoff = DateTimeOffset.UtcNow - CompletedRunRetention;
        foreach (var kvp in _activeRuns)
        {
            if (kvp.Value.Run.CompletedAt is not null && kvp.Value.Run.CompletedAt < cutoff)
            {
                _activeRuns.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static bool IsTerminal(TaskChainRunStatus status)
        => status is TaskChainRunStatus.Completed
            or TaskChainRunStatus.Failed
            or TaskChainRunStatus.Canceled
            or TaskChainRunStatus.OverTime
            or TaskChainRunStatus.Rejected;

    private static TaskChainStatus? ResolveTerminalChainStatus(TaskChainRunStatus runStatus, TaskChainStatus? fallback)
        => runStatus switch
        {
            TaskChainRunStatus.Completed => TaskChainStatus.Completed,
            TaskChainRunStatus.Failed => TaskChainStatus.Failed,
            TaskChainRunStatus.Canceled => TaskChainStatus.Canceled,
            TaskChainRunStatus.OverTime => TaskChainStatus.OverTime,
            _ => fallback
        };
}

