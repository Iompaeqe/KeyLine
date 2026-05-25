using System.Windows;
using MacroSpammer.Domain;

namespace MacroSpammer.State;

public sealed class TimelineDragState
{
    public MacroTimeline? DraggedStepTimeline { get; private set; }
    public MacroStep? DraggedStep { get; private set; }
    public Point StepDragStartPoint { get; private set; }
    public bool IsDraggingStep { get; private set; }

    public MacroTimeline? DraggedTimelineHeader { get; private set; }
    public Point HeaderDragStartPoint { get; private set; }
    public int HeaderDragStartIndex { get; private set; }
    public bool IsDraggingTimelineHeader { get; private set; }

    public bool IsTimelinePanning { get; private set; }
    public Point TimelinePanStartMouse { get; private set; }
    public double TimelinePanStartOffset { get; private set; }
    
    public Point StepDragCurrentPoint { get; private set; }
    public MacroStep? StepDropRawInsertAnchor { get; private set; }

    public void BeginStepDrag(MacroTimeline timeline, MacroStep step, Point startPoint)
    {
        DraggedStepTimeline = timeline;
        DraggedStep = step;
        StepDragStartPoint = startPoint;
        StepDragCurrentPoint = startPoint;
        StepDropRawInsertAnchor = null;
        IsDraggingStep = false;
    }
    
    public bool UpdateStepDragPreview(Point currentPoint, MacroStep? rawInsertAnchor)
    {
        StepDragCurrentPoint = currentPoint;

        if (ReferenceEquals(StepDropRawInsertAnchor, rawInsertAnchor))
            return false;

        StepDropRawInsertAnchor = rawInsertAnchor;
        return true;
    }

    public bool ShouldStartStepDrag(Point currentPoint, double dragThreshold)
    {
        if (DraggedStep == null)
            return false;

        var dragDistance = Math.Abs(currentPoint.X - StepDragStartPoint.X);
        return dragDistance >= dragThreshold;
    }

    public void MarkStepDragging()
    {
        IsDraggingStep = true;
    }

    public void EndStepDrag()
    {
        DraggedStepTimeline = null;
        DraggedStep = null;
        StepDragStartPoint = default;
        StepDragCurrentPoint = default;
        StepDropRawInsertAnchor = null;
        IsDraggingStep = false;
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
