using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;

namespace KeyLine.UI.Timeline;

public sealed partial class TimelineRenderer
{
    // Seeds the position cache so the dropped node(s) animate from where the drag ghost was
    // released to their final laid-out position on the next full/row refresh. The dropped
    // position is captured by the host (it owns the ghost); the cache is owned here.
    public void SeedDroppedNodeAnimation(
        MacroTimeline timeline,
        IReadOnlyCollection<MacroNode> draggedRawItems,
        Point droppedPosition)
    {
        if (draggedRawItems.Count == 0)
            return;

        var draggedRawItemSet = draggedRawItems.ToHashSet();
        var currentLeft = droppedPosition.X;

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        foreach (var visibleStep in visibleSteps)
        {
            var rawItems = TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, visibleStep);
            if (!rawItems.Any(draggedRawItemSet.Contains))
                continue;

            var animationKey = GetTimelineAnimationKey(timeline, visibleStep);
            var top = TimelineLayoutCalculator.GetItemTop(
                TimelineConnectorY,
                MeasureTimelineItem(CreateNode(timeline, visibleStep)).Height);

            _timelineVisualPositions[animationKey] = new Point(currentLeft, top);
            currentLeft += GetCachedNodePreviewWidth(timeline, visibleStep) + TimelineItemGap;
        }
    }
}
