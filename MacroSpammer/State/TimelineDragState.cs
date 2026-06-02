using System.Windows;
using MacroSpammer.Domain;

namespace MacroSpammer.State;

public sealed class TimelineDragState
{
    public MacroTimeline? DraggedNodeTimeline { get; private set; }
    public MacroNode? DraggedNode { get; private set; }
    public Point NodeDragStartPoint { get; private set; }
    public bool IsDraggingNode { get; private set; }

    public MacroTimeline? DraggedTimelineHeader { get; private set; }
    public Point HeaderDragStartPoint { get; private set; }
    public int HeaderDragStartIndex { get; private set; }
    public bool IsDraggingTimelineHeader { get; private set; }

    public bool IsTimelinePanning { get; private set; }
    public Point TimelinePanStartMouse { get; private set; }
    public double TimelinePanStartOffset { get; private set; }
    
    public Point NodeDragCurrentPoint { get; private set; }
    public MacroNode? NodeDropRawInsertAnchor { get; private set; }

    public void BeginStepDrag(MacroTimeline timeline, MacroNode node, Point startPoint)
    {
        DraggedNodeTimeline = timeline;
        DraggedNode = node;
        NodeDragStartPoint = startPoint;
        NodeDragCurrentPoint = startPoint;
        NodeDropRawInsertAnchor = null;
        IsDraggingNode = false;
    }
    
    public bool UpdateStepDragPreview(Point currentPoint, MacroNode? rawInsertAnchor)
    {
        NodeDragCurrentPoint = currentPoint;

        if (ReferenceEquals(NodeDropRawInsertAnchor, rawInsertAnchor))
            return false;

        NodeDropRawInsertAnchor = rawInsertAnchor;
        return true;
    }

    public bool ShouldStartStepDrag(Point currentPoint, double dragThreshold)
    {
        if (DraggedNode == null)
            return false;

        var dragDistance = Math.Abs(currentPoint.X - NodeDragStartPoint.X);
        return dragDistance >= dragThreshold;
    }

    public void MarkStepDragging()
    {
        IsDraggingNode = true;
    }

    public void EndStepDrag()
    {
        DraggedNodeTimeline = null;
        DraggedNode = null;
        NodeDragStartPoint = default;
        NodeDragCurrentPoint = default;
        NodeDropRawInsertAnchor = null;
        IsDraggingNode = false;
    }

    public void BeginTimelineHeaderDrag(MacroTimeline timeline, Point startPoint, int startIndex)
    {
        DraggedTimelineHeader = timeline;
        HeaderDragStartPoint = startPoint;
        HeaderDragStartIndex = Math.Max(0, startIndex);
        IsDraggingTimelineHeader = false;
    }

    public bool ShouldStartTimelineHeaderDrag(Point currentPoint, double dragThreshold)
    {
        if (DraggedTimelineHeader == null)
            return false;

        var dragDistance = Math.Abs(currentPoint.Y - HeaderDragStartPoint.Y);
        return dragDistance >= dragThreshold;
    }

    public void MarkTimelineHeaderDragging()
    {
        IsDraggingTimelineHeader = true;
    }

    public void EndTimelineHeaderDrag()
    {
        DraggedTimelineHeader = null;
        HeaderDragStartPoint = default;
        HeaderDragStartIndex = 0;
        IsDraggingTimelineHeader = false;
    }

    public void BeginTimelinePan(Point startMousePoint, double startOffset)
    {
        IsTimelinePanning = true;
        TimelinePanStartMouse = startMousePoint;
        TimelinePanStartOffset = startOffset;
    }

    public double GetTimelinePanTargetOffset(Point currentMousePoint)
    {
        var deltaX = currentMousePoint.X - TimelinePanStartMouse.X;
        return TimelinePanStartOffset - deltaX;
    }

    public void EndTimelinePan()
    {
        IsTimelinePanning = false;
        TimelinePanStartMouse = default;
        TimelinePanStartOffset = 0;
    }
}
