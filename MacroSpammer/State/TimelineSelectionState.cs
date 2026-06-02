using MacroSpammer.Domain;

namespace MacroSpammer.State;

public sealed class TimelineSelectionState
{
    public MacroTimeline? SelectedTimeline { get; private set; }
    public MacroNode? SelectedNode { get; private set; }
    public MacroNode? AnchorNode { get; private set; }
    public List<MacroNode> SelectedNodes { get; } = new();

    public bool HasNodeSelection => SelectedTimeline != null && SelectedNode != null;
    public bool HasMultipleNodeSelection => SelectedTimeline != null && SelectedNodes.Count > 1;
    public bool HasTimelineSelection => SelectedTimeline != null && SelectedNode == null;

    public void SelectNode(MacroTimeline timeline, MacroNode node)
    {
        SelectedTimeline = timeline;
        SelectedNode = node;
        AnchorNode = node;
        SelectedNodes.Clear();
        SelectedNodes.Add(node);
    }

    public void SelectNodes(MacroTimeline timeline, IEnumerable<MacroNode> nodes, MacroNode? anchorNode = null)
    {
        SelectedTimeline = timeline;
        SelectedNodes.Clear();
        SelectedNodes.AddRange(nodes);
        SelectedNode = SelectedNodes.FirstOrDefault();
        AnchorNode = anchorNode ?? SelectedNode;

        if (SelectedNode == null)
        {
            SelectedTimeline = null;
            AnchorNode = null;
        }
    }

    public void SelectTimeline(MacroTimeline timeline)
    {
        SelectedTimeline = timeline;
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
    }

    public void Clear()
    {
        SelectedTimeline = null;
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
    }

    public bool IsTimelineSelected(MacroTimeline timeline)
    {
        return ReferenceEquals(SelectedTimeline, timeline) && SelectedNode == null;
    }

    public bool IsNodeSelected(MacroTimeline timeline, MacroNode node)
    {
        if (!ReferenceEquals(SelectedTimeline, timeline) || SelectedNodes.Count == 0)
            return false;

        return SelectedNodes.Any(selected => ReferenceEquals(selected, node));
    }
}
