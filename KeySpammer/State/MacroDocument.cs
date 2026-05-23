using System.Collections.ObjectModel;
using KeySpammer.Domain;

namespace KeySpammer.State;

public sealed class MacroDocument
{
    public ObservableCollection<MacroStep> Steps { get; } = new();

    public bool HasSteps => Steps.Count > 0;

    public void Add(MacroStep step)
    {
        Steps.Add(step);
    }

    public void AddRange(IEnumerable<MacroStep> steps)
    {
        foreach (var step in steps)
            Steps.Add(step);
    }

    public void Remove(MacroStep step)
    {
        Steps.Remove(step);
    }

    public void Clear()
    {
        Steps.Clear();
    }

    public List<MacroStep> ToPlaybackList()
    {
        return Steps.ToList();
    }

    public int IndexOf(MacroStep step)
    {
        return Steps.IndexOf(step);
    }

    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Steps.Count)
            return;

        if (newIndex < 0 || newIndex >= Steps.Count)
            return;

        if (oldIndex == newIndex)
            return;

        Steps.Move(oldIndex, newIndex);
    }
}