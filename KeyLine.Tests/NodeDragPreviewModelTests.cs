using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.UI.Timeline;
using System.Windows;

namespace KeyLine.Tests;

public sealed class NodeDragPreviewModelTests
{
    [Fact]
    public void UpdateFromMouse_UsesLeadingVisualWidthForNestedBlockEndCenters()
    {
        var timeline = new MacroTimeline();
        var (outerStart, outerEnd) = TimelineBlockService.CreateRepeatBlock();
        var (innerStart, innerEnd) = TimelineBlockService.CreateConditionBlock();
        var dragged = new MacroNode { Type = MacroNodeType.Text, Text = "dragged" };
        var model = new NodeDragPreviewModel();

        timeline.Nodes.Add(outerStart);
        timeline.Nodes.Add(innerStart);
        timeline.Nodes.Add(dragged);
        timeline.Nodes.Add(innerEnd);
        timeline.Nodes.Add(outerEnd);

        model.Begin(
            timeline,
            timeline.Nodes,
            new[] { dragged },
            dragged,
            _ => 10,
            node => node == innerEnd || node == outerEnd ? 30 : 0,
            firstItemLeft: 0,
            itemGap: 0);

        Assert.Equal(60, model.Slots.Single(slot => slot.DisplayNode == innerEnd).Left);

        var changedBeforeRenderedCenter = model.UpdateFromMouse(
            new Point(50, 0),
            moveEpsilon: 0,
            firstItemLeft: 0,
            itemGap: 0);

        Assert.False(changedBeforeRenderedCenter);
        Assert.Equal(
            new[] { outerStart, innerStart, dragged, innerEnd, outerEnd },
            model.PreviewRawSteps);

        var changedAfterRenderedCenter = model.UpdateFromMouse(
            new Point(66, 0),
            moveEpsilon: 0,
            firstItemLeft: 0,
            itemGap: 0);

        Assert.True(changedAfterRenderedCenter);
        Assert.Equal(
            new[] { outerStart, innerStart, innerEnd, dragged, outerEnd },
            model.PreviewRawSteps);
    }
}
