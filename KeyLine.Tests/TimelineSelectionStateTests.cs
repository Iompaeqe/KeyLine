using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.State;

namespace KeyLine.Tests;

public sealed class TimelineSelectionStateTests
{
    [Fact]
    public void IsNodeSelected_MatchesEquivalentSyntheticDisplayNodes()
    {
        var timeline = new MacroTimeline
        {
            UseStandardDelay = true,
            ShowKeyUpDown = false
        };
        var keyDown = new MacroNode
        {
            Type = MacroNodeType.KeyDown,
            KeyName = "A",
            VirtualKey = 65
        };
        var keyUp = new MacroNode
        {
            Type = MacroNodeType.KeyUp,
            KeyName = "A",
            VirtualKey = 65
        };
        timeline.Nodes.Add(keyDown);
        timeline.Nodes.Add(keyUp);

        var selectedDisplayNode = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes,
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown).Single();
        var rebuiltDisplayNode = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes,
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown).Single();

        var selection = new TimelineSelectionState();
        selection.SelectNodes(timeline, new[] { selectedDisplayNode }, selectedDisplayNode);

        Assert.True(selection.IsNodeSelected(timeline, rebuiltDisplayNode));
        Assert.True(selection.IsNodeSelected(timeline, keyDown));
    }
}
