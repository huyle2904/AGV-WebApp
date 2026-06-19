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
        => _activeRuns.TryAdd(snapshot.Run.RobotId, snapshot);

    public TaskChainRunSnapshot? GetActiveRun(string? robotId = null)
    {
        if (!string.IsNullOrWhiteSpace(robotId))
        {
            return _activeRuns.TryGetValue(robotId, out var snapshot) ? snapshot : null;
        }

        return _activeRuns.Values
            .OrderByDescending(item => item.LastUpdated)
            .FirstOrDefault();
    }

    public IReadOnlyList<TaskChainRunSnapshot> GetActiveRuns()
        => _activeRuns.Values.OrderBy(item => item.Run.RobotId).ToList();

    public void UpdateActiveRun(TaskChainRunSnapshot snapshot)
    {
        _activeRuns[snapshot.Run.RobotId] = snapshot;
        UpdateTaskChainStatus(snapshot.Run.TaskChainName, snapshot.TaskChainStatus?.TaskListStatus);
    }

    public void CompleteRun(TaskChainRunSnapshot snapshot)
    {
        _activeRuns.TryRemove(snapshot.Run.RobotId, out _);
        UpdateTaskChainStatus(snapshot.Run.TaskChainName, snapshot.TaskChainStatus?.TaskListStatus);

        lock (_historyLock)
        {
            _recentRuns.Add(snapshot);
            if (_recentRuns.Count > 50)
            {
                _recentRuns.RemoveRange(0, _recentRuns.Count - 50);
            }
        }
    }

    public IReadOnlyList<TaskChainRunSnapshot> GetRecentRuns()
    {
        lock (_historyLock)
        {
            return _recentRuns.OrderByDescending(item => item.LastUpdated).ToList();
        }
    }
}
