using MacroSpammer.Domain;

namespace MacroSpammer.State;

public sealed class TimelineSelectionState
{
    public MacroTimeline? SelectedTimeline { get; private set; }
    public MacroNode? SelectedStep { get; private set; }
    public MacroNode? AnchorStep { get; private set; }
    public List<MacroNode> SelectedSteps { get; } = new();

    public bool HasStepSelection => SelectedTimeline != null && SelectedStep != null;
    public bool HasMultipleStepSelection => SelectedTimeline != null && SelectedSteps.Count > 1;
    public bool HasTimelineSelection => SelectedTimeline != null && SelectedStep == null;

    public void SelectStep(MacroTimeline timeline, MacroNode node)
    {
        SelectedTimeline = timeline;
        SelectedStep = node;
        AnchorStep = node;
        SelectedSteps.Clear();
        SelectedSteps.Add(node);
    }

    public void SelectSteps(MacroTimeline timeline, IEnumerable<MacroNode> steps, MacroNode? anchorStep = null)
    {
        SelectedTimeline = timeline;
        SelectedSteps.Clear();
        SelectedSteps.AddRange(steps);
        SelectedStep = SelectedSteps.FirstOrDefault();
        AnchorStep = anchorStep ?? SelectedStep;

        if (SelectedStep == null)
        {
            SelectedTimeline = null;
            AnchorStep = null;
        }
    }

    public void SelectTimeline(MacroTimeline timeline)
    {
        SelectedTimeline = timeline;
        SelectedStep = null;
        AnchorStep = null;
        SelectedSteps.Clear();
    }

    public void Clear()
    {
        SelectedTimeline = null;
        SelectedStep = null;
        AnchorStep = null;
        SelectedSteps.Clear();
    }

    public bool IsTimelineSelected(MacroTimeline timeline)
    {
        return ReferenceEquals(SelectedTimeline, timeline) && SelectedStep == null;
    }

    public bool IsStepSelected(MacroTimeline timeline, MacroNode node)
    {
        if (!ReferenceEquals(SelectedTimeline, timeline) || SelectedSteps.Count == 0)
            return false;

        return SelectedSteps.Any(selected => ReferenceEquals(selected, node));
    }
}
