using KeyLine.Domain;

namespace KeyLine.Services.Playback;

/// <summary>
/// Session-only runtime state for the Sequence loop mode: a per-workspace "next timeline"
/// pointer and a map of active cooldown end-times keyed by timeline. None of this is persisted;
/// it resets when KeyLine restarts. The configured cooldown value lives on the timeline itself
/// (<see cref="MacroTimeline.CooldownMs"/>) and is never cleared by this controller.
/// </summary>
public sealed class SequenceController
{
    private sealed class WorkspaceState
    {
        public int NextIndex;
        public readonly Dictionary<MacroTimeline, DateTime> CooldownUntilUtc = new();
        public MacroTimeline? LastPlayed;
    }

    private readonly Dictionary<MacroWorkspace, WorkspaceState> _states = new();
    private readonly Random _random;

    public SequenceController(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    private WorkspaceState GetState(MacroWorkspace workspace)
    {
        if (!_states.TryGetValue(workspace, out var state))
        {
            state = new WorkspaceState();
            _states[workspace] = state;
        }

        return state;
    }

    public bool IsOnCooldown(MacroWorkspace workspace, MacroTimeline timeline, DateTime nowUtc) =>
        GetState(workspace).CooldownUntilUtc.TryGetValue(timeline, out var until) && until > nowUtc;

    public TimeSpan GetRemainingCooldown(MacroWorkspace workspace, MacroTimeline timeline, DateTime nowUtc)
    {
        if (GetState(workspace).CooldownUntilUtc.TryGetValue(timeline, out var until) && until > nowUtc)
            return until - nowUtc;

        return TimeSpan.Zero;
    }

    public MacroTimeline? GetLastPlayed(MacroWorkspace workspace) => GetState(workspace).LastPlayed;

    /// <summary>
    /// Index of the normal timeline the Sequence pointer currently points at (the one that will be
    /// checked first on the next trigger). Used for header "Next" display.
    /// </summary>
    public int GetNextIndex(MacroWorkspace workspace, int normalCount)
    {
        if (normalCount <= 0)
            return 0;

        var index = GetState(workspace).NextIndex;
        return ((index % normalCount) + normalCount) % normalCount;
    }

    /// <summary>
    /// Picks the timeline to play for this trigger, or null when nothing is ready (every timeline is
    /// empty, disabled, or on cooldown, or there are no normal timelines). This is the single place
    /// the three Sequence sub-modes are resolved:
    /// <list type="bullet">
    /// <item>Ordered scans from the rotating pointer with wrap-around.</item>
    /// <item>Priority always scans from index 0 (earlier timelines win); no pointer.</item>
    /// <item>Random picks uniformly from the ready set; no pointer.</item>
    /// </list>
    /// </summary>
    public MacroTimeline? SelectNext(
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> normalTimelines,
        SequenceMode mode,
        DateTime nowUtc)
    {
        if (normalTimelines.Count == 0)
            return null;

        switch (mode)
        {
            case SequenceMode.Random:
            {
                var ready = new List<MacroTimeline>();
                foreach (var timeline in normalTimelines)
                {
                    if (IsReady(workspace, timeline, nowUtc))
                        ready.Add(timeline);
                }

                return ready.Count == 0 ? null : ready[_random.Next(ready.Count)];
            }

            case SequenceMode.Priority:
            {
                foreach (var timeline in normalTimelines)
                {
                    if (IsReady(workspace, timeline, nowUtc))
                        return timeline;
                }

                return null;
            }

            default: // Ordered
            {
                var start = GetNextIndex(workspace, normalTimelines.Count);
                for (var offset = 0; offset < normalTimelines.Count; offset++)
                {
                    var timeline = normalTimelines[(start + offset) % normalTimelines.Count];
                    if (IsReady(workspace, timeline, nowUtc))
                        return timeline;
                }

                return null;
            }
        }
    }

    private bool IsReady(MacroWorkspace workspace, MacroTimeline timeline, DateTime nowUtc) =>
        timeline.HasNodes && !timeline.IsDisabled && !IsOnCooldown(workspace, timeline, nowUtc);

    /// <summary>
    /// Records that <paramref name="played"/> was just played: puts it on active cooldown using its
    /// configured value. Only Ordered advances the rotating pointer to the timeline after it; Priority
    /// and Random never touch the pointer, so an Ordered run can resume cleanly after a mode switch.
    /// </summary>
    public void MarkPlayed(
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> normalTimelines,
        MacroTimeline played,
        DateTime nowUtc,
        SequenceMode mode = SequenceMode.Ordered)
    {
        var state = GetState(workspace);

        var cooldownMs = Math.Max(0, played.CooldownMs);
        if (cooldownMs > 0)
            state.CooldownUntilUtc[played] = nowUtc.AddMilliseconds(cooldownMs);
        else
            state.CooldownUntilUtc.Remove(played);

        state.LastPlayed = played;

        if (mode != SequenceMode.Ordered)
            return;

        var playedIndex = IndexOf(normalTimelines, played);
        if (playedIndex >= 0 && normalTimelines.Count > 0)
            state.NextIndex = (playedIndex + 1) % normalTimelines.Count;
    }

    private static int IndexOf(IReadOnlyList<MacroTimeline> timelines, MacroTimeline target)
    {
        for (var i = 0; i < timelines.Count; i++)
        {
            if (ReferenceEquals(timelines[i], target))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Resets the pointer to the first timeline and clears active cooldown timers and last-played
    /// state. Configured timeline cooldown values are not touched.
    /// </summary>
    public void Reset(MacroWorkspace workspace)
    {
        var state = GetState(workspace);
        state.NextIndex = 0;
        state.CooldownUntilUtc.Clear();
        state.LastPlayed = null;
    }
}
