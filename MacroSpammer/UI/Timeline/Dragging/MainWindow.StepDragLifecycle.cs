using System.Windows.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private void CompleteStepDrop()
    {
        if (_drag.IsDraggingStep && _drag.DraggedStepTimeline != null && _drag.DraggedStep != null)
        {
            SaveUndoSnapshot();
            CaptureDroppedGhostPositionForAnimation();

            MoveStepBeforeRawAnchor(
                _drag.DraggedStepTimeline,
                _drag.DraggedStep,
                _drag.StepDropRawInsertAnchor);

            SeedDraggedNodeAnimationFromGhost();
        }

        CancelTimelineDragState();
        RefreshTimeline();
        ScheduleSaveState();

        _lastDroppedGhostRowsPanelPosition = null;
    }

    private void CancelTimelineDragState()
    {
        EndDraggedStepGhost();

        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _stepDragOriginalRawSteps.Clear();
        _stepDragPreviewWidthByFirstRawItem.Clear();
        _stepDragSlotWidth = 0;
        _lastStepDragPreviewMousePoint = null;
        ClearPendingClickSelection();

        if (_drag.DraggedStepTimeline != null)
            _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(_drag.DraggedStepTimeline));

        _drag.EndStepDrag();
        _drag.EndTimelineHeaderDrag();
        _drag.EndTimelinePan();

        if (Mouse.Captured != null)
            Mouse.Capture(null);
    }
}
