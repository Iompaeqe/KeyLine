using System.Windows;

namespace KeySpammer;

public partial class MainWindow
{
    private void CaptureDroppedGhostPositionForAnimation()
    {
        if (_draggedStepGhostTransform == null || _drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
        {
            _lastDroppedGhostRowsPanelPosition = null;
            return;
        }

        _lastDroppedGhostRowsPanelPosition = new Point(
            _draggedStepGhostTransform.X,
            MainWindow.TimelineConnectorY - (_draggedStepGhostHeight / 2.0));
    }

    private void SeedDraggedNodeAnimationFromGhost()
    {
        if (_lastDroppedGhostRowsPanelPosition == null || _drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
            return;

        var animationKey = GetTimelineAnimationKey(_drag.DraggedStepTimeline, _drag.DraggedStep);
        _timelineVisualPositions[animationKey] = _lastDroppedGhostRowsPanelPosition.Value;
    }
}
