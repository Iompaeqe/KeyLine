using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

namespace MacroSpammer.Services.Playback;

public sealed class PlaybackController
{
    private readonly Dictionary<MacroTimeline, MacroRunner> _runners = new();
    private readonly HashSet<MacroWorkspace> _shortcutStartingWorkspaces = new();

    public bool StopRequested { get; private set; }
    public bool AnyRunnerRunning => _runners.Values.Any(runner => runner.IsRunning);
    public bool AnyRunnerPaused => _runners.Values.Any(runner => runner.IsPaused);

    public MacroRunner GetRunner(MacroTimeline timeline)
    {
        if (_runners.TryGetValue(timeline, out var runner))
            return runner;

        runner = new MacroRunner();
        _runners[timeline] = runner;
        return runner;
    }

    public bool TryGetRunner(MacroTimeline timeline, out MacroRunner runner)
    {
        return _runners.TryGetValue(timeline, out runner!);
    }

    public void RemoveRunner(MacroTimeline timeline)
    {
        _runners.Remove(timeline);
        StopRequested = false;
    }

    public bool MarkShortcutStarting(MacroWorkspace workspace)
    {
        return _shortcutStartingWorkspaces.Add(workspace);
    }

    public void UnmarkShortcutStarting(MacroWorkspace workspace)
    {
        _shortcutStartingWorkspaces.Remove(workspace);
    }

    public bool IsWorkspaceRunning(MacroWorkspace workspace)
    {
        if (_shortcutStartingWorkspaces.Contains(workspace))
            return true;

        return workspace.Document.Timelines.Any(timeline =>
            _runners.TryGetValue(timeline, out var runner) && runner.IsRunning);
    }

    public bool IsWorkspacePaused(MacroWorkspace workspace)
    {
        return workspace.Document.Timelines.Any(timeline =>
            _runners.TryGetValue(timeline, out var runner) &&
            runner.IsRunning &&
            runner.IsPaused);
    }

    public void StopAll()
    {
        StopRequested = true;

        foreach (var runner in _runners.Values)
            runner.Stop();
    }

    public void StopWorkspace(MacroWorkspace workspace)
    {
        StopRequested = true;

        foreach (var timeline in workspace.Document.Timelines)
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Stop();
        }

        _shortcutStartingWorkspaces.Remove(workspace);
    }

    public void PauseWorkspace(MacroWorkspace workspace)
    {
        foreach (var timeline in workspace.Document.Timelines)
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Pause();
        }
    }

    public void ResumeWorkspace(MacroWorkspace workspace)
    {
        foreach (var timeline in workspace.Document.Timelines)
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Resume();
        }
    }

    public void PauseAll()
    {
        foreach (var runner in _runners.Values)
            runner.Pause();
    }

    public void ResumeAll()
    {
        foreach (var runner in _runners.Values)
            runner.Resume();
    }

    public void PrepareManualStart()
    {
        StopRequested = false;
    }

    public Task RunAsyncPlayback(
        nint targetHwnd,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null)
    {
        StopRequested = false;
        var tasks = new List<Task>();

        for (var i = 0; i < runnableTimelines.Count; i++)
        {
            var timeline = runnableTimelines[i];
            var runnerIndex = i;
            var runner = GetRunner(timeline);
            var steps = timeline.Nodes.ToList();
            var loopCount = Math.Max(0, timeline.LoopCount);
            var baseDelayMs = Math.Max(0, timeline.BaseDelayMs);
            var useStandardDelay = timeline.UseStandardDelay;
            var standardDelayMs = timeline.StandardDelayMs;
            var useTextInputMode = timeline.UseTextInputMode;

            tasks.Add(Task.Run(() => runner.StartAsync(
                targetHwnd,
                steps,
                loopCount,
                baseDelayMs,
                useStandardDelay,
                standardDelayMs,
                useTextInputMode,
                onRunnerLoopCompleted == null ? null : () => onRunnerLoopCompleted(runnerIndex))));
        }

        return Task.WhenAll(tasks);
    }

    public async Task RunSyncedPlayback(
        nint targetHwnd,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null)
    {
        StopRequested = false;
        var completedLoops = new int[runnableTimelines.Count];

        while (!StopRequested)
        {
            var tasks = new List<Task>();
            var startedIndexes = new List<int>();

            for (var i = 0; i < runnableTimelines.Count; i++)
            {
                var timeline = runnableTimelines[i];
                var targetLoops = Math.Max(0, timeline.LoopCount);

                if (targetLoops > 0 && completedLoops[i] >= targetLoops)
                    continue;

                var runnerIndex = i;
                var runner = GetRunner(timeline);
                var steps = timeline.Nodes.ToList();
                var baseDelayMs = targetLoops == 0
                    ? Math.Max(10, timeline.BaseDelayMs)
                    : Math.Max(0, timeline.BaseDelayMs);
                var useStandardDelay = timeline.UseStandardDelay;
                var standardDelayMs = timeline.StandardDelayMs;
                var useTextInputMode = timeline.UseTextInputMode;

                startedIndexes.Add(i);
                tasks.Add(Task.Run(() => runner.StartAsync(
                    targetHwnd,
                    steps,
                    1,
                    baseDelayMs,
                    useStandardDelay,
                    standardDelayMs,
                    useTextInputMode,
                    onRunnerLoopCompleted == null ? null : () => onRunnerLoopCompleted(runnerIndex))));
            }

            if (tasks.Count == 0)
                break;

            await Task.WhenAll(tasks);

            if (StopRequested)
                break;

            foreach (var index in startedIndexes)
                completedLoops[index]++;
        }
    }

    public async Task RunSequencePlayback(
        nint targetHwnd,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null)
    {
        StopRequested = false;
        var completedLoops = new int[runnableTimelines.Count];

        while (!StopRequested)
        {
            var startedAnyTimeline = false;

            for (var i = 0; i < runnableTimelines.Count; i++)
            {
                if (StopRequested)
                    break;

                var timeline = runnableTimelines[i];
                var targetLoops = Math.Max(0, timeline.LoopCount);

                if (targetLoops > 0 && completedLoops[i] >= targetLoops)
                    continue;

                startedAnyTimeline = true;

                var runner = GetRunner(timeline);
                var steps = timeline.Nodes.ToList();
                var useStandardDelay = timeline.UseStandardDelay;
                var standardDelayMs = timeline.StandardDelayMs;
                var useTextInputMode = timeline.UseTextInputMode;

                await Task.Run(() => runner.StartAsync(
                    targetHwnd,
                    steps,
                    1,
                    0,
                    useStandardDelay,
                    standardDelayMs,
                    useTextInputMode)).ConfigureAwait(false);

                if (StopRequested)
                    break;

                completedLoops[i]++;
                onRunnerLoopCompleted?.Invoke(i);

                if (!HasEligibleSequenceTimeline(runnableTimelines, completedLoops))
                    continue;

                var delayMs = targetLoops == 0
                    ? Math.Max(10, timeline.BaseDelayMs)
                    : Math.Max(0, timeline.BaseDelayMs);

                await runner.DelayAsync(delayMs).ConfigureAwait(false);
            }

            if (!startedAnyTimeline)
                break;
        }
    }

    private static bool HasEligibleSequenceTimeline(
        IReadOnlyList<MacroTimeline> runnableTimelines,
        IReadOnlyList<int> completedLoops)
    {
        for (var i = 0; i < runnableTimelines.Count && i < completedLoops.Count; i++)
        {
            var targetLoops = Math.Max(0, runnableTimelines[i].LoopCount);
            if (targetLoops == 0 || completedLoops[i] < targetLoops)
                return true;
        }

        return false;
    }
}
