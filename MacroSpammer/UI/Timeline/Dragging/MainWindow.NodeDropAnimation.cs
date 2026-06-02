using System.Windows;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void CaptureDroppedGhostPositionForAnimation()
    {
        if (_drag.DraggedNode == null || _drag.DraggedNodeTimeline == null)
        {
            NodeDragGhost.ClearDropPosition();
            return;
        }

        NodeDragGhost.CaptureDropPosition(
            TimelineRowsPanel,
            TimelineLayoutCalculator.GetItemTop(MainWindow.TimelineConnectorY, NodeDragGhost.Height));
    }

    private void SeedDraggedNodeAnimationFromGhost()
    {
        if (NodeDragGhost.LastDroppedRowsPanelPosition == null ||
            _drag.DraggedNode == null ||
            _drag.DraggedNodeTimeline == null)
        {
            return;
        }

        var animationKey = GetTimelineAnimationKey(_drag.DraggedNodeTimeline, _drag.DraggedNode);
        _timelineVisualPositions[animationKey] = NodeDragGhost.LastDroppedRowsPanelPosition.Value;
    }
}