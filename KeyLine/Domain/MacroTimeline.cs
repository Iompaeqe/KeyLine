using System.Collections.ObjectModel;

namespace KeyLine.Domain;

public sealed class MacroTimeline
{
    public const string StartHookName = "Start";
    public const string EndHookName = "End";

    public string Name { get; set; } = "T1";

    public ObservableCollection<MacroNode> Nodes { get; } = new();

    public bool UseStandardDelay { get; set; }

    public int StandardDelayMs { get; set; } = 50;

    public bool ShowKeyUpDown { get; set; } = true;

    public bool UseTextInputMode { get; set; }

    public int LoopCount { get; set; }

    public int BaseDelayMs { get; set; } = 50;

    // Configured cooldown (ms) for Sequence/Random loop modes. Saved with the timeline.
    // Active (runtime) cooldown timers are tracked separately and are not persisted.
    public int CooldownMs { get; set; }

    // Collapsed timelines render as a short summary row (name + status) with nodes hidden,
    // so many timelines fit on screen. Persisted so the layout survives restarts.
    public bool IsCollapsed { get; set; }

    public bool HasNodes => Nodes.Count > 0;

    public List<MacroNode> ToPlaybackList()
    {
        return Nodes.ToList();
    }
}
