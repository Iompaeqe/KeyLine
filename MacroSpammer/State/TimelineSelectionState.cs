using MacroSpammer.Domain;

namespace MacroSpammer.State;

public sealed class TimelineSelectionState
{
    public MacroTimeline? SelectedTimeline { get; private set; }
    public MacroStep? SelectedStep { get; private set; }

    public bool HasStepSelection => SelectedTimeline != null && SelectedStep != null;
    public bool HasTimelineSelection => SelectedTimeline != null && SelectedStep == null;

    public void SelectStep(MacroTimeline timeline, MacroStep step)
    {
        SelectedTimeline = timeline;
        SelectedStep = step;
    }

    public void SelectTimeline(MacroTimeline timeline)
    {
        SelectedTimeline = timeline;
        SelectedStep = null;
    }

    public void Clear()
    {
        SelectedTimeline = null;
        SelectedStep = null;
    }

    public bool IsTimelineSelected(MacroTimeline timeline)
    {
        return ReferenceEquals(SelectedTimeline, timeline) && SelectedStep == null;
    }

    public bool IsStepSelected(MacroTimeline timeline, MacroStep step)
    {
        if (!ReferenceEquals(SelectedTimeline, timeline) || SelectedStep == null)
            return false;

        return ReferenceEquals(SelectedStep, step);
    }
}