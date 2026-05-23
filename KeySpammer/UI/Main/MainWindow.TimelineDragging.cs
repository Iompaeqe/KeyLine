using System.Windows;
using System.Windows.Input;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private const double StepDragThreshold = 6;

    private void AttachStepMouseHandlers(FrameworkElement element, MacroStep step)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _selection.Select(step);
            _drag.BeginStepDrag(step, e.GetPosition(TimelinePanel));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (_drag.DraggedStep == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPoint = e.GetPosition(TimelinePanel);

            if (!_drag.IsDraggingStep)
            {
                if (!_drag.ShouldStartStepDrag(currentPoint, StepDragThreshold))
                    return;

                _drag.MarkStepDragging();
            }

            MoveStepByMouseX(_drag.DraggedStep, currentPoint.X);

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            EndStepDrag(element);
            RefreshTimeline();

            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            // During live drag we rebuild the timeline, so the old element loses capture.
            // Do not kill drag state while the left mouse button is still held.
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                _drag.EndStepDrag();
        };
    }

    private void EndStepDrag(FrameworkElement element)
    {
        _drag.EndStepDrag();

        if (element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }

    private void MoveStepByMouseX(MacroStep draggedStep, double mouseX)
    {
        var draggedItems = GetRawStepsForDisplayStep(draggedStep);
        if (draggedItems.Count == 0)
            return;

        MacroStep? insertBeforeDisplayStep = null;

        foreach (var child in TimelinePanel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not MacroStep childStep)
                continue;

            var childItems = GetRawStepsForDisplayStep(childStep);
            if (childItems.Count == 0)
                continue;

            if (draggedItems.Any(childItems.Contains))
                continue;

            var childPos = child.TransformToAncestor(TimelinePanel).Transform(new Point(0, 0));
            var midpoint = childPos.X + child.ActualWidth * 0.5;

            if (mouseX < midpoint)
            {
                insertBeforeDisplayStep = childStep;
                break;
            }
        }

        MoveStepBeforeDisplayStep(draggedStep, insertBeforeDisplayStep);
    }

    private void MoveStepBeforeDisplayStep(MacroStep draggedStep, MacroStep? insertBeforeDisplayStep)
    {
        var draggedItems = GetRawStepsForDisplayStep(draggedStep);
        if (draggedItems.Count == 0)
            return;

        var oldFirstIndex = _document.Steps.IndexOf(draggedItems[0]);
        var rawInsertAnchor = GetRawInsertAnchor(insertBeforeDisplayStep, draggedItems);

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? _document.Steps.Count
            : _document.Steps.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = _document.Steps.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return;

        RemoveDraggedItems(draggedItems);

        var insertIndex = GetInsertIndex(rawInsertAnchor);
        InsertDraggedItems(insertIndex, draggedItems);
        
        RefreshTimeline();
        _selection.Select(draggedStep);
    }
    
    private void RecaptureDraggedTimelineElement()
    {
        if (_drag.DraggedStep == null)
            return;

        var rebuiltElement = FindTimelineElementForStep(_drag.DraggedStep);

        if (rebuiltElement == null)
            return;

        if (!rebuiltElement.IsMouseCaptured)
            rebuiltElement.CaptureMouse();
    }
    
    private FrameworkElement? FindTimelineElementForStep(MacroStep step)
    {
        foreach (var child in TimelinePanel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not MacroStep childStep)
                continue;

            if (ReferenceEquals(childStep, step))
                return child;

            if (childStep.IsSyntheticDisplayStep && childStep.SourceSteps.Contains(step))
                return child;

            if (step.IsSyntheticDisplayStep &&
                childStep.IsSyntheticDisplayStep &&
                childStep.SourceSteps.SequenceEqual(step.SourceSteps))
            {
                return child;
            }
        }

        return null;
    }

    private MacroStep? GetRawInsertAnchor(MacroStep? insertBeforeDisplayStep, List<MacroStep> draggedItems)
    {
        if (insertBeforeDisplayStep == null)
            return null;

        var targetItems = GetRawStepsForDisplayStep(insertBeforeDisplayStep);

        if (targetItems.Count == 0)
            return null;

        return draggedItems.Any(targetItems.Contains)
            ? null
            : targetItems[0];
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private void RemoveDraggedItems(IEnumerable<MacroStep> draggedItems)
    {
        foreach (var item in draggedItems)
            _document.Steps.Remove(item);
    }

    private int GetInsertIndex(MacroStep? rawInsertAnchor)
    {
        if (rawInsertAnchor == null)
            return _document.Steps.Count;

        var insertIndex = _document.Steps.IndexOf(rawInsertAnchor);
        return insertIndex < 0 ? _document.Steps.Count : insertIndex;
    }

    private void InsertDraggedItems(int insertIndex, IReadOnlyList<MacroStep> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            _document.Steps.Insert(insertIndex + i, draggedItems[i]);
    }

    private List<MacroStep> GetRawStepsForDisplayStep(MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(sourceStep => _document.Steps.Contains(sourceStep))
                .ToList();
        }

        return _document.Steps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }
}