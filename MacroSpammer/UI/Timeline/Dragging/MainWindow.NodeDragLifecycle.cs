using System.Windows.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private void CompleteStepDrop()
    {
        if (_drag.IsDraggingNode && _drag.DraggedNodeTimeline != null && _drag.DraggedNode != null)
        {
            SaveUndoSnapshot();
            CaptureDroppedGhostPositionForAnimation();

            MoveStepBeforeRawAnchor(
                _drag.DraggedNodeTimeline,
                _drag.DraggedNode,
                _drag.NodeDropRawInsertAnchor);

            SeedDraggedNodeAnimationFromGhost();
            MergeAdjacentDelayNodesIfEnabled(_drag.DraggedNodeTimeline);
        }

        CancelTimelineDragState();
        RefreshTimeline();
        ScheduleSaveState();

        _lastDroppedGhostRowsPanelPosition = null;
    }

    private void CancelTimelineDragState()
    {
        EndDraggedStepGhost();

        _nodeDragPreview.Clear();
        ClearPendingClickSelection();

        if (_drag.DraggedNodeTimeline != null)
            _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(_drag.DraggedNodeTimeline));

        _drag.EndStepDrag();
        _drag.EndTimelineHeaderDrag();
        _drag.EndTimelinePan();

        if (Mouse.Captured != null)
            Mouse.Capture(null);
    }
}