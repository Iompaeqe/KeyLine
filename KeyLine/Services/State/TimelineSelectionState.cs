using KeyLine.Domain;

namespace KeyLine.State;

public sealed class TimelineSelectionState
{
    private readonly HashSet<MacroNode> _selectedNodeSet = new();
    private readonly HashSet<MacroTimeline> _selectedTimelineSet = new();

    // SelectedTimeline doubles as: the timeline that owns the selected nodes (node mode), or the
    // anchor/primary timeline when one or more timelines are selected (timeline mode).
    public MacroTimeline? SelectedTimeline { get; private set; }
    public MacroNode? SelectedNode { get; private set; }
    public MacroNode? AnchorNode { get; private set; }
    public List<MacroNode> SelectedNodes { get; } = new();

    // Timeline (vs node) selection. Only meaningful when SelectedNode == null. Node and timeline
    // selection are mutually exclusive — selecting nodes clears timelines and vice versa.
    public List<MacroTimeline> SelectedTimelines { get; } = new();
    public MacroTimeline? AnchorTimeline { get; private set; }

    public bool HasNodeSelection => SelectedTimeline != null && SelectedNode != null;
    public bool HasMultipleNodeSelection => SelectedTimeline != null && SelectedNodes.Count > 1;
    public bool HasTimelineSelection => SelectedNode == null && _selectedTimelineSet.Count > 0;
    public bool HasMultipleTimelineSelection => SelectedNode == null && _selectedTimelineSet.Count > 1;

    public void SelectNode(MacroTimeline timeline, MacroNode node)
    {
        SelectedTimeline = timeline;
        SelectedNode = node;
        AnchorNode = node;
        SelectedNodes.Clear();
        SelectedNodes.Add(node);
        _selectedNodeSet.Clear();
        _selectedNodeSet.Add(node);
        ClearTimelineSelection();
    }

    public void SelectNodes(MacroTimeline timeline, IEnumerable<MacroNode> nodes, MacroNode? anchorNode = null)
    {
        SelectedTimeline = timeline;
        SelectedNodes.Clear();
        SelectedNodes.AddRange(nodes);
        _selectedNodeSet.Clear();
        foreach (var selectedNode in SelectedNodes)
            _selectedNodeSet.Add(selectedNode);
        SelectedNode = SelectedNodes.FirstOrDefault();
        AnchorNode = anchorNode ?? SelectedNode;
        ClearTimelineSelection();

        if (SelectedNode == null)
        {
            SelectedTimeline = null;
            AnchorNode = null;
            _selectedNodeSet.Clear();
        }
    }

    public void SelectTimeline(MacroTimeline timeline)
    {
        SelectedTimeline = timeline;
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
        _selectedNodeSet.Clear();

        SelectedTimelines.Clear();
        _selectedTimelineSet.Clear();
        SelectedTimelines.Add(timeline);
        _selectedTimelineSet.Add(timeline);
        AnchorTimeline = timeline;
    }

    /// <summary>Selects a set of timelines (used by range/Shift selection). Clears node selection.</summary>
    public void SelectTimelines(IEnumerable<MacroTimeline> timelines, MacroTimeline? anchor = null)
    {
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
        _selectedNodeSet.Clear();

        SelectedTimelines.Clear();
        _selectedTimelineSet.Clear();
        foreach (var timeline in timelines)
        {
            if (_selectedTimelineSet.Add(timeline))
                SelectedTimelines.Add(timeline);
        }

        SelectedTimeline = anchor != null && _selectedTimelineSet.Contains(anchor)
            ? anchor
            : SelectedTimelines.LastOrDefault();
        AnchorTimeline = anchor ?? SelectedTimeline;

        if (SelectedTimeline == null)
            Clear();
    }

    /// <summary>Toggles one timeline in/out of the selection (used by Ctrl selection).</summary>
    public void ToggleTimelineSelection(MacroTimeline timeline)
    {
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
        _selectedNodeSet.Clear();

        if (_selectedTimelineSet.Remove(timeline))
        {
            SelectedTimelines.Remove(timeline);
            SelectedTimeline = SelectedTimelines.LastOrDefault();
            AnchorTimeline = SelectedTimeline;
        }
        else
        {
            _selectedTimelineSet.Add(timeline);
            SelectedTimelines.Add(timeline);
            SelectedTimeline = timeline;
            AnchorTimeline = timeline;
        }
    }

    public void Clear()
    {
        SelectedTimeline = null;
        SelectedNode = null;
        AnchorNode = null;
        SelectedNodes.Clear();
        _selectedNodeSet.Clear();
        ClearTimelineSelection();
    }

    public bool IsTimelineSelected(MacroTimeline timeline)
    {
        return SelectedNode == null && _selectedTimelineSet.Contains(timeline);
    }

    public bool IsNodeSelected(MacroTimeline timeline, MacroNode node)
    {
        if (!ReferenceEquals(SelectedTimeline, timeline) || SelectedNodes.Count == 0)
            return false;

        return _selectedNodeSet.Contains(node) ||
               SelectedNodes.Any(selectedNode => IsSameSelectionNode(node, selectedNode));
    }

    private void ClearTimelineSelection()
    {
        SelectedTimelines.Clear();
        _selectedTimelineSet.Clear();
        AnchorTimeline = null;
    }

    private static bool IsSameSelectionNode(MacroNode node, MacroNode selectedNode)
    {
        if (ReferenceEquals(node, selectedNode))
            return true;

        if (node.IsSyntheticDisplayNode)
            return node.SourceNodes.Contains(selectedNode) ||
                   (selectedNode.IsSyntheticDisplayNode && node.SourceNodes.SequenceEqual(selectedNode.SourceNodes));

        return selectedNode.IsSyntheticDisplayNode && selectedNode.SourceNodes.Contains(node);
    }
}
