using System.Windows;
using KeySpammer.UI.Timeline;

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

        var ghostPositionInRowsPanel = TimelineDragOverlayCanvas.TranslatePoint(
            new Point(_draggedStepGhostTransform.X, _draggedStepGhostTransform.Y),
            TimelineRowsPanel);

        _lastDroppedGhostRowsPanelPosition = new Point(
            ghostPositionInRowsPanel.X,
            TimelineLayoutCalculator.GetItemTop(MainWindow.TimelineConnectorY, _draggedStepGhostHeight));
    }

    private void SeedDraggedNodeAnimationFromGhost()
    {
        if (_lastDroppedGhostRowsPanelPosition == null || _drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
            return;

        var animationKey = GetTimelineAnimationKey(_drag.DraggedStepTimeline, _drag.DraggedStep);
        _timelineVisualPositions[animationKey] = _lastDroppedGhostRowsPanelPosition.Value;
    }
}
