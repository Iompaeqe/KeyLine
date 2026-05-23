using KeySpammer.Domain;

namespace KeySpammer.State;

public sealed class TimelineSelectionState
{
    public MacroStep? SelectedStep { get; private set; }

    public bool HasSelection => SelectedStep != null;

    public void Select(MacroStep? step)
    {
        SelectedStep = step;
    }

    public void Clear()
    {
        SelectedStep = null;
    }

    public bool IsSelected(MacroStep step)
    {
        return ReferenceEquals(SelectedStep, step);
    }
}