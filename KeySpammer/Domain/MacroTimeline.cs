using System.Collections.ObjectModel;
using KeySpammer.Domain;

namespace KeySpammer.Domain;

public sealed class MacroTimeline
{
    public string Name { get; set; } = "T1";

    public ObservableCollection<MacroStep> Steps { get; } = new();

    public bool UseStandardDelay { get; set; }

    public int StandardDelayMs { get; set; } = 50;

    public bool ShowKeyUpDown { get; set; } = true;

    public bool HasSteps => Steps.Count > 0;

    public List<MacroStep> ToPlaybackList()
    {
        return Steps.ToList();
    }
}