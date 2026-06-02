using System;
using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void BeginStepDragPreviewModel(MacroTimeline timeline, MacroNode draggedNode)
    {
        _nodeDragPreview.Clear();
        _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(timeline));

        var draggedItems = GetRawStepsForDrag(timeline, draggedNode);
        if (draggedItems.Count == 0)
            return;

        _nodeDragPreview.Begin(
            timeline,
            timeline.Nodes,
            draggedItems,
            _drag.DraggedNode,
            measuredNode => Math.Max(1, MeasureTimelineItem(CreateNode(timeline, measuredNode)).Width),
            TimelineFirstItemLeft,
            TimelineItemGap);

        _drag.UpdateStepDragPreview(_drag.NodeDragCurrentPoint, GetStepDragPreviewRawInsertAnchor());
    }

    private bool UpdateStepDragPreviewFromMouse(Point currentPoint)
    {
        var orderChanged = _nodeDragPreview.UpdateFromMouse(
            currentPoint,
            DragUi.PreviewMouseMoveEpsilon,
            TimelineFirstItemLeft,
            TimelineItemGap);

        var rawInsertAnchor = GetStepDragPreviewRawInsertAnchor();
        var anchorChanged = _drag.UpdateStepDragPreview(currentPoint, rawInsertAnchor);

        return orderChanged || anchorChanged;
    }

    private MacroNode? GetStepDragPreviewRawInsertAnchor()
    {
        return _nodeDragPreview.GetRawInsertAnchor();
    }

    private IReadOnlyList<MacroNode> GetTimelineRenderRawSteps(MacroTimeline timeline)
    {
        if (_drag.IsDraggingNode &&
            ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
            _nodeDragPreview.HasPreviewRawSteps)
        {
            return _nodeDragPreview.PreviewRawSteps;
        }

        return timeline.Nodes;
    }

    private IReadOnlyList<NodePreviewSlot> GetTimelineRenderPreviewSlots(MacroTimeline timeline)
    {
        if (_drag.IsDraggingNode &&
            ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
            _nodeDragPreview.HasSlots)
        {
            return _nodeDragPreview.Slots;
        }

        return Array.Empty<NodePreviewSlot>();
    }

    private double GetTimelineRowTopY(MacroTimeline timeline)
    {
        var index = _document.Timelines.IndexOf(timeline);
        if (index < 0)
            return 0;

        return index * (TimelineRowHeight + TimelineRowGap);
    }
}
