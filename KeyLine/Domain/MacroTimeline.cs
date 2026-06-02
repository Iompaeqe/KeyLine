using System.Collections.ObjectModel;

namespace KeyLine.Domain;

public sealed class MacroTimeline
{
    public string Name { get; set; } = "T1";

    public ObservableCollection<MacroNode> Nodes { get; } = new();

    public bool UseStandardDelay { get; set; }

    public int StandardDelayMs { get; set; } = 50;

    public bool ShowKeyUpDown { get; set; } = true;

    public bool UseTextInputMode { get; set; }

    public int LoopCount { get; set; }

    public int BaseDelayMs { get; set; } = 50;

    public bool HasNodes => Nodes.Count > 0;

    public List<MacroNode> ToPlaybackList()
    {
        return Nodes.ToList();
    }
}
