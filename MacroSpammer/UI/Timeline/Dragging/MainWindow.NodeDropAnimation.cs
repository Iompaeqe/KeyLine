using System.Windows;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void CaptureDroppedGhostPositionForAnimation()
    {
        if (_draggedStepGhostTransform == null || _drag.DraggedNode == null || _drag.DraggedNodeTimeline == null)
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
        if (_lastDroppedGhostRowsPanelPosition == null || _drag.DraggedNode == null || _drag.DraggedNodeTimeline == null)
            return;

        var animationKey = GetTimelineAnimationKey(_drag.DraggedNodeTimeline, _drag.DraggedNode);
        _timelineVisualPositions[animationKey] = _lastDroppedGhostRowsPanelPosition.Value;
    }
}
