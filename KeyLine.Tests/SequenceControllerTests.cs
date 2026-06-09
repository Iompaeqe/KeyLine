using KeyLine.Domain;
using KeyLine.Services.Playback;

namespace KeyLine.Tests;

public sealed class SequenceControllerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static MacroTimeline Timeline(int cooldownMs = 0, bool withNode = true)
    {
        var timeline = new MacroTimeline { CooldownMs = cooldownMs };
        if (withNode)
            timeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "x" });
        return timeline;
    }

    [Fact]
    public void Sequence_AdvancesThroughTimelinesAndWraps()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(), Timeline(), Timeline() };

        var played = new List<MacroTimeline>();
        for (var i = 0; i < 4; i++)
        {
            var selected = controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now);
            Assert.NotNull(selected);
            controller.MarkPlayed(workspace, timelines, selected!, Now);
            played.Add(selected!);
        }

        Assert.Equal(new[] { timelines[0], timelines[1], timelines[2], timelines[0] }, played);
    }

    [Fact]
    public void Sequence_SkipsTimelinesOnCooldownWithWrap()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        // T1 and T2 have long cooldowns; T3 has none.
        var timelines = new[] { Timeline(10_000), Timeline(10_000), Timeline(0) };

        // Play T1, T2, T3 -> pointer wraps to index 0, T1 and T2 still on cooldown.
        controller.MarkPlayed(workspace, timelines, controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now)!, Now);
        controller.MarkPlayed(workspace, timelines, controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now)!, Now);
        controller.MarkPlayed(workspace, timelines, controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now)!, Now);

        // Pointer is at index 0 (T1, on cooldown) -> skip T1, skip T2, land on T3.
        var selected = controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now);

        Assert.Same(timelines[2], selected);
    }

    [Fact]
    public void AllOnCooldown_ReturnsNullAndLeavesPointerUnchanged()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(10_000), Timeline(10_000) };

        controller.MarkPlayed(workspace, timelines, controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now)!, Now);
        controller.MarkPlayed(workspace, timelines, controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now)!, Now);

        var pointerBefore = controller.GetNextIndex(workspace, timelines.Length);
        var selected = controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now);

        Assert.Null(selected);
        Assert.Equal(pointerBefore, controller.GetNextIndex(workspace, timelines.Length));
    }

    [Fact]
    public void Cooldown_ExpiresAfterConfiguredDuration()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(5_000) };

        controller.MarkPlayed(workspace, timelines, timelines[0], Now);

        Assert.True(controller.IsOnCooldown(workspace, timelines[0], Now.AddSeconds(4)));
        Assert.False(controller.IsOnCooldown(workspace, timelines[0], Now.AddSeconds(6)));
    }

    [Fact]
    public void Random_NeverPicksTimelineOnCooldown()
    {
        var controller = new SequenceController(new Random(12345));
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(0), Timeline(10_000), Timeline(0) };

        // Put T2 on cooldown.
        controller.MarkPlayed(workspace, timelines, timelines[1], Now);

        for (var i = 0; i < 30; i++)
        {
            var selected = controller.SelectNext(workspace, timelines, SequenceSelectionMode.Random, Now);
            Assert.NotNull(selected);
            Assert.NotSame(timelines[1], selected);
        }
    }

    [Fact]
    public void EmptyTimeline_IsSkipped()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(withNode: false), Timeline() };

        var selected = controller.SelectNext(workspace, timelines, SequenceSelectionMode.Sequence, Now);

        Assert.Same(timelines[1], selected);
    }

    [Fact]
    public void Reset_ClearsActiveCooldownsAndPointer_ButNotConfiguredCooldown()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();
        var timelines = new[] { Timeline(5_000), Timeline() };

        controller.MarkPlayed(workspace, timelines, timelines[0], Now);
        Assert.True(controller.IsOnCooldown(workspace, timelines[0], Now.AddSeconds(1)));

        controller.Reset(workspace);

        Assert.False(controller.IsOnCooldown(workspace, timelines[0], Now.AddSeconds(1)));
        Assert.Equal(0, controller.GetNextIndex(workspace, timelines.Length));
        Assert.Null(controller.GetLastPlayed(workspace));
        Assert.Equal(5_000, timelines[0].CooldownMs); // configured value preserved
    }

    [Fact]
    public void NoNormalTimelines_ReturnsNull()
    {
        var controller = new SequenceController();
        var workspace = new MacroWorkspace();

        Assert.Null(controller.SelectNext(workspace, Array.Empty<MacroTimeline>(), SequenceSelectionMode.Sequence, Now));
        Assert.Null(controller.SelectNext(workspace, Array.Empty<MacroTimeline>(), SequenceSelectionMode.Random, Now));
    }
}
