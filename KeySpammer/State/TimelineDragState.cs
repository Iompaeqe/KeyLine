using System.Windows;
using KeySpammer.Domain;

namespace KeySpammer.State;

public sealed class TimelineDragState
{
    public MacroStep? DraggedStep { get; private set; }
    public Point StepDragStartPoint { get; private set; }
    public bool IsDraggingStep { get; private set; }

    public bool IsTimelinePanning { get; private set; }
    public Point TimelinePanStartMouse { get; private set; }
    public double TimelinePanStartOffset { get; private set; }

    public void BeginStepDrag(MacroStep step, Point startPoint)
    {
        DraggedStep = step;
        StepDragStartPoint = startPoint;
        IsDraggingStep = false;
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
        DraggedStep = null;
        StepDragStartPoint = default;
        IsDraggingStep = false;
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