namespace KeyLine.Domain;

public enum MacroLoopMode
{
    Async,
    Sync,
    Cycle,
    Chain,
    Sequence
}

// Sub-mode for LoopMode.Sequence. Sequence runs exactly one eligible normal timeline per activation;
// this picks which one. The legacy top-level "Random" loop mode migrates to Sequence + Random.
public enum SequenceMode
{
    Ordered,
    Priority,
    Random
}

public enum ShortcutTriggerBehavior
{
    PassThrough,
    RemapConsume
}

public sealed class MacroWorkspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ProfileId { get; set; } = MacroProfile.NoProfileId;

    public string Name { get; set; } = "Macro 1";

    public MacroDocument Document { get; set; } = new();

    public int LoopCount { get; set; }

    public int TimerMs { get; set; }

    public int BaseDelayMs { get; set; } = 50;

    public MacroLoopMode LoopMode { get; set; } = MacroLoopMode.Async;

    // Only meaningful when LoopMode == Sequence. Defaults to Ordered (the original Sequence behavior).
    public SequenceMode SequenceMode { get; set; } = SequenceMode.Ordered;

    public string ShortcutKeys { get; set; } = "";

    public bool ShortcutsEnabled { get; set; }

    public ShortcutTriggerBehavior ShortcutTriggerBehavior { get; set; } = ShortcutTriggerBehavior.PassThrough;

    public string TargetWindowSearchName { get; set; } = "";

    public long TargetWindowHandle { get; set; }

    public string TargetWindowTitle { get; set; } = "";

    public long TargetChildWindowHandle { get; set; }

    public string TargetChildWindowTitle { get; set; } = "";

    public string ErrorMessage { get; set; } = "";

    // --- Macro Hooks (v1.9) ---
    // Start/End hook timelines wrap macro execution. They are stored separately from
    // Document.Timelines so "normal timelines" stay untouched; hooks never participate in
    // loop-mode/sequence selection, reorder, or sharing-export filtering.
    public MacroTimeline StartHookTimeline { get; set; } = new() { Name = MacroTimeline.StartHookName };

    public MacroTimeline EndHookTimeline { get; set; } = new() { Name = MacroTimeline.EndHookName };

    public bool StartHookEnabled { get; set; }

    public bool EndHookEnabled { get; set; }

    // Reset shortcut for Sequence/Random loop modes (resets the session pointer + active cooldowns).
    public string ResetShortcutKeys { get; set; } = "";

    public bool IsHookTimeline(MacroTimeline timeline) =>
        ReferenceEquals(timeline, StartHookTimeline) || ReferenceEquals(timeline, EndHookTimeline);

    /// <summary>
    /// The timelines shown in the timeline strip: enabled Start hook first, then the normal
    /// timelines, then the enabled End hook. Used for rendering only.
    /// </summary>
    public IEnumerable<MacroTimeline> EnumerateDisplayTimelines()
    {
        if (StartHookEnabled)
            yield return StartHookTimeline;

        foreach (var timeline in Document.Timelines)
            yield return timeline;

        if (EndHookEnabled)
            yield return EndHookTimeline;
    }
}

