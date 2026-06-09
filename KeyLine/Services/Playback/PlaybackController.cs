using KeyLine.Domain;
using KeyLine.Services.Macro;

namespace KeyLine.Services.Playback;

public enum TimelinePlaybackStatus
{
    Idle,
    Running,
    Waiting,
    Stopped,
    Warning
}

public sealed class PlaybackController : IMacroRunHost
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

        return EnumerateWorkspaceTimelines(workspace).Any(timeline =>
            _runners.TryGetValue(timeline, out var runner) && runner.IsRunning);
    }

    public bool IsWorkspacePaused(MacroWorkspace workspace)
    {
        return EnumerateWorkspaceTimelines(workspace).Any(timeline =>
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

        foreach (var timeline in EnumerateWorkspaceTimelines(workspace))
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Stop();
        }

        _shortcutStartingWorkspaces.Remove(workspace);
    }

    public void PauseWorkspace(MacroWorkspace workspace)
    {
        foreach (var timeline in EnumerateWorkspaceTimelines(workspace))
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Pause();
        }
    }

    public void ResumeWorkspace(MacroWorkspace workspace)
    {
        foreach (var timeline in EnumerateWorkspaceTimelines(workspace))
        {
            if (_runners.TryGetValue(timeline, out var runner))
                runner.Resume();
        }
    }

    // Normal timelines plus the workspace's Start/End hook timelines (which run their own
    // single-pass runners and must be stoppable/pausable alongside the body).
    private static IEnumerable<MacroTimeline> EnumerateWorkspaceTimelines(MacroWorkspace workspace)
    {
        foreach (var timeline in workspace.Document.Timelines)
            yield return timeline;

        yield return workspace.StartHookTimeline;
        yield return workspace.EndHookTimeline;
    }

    /// <summary>
    /// Runs a single timeline a single finite pass (used for Start/End hook timelines, and for
    /// Sequence/Random's one-timeline body). The loop count is forced to 1 so an infinite-loop
    /// hook can never hang the macro.
    /// </summary>
    public Task RunSingleTimelineAsync(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        MacroTimeline timeline,
        bool followForegroundWindow = false,
        Action<string>? onPlaybackFailure = null,
        MacroRunContext? runContext = null)
    {
        StopRequested = false;
        runContext ??= CreateRunContext(targetHwnd, workspace, activeProfileWorkspaces, followForegroundWindow);

        var runner = GetRunner(timeline);
        var steps = timeline.Nodes.ToList();

        return runner.StartAsync(
            targetHwnd,
            steps,
            loopCount: 1,
            baseDelayMs: Math.Max(0, timeline.BaseDelayMs),
            useStandardDelay: timeline.UseStandardDelay,
            standardDelayMs: timeline.StandardDelayMs,
            useTextInputMode: timeline.UseTextInputMode,
            runContext: runContext,
            reportFailure: onPlaybackFailure);
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
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null,
        Action<string>? onPlaybackFailure = null,
        MacroRunContext? runContext = null)
    {
        StopRequested = false;
        var tasks = new List<Task>();
        runContext ??= CreateRunContext(targetHwnd, workspace, activeProfileWorkspaces);

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

            tasks.Add(Task.Run(async () =>
            {
                onTimelineStatusChanged?.Invoke(runnerIndex, TimelinePlaybackStatus.Running);
                try
                {
                    await runner.StartAsync(
                        targetHwnd,
                        steps,
                        loopCount,
                        baseDelayMs,
                        useStandardDelay,
                        standardDelayMs,
                        useTextInputMode,
                        onRunnerLoopCompleted == null ? null : () => onRunnerLoopCompleted(runnerIndex),
                        runContext,
                        onPlaybackFailure);
                }
                finally
                {
                    onTimelineStatusChanged?.Invoke(runnerIndex, TimelinePlaybackStatus.Stopped);
                }
            }));
        }

        return Task.WhenAll(tasks);
    }

    public async Task RunSyncedPlayback(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null,
        Action<string>? onPlaybackFailure = null,
        MacroRunContext? runContext = null)
    {
        StopRequested = false;
        var completedLoops = new int[runnableTimelines.Count];
        runContext ??= CreateRunContext(targetHwnd, workspace, activeProfileWorkspaces);

        while (!StopRequested)
        {
            var tasks = new List<Task>();
            var startedIndexes = new List<int>();

            for (var i = 0; i < runnableTimelines.Count; i++)
            {
                var timeline = runnableTimelines[i];
                var targetLoops = Math.Max(0, timeline.LoopCount);

                if (targetLoops > 0 && completedLoops[i] >= targetLoops)
                {
                    onTimelineStatusChanged?.Invoke(i, TimelinePlaybackStatus.Stopped);
                    continue;
                }

                var runnerIndex = i;
                var runner = GetRunner(timeline);
                var steps = timeline.Nodes.ToList();
                var baseDelayMs = targetLoops == 0
                    ? Math.Max(10, timeline.BaseDelayMs)
                    : Math.Max(0, timeline.BaseDelayMs);
                var useStandardDelay = timeline.UseStandardDelay;
                var standardDelayMs = timeline.StandardDelayMs;
                var useTextInputMode = timeline.UseTextInputMode;
                var willExpireAfterThisPass = targetLoops > 0 && completedLoops[i] + 1 >= targetLoops;

                startedIndexes.Add(i);
                tasks.Add(Task.Run(async () =>
                {
                    onTimelineStatusChanged?.Invoke(runnerIndex, TimelinePlaybackStatus.Running);
                    await runner.StartAsync(
                        targetHwnd,
                        steps,
                        1,
                        baseDelayMs,
                        useStandardDelay,
                        standardDelayMs,
                        useTextInputMode,
                        onRunnerLoopCompleted == null ? null : () => onRunnerLoopCompleted(runnerIndex),
                        runContext,
                        onPlaybackFailure);

                    onTimelineStatusChanged?.Invoke(
                        runnerIndex,
                        willExpireAfterThisPass
                            ? TimelinePlaybackStatus.Stopped
                            : TimelinePlaybackStatus.Waiting);
                }));
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

    public async Task RunCyclePlayback(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null,
        Action<string>? onPlaybackFailure = null,
        MacroRunContext? runContext = null)
    {
        StopRequested = false;
        var completedLoops = new int[runnableTimelines.Count];
        runContext ??= CreateRunContext(targetHwnd, workspace, activeProfileWorkspaces);

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
                PublishCycleStatuses(runnableTimelines, completedLoops, i, onTimelineStatusChanged);

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
                    useTextInputMode,
                    runContext: runContext,
                    reportFailure: onPlaybackFailure)).ConfigureAwait(false);

                if (StopRequested)
                    break;

                completedLoops[i]++;
                onRunnerLoopCompleted?.Invoke(i);
                PublishCycleStatuses(runnableTimelines, completedLoops, null, onTimelineStatusChanged);

                if (!HasEligibleCycleTimeline(runnableTimelines, completedLoops))
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

    public async Task RunChainPlayback(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null,
        Action<string>? onPlaybackFailure = null,
        MacroRunContext? runContext = null)
    {
        StopRequested = false;
        var completedLoops = new int[runnableTimelines.Count];
        runContext ??= CreateRunContext(targetHwnd, workspace, activeProfileWorkspaces);

        for (var i = 0; i < runnableTimelines.Count; i++)
        {
            if (StopRequested)
                break;

            var timeline = runnableTimelines[i];
            var targetLoops = Math.Max(0, timeline.LoopCount);

            if (targetLoops > 0 && completedLoops[i] >= targetLoops)
                continue;

            PublishChainStatuses(runnableTimelines, completedLoops, i, onTimelineStatusChanged);

            var runnerIndex = i;
            var runner = GetRunner(timeline);
            var steps = timeline.Nodes.ToList();
            var baseDelayMs = targetLoops == 0
                ? Math.Max(10, timeline.BaseDelayMs)
                : Math.Max(0, timeline.BaseDelayMs);
            var useStandardDelay = timeline.UseStandardDelay;
            var standardDelayMs = timeline.StandardDelayMs;
            var useTextInputMode = timeline.UseTextInputMode;

            await Task.Run(() => runner.StartAsync(
                targetHwnd,
                steps,
                targetLoops,
                baseDelayMs,
                useStandardDelay,
                standardDelayMs,
                useTextInputMode,
                () =>
                {
                    completedLoops[runnerIndex]++;
                    onRunnerLoopCompleted?.Invoke(runnerIndex);
                },
                runContext,
                onPlaybackFailure)).ConfigureAwait(false);

            if (StopRequested)
                break;

            if (targetLoops > 0)
                completedLoops[i] = Math.Max(completedLoops[i], targetLoops);
        }

        PublishChainStatuses(runnableTimelines, completedLoops, null, onTimelineStatusChanged);
    }

    public string? ResolveMacroName(string macroId, MacroRunContext context)
    {
        return FindMacroInContext(macroId, context)?.Name;
    }

    public bool IsMacroRunning(string macroId, MacroRunContext context)
    {
        if (string.Equals(
                NormalizeMacroId(macroId),
                NormalizeMacroId(context.CurrentMacroId),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var workspace = FindMacroInContext(macroId, context);
        return workspace != null && IsWorkspaceRunning(workspace);
    }

    public async Task RunMacroAsync(
        string macroId,
        MacroRunContext context,
        Action<string>? reportFailure,
        CancellationToken token)
    {
        var targetWorkspace = FindMacroInContext(macroId, context);
        if (targetWorkspace == null)
        {
            reportFailure?.Invoke("Run Macro target is missing or outside the active profile.");
            return;
        }

        if (context.IsMacroInCallStack(targetWorkspace.Id))
        {
            reportFailure?.Invoke($"Run Macro recursion prevented: {targetWorkspace.Name}.");
            return;
        }

        if (IsWorkspaceRunning(targetWorkspace))
            return;

        var runnableTimelines = targetWorkspace.Document.Timelines
            .Where(timeline => timeline.Nodes.Count > 0)
            .ToList();

        if (runnableTimelines.Count == 0)
            return;

        var childContext = context.CreateChild(targetWorkspace);
        using var stopRegistration = token.Register(() => StopWorkspace(targetWorkspace));

        await RunPlaybackForWorkspaceAsync(
            context.CurrentTargetWindowHandle,
            targetWorkspace,
            context.ActiveProfileWorkspaces,
            runnableTimelines,
            childContext,
            reportFailure);
    }

    private Task RunPlaybackForWorkspaceAsync(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        MacroRunContext runContext,
        Action<string>? onPlaybackFailure)
    {
        return workspace.LoopMode switch
        {
            MacroLoopMode.Chain => RunChainPlayback(
                targetHwnd,
                workspace,
                activeProfileWorkspaces,
                runnableTimelines,
                onPlaybackFailure: onPlaybackFailure,
                runContext: runContext),
            MacroLoopMode.Cycle => RunCyclePlayback(
                targetHwnd,
                workspace,
                activeProfileWorkspaces,
                runnableTimelines,
                onPlaybackFailure: onPlaybackFailure,
                runContext: runContext),
            MacroLoopMode.Sync when runnableTimelines.Count > 1 => RunSyncedPlayback(
                targetHwnd,
                workspace,
                activeProfileWorkspaces,
                runnableTimelines,
                onPlaybackFailure: onPlaybackFailure,
                runContext: runContext),
            _ => RunAsyncPlayback(
                targetHwnd,
                workspace,
                activeProfileWorkspaces,
                runnableTimelines,
                onPlaybackFailure: onPlaybackFailure,
                runContext: runContext)
        };
    }

    public MacroRunContext CreateRunContext(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        bool followForegroundWindow = false)
    {
        return new MacroRunContext(
            targetHwnd,
            workspace.Id,
            activeProfileWorkspaces,
            this)
        {
            FollowForegroundWindow = followForegroundWindow
        };
    }

    private static MacroWorkspace? FindMacroInContext(string macroId, MacroRunContext context)
    {
        macroId = NormalizeMacroId(macroId);
        if (string.IsNullOrWhiteSpace(macroId))
            return null;

        return context.ActiveProfileWorkspaces.FirstOrDefault(workspace =>
            string.Equals(NormalizeMacroId(workspace.Id), macroId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMacroId(string? macroId) =>
        string.IsNullOrWhiteSpace(macroId) ? "" : macroId.Trim();

    private static bool HasEligibleCycleTimeline(
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

    private static void PublishCycleStatuses(
        IReadOnlyList<MacroTimeline> runnableTimelines,
        IReadOnlyList<int> completedLoops,
        int? runningIndex,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged)
    {
        if (onTimelineStatusChanged == null)
            return;

        for (var i = 0; i < runnableTimelines.Count && i < completedLoops.Count; i++)
        {
            var targetLoops = Math.Max(0, runnableTimelines[i].LoopCount);
            var isExpired = targetLoops > 0 && completedLoops[i] >= targetLoops;
            var status = isExpired
                ? TimelinePlaybackStatus.Stopped
                : runningIndex == i
                    ? TimelinePlaybackStatus.Running
                    : TimelinePlaybackStatus.Waiting;

            onTimelineStatusChanged(i, status);
        }
    }

    private static void PublishChainStatuses(
        IReadOnlyList<MacroTimeline> runnableTimelines,
        IReadOnlyList<int> completedLoops,
        int? runningIndex,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged)
    {
        if (onTimelineStatusChanged == null)
            return;

        for (var i = 0; i < runnableTimelines.Count && i < completedLoops.Count; i++)
        {
            var targetLoops = Math.Max(0, runnableTimelines[i].LoopCount);
            var isExpired = targetLoops > 0 && completedLoops[i] >= targetLoops;
            var status = isExpired || (runningIndex.HasValue && i < runningIndex.Value)
                ? TimelinePlaybackStatus.Stopped
                : runningIndex == i
                    ? TimelinePlaybackStatus.Running
                    : TimelinePlaybackStatus.Waiting;

            onTimelineStatusChanged(i, status);
        }
    }
}
