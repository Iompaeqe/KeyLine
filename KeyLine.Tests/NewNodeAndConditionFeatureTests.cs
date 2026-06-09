using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.UI.Timeline;

namespace KeyLine.Tests;

public sealed class NewNodeAndConditionFeatureTests
{
    [Fact]
    public void ConditionDefinitions_EnableInvertOnlyForSupportedConditions()
    {
        Assert.True(MacroConditionDefinitions.CanInvert(MacroConditionType.TargetWindowFocused));
        Assert.True(MacroConditionDefinitions.CanInvert(MacroConditionType.WindowExists));
        Assert.True(MacroConditionDefinitions.CanInvert(MacroConditionType.MacroRunning));
        Assert.False(MacroConditionDefinitions.CanInvert(MacroConditionType.TimePassed));
    }

    [Fact]
    public void ConditionSummary_UsesGenericInvertOnlyWhenSupported()
    {
        var focused = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.TargetWindowFocused,
            ConditionIsInverted = true
        };
        var timePassed = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.TimePassed,
            ConditionIsInverted = true,
            ConditionTimePassedMs = 1000
        };

        Assert.Equal("If NOT Window Focused: Selected Target Window", NodeDisplayFormatter.GetConditionSummary(focused));
        Assert.Equal("If 1s Passed", NodeDisplayFormatter.GetConditionSummary(timePassed));
    }

    [Fact]
    public void ConditionSummary_DisplaysWindowAndMacroReferences()
    {
        var windowExists = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.WindowExists,
            ConditionIsInverted = true,
            WindowReference = WindowReference.Custom("Discord")
        };
        var macroRunning = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.MacroRunning,
            ConditionIsInverted = true,
            ConditionMacroId = "macro-b"
        };

        Assert.Equal(
            "If NOT Window Exists: Custom \"Discord\"",
            NodeDisplayFormatter.GetConditionSummary(windowExists));
        Assert.Equal(
            "If NOT Macro \"Buff Loop\" Is Running",
            NodeDisplayFormatter.GetConditionSummary(macroRunning, id => id == "macro-b" ? "Buff Loop" : null));
    }

    [Fact]
    public void TimePassedRuntimeState_IsScopedToRunContextAndResetsAfterPass()
    {
        var context = new MacroRunContext(0);
        var node = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.TimePassed,
            ConditionTimePassedMs = 5000
        };
        var start = new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(context.HasTimePassed(node, node.ConditionTimePassedMs, start));
        Assert.False(context.HasTimePassed(node, node.ConditionTimePassedMs, start.AddMilliseconds(4999)));
        Assert.True(context.HasTimePassed(node, node.ConditionTimePassedMs, start.AddMilliseconds(5000)));
        Assert.False(context.HasTimePassed(node, node.ConditionTimePassedMs, start.AddMilliseconds(9999)));
        Assert.True(context.HasTimePassed(node, node.ConditionTimePassedMs, start.AddMilliseconds(10000)));
    }

    [Fact]
    public void MacroRunningRuntime_ChecksOnlyOtherActiveProfileMacros()
    {
        var playback = new PlaybackController();
        var current = new MacroWorkspace { Id = "macro-a", Name = "Current" };
        var other = new MacroWorkspace { Id = "macro-b", Name = "Other" };
        var inactiveProfileMacro = new MacroWorkspace { Id = "macro-c", Name = "Inactive" };
        var context = new MacroRunContext(
            0,
            current.Id,
            new[] { current, other },
            playback);

        playback.MarkShortcutStarting(current);
        playback.MarkShortcutStarting(other);
        playback.MarkShortcutStarting(inactiveProfileMacro);

        Assert.False(playback.IsMacroRunning(current.Id, context));
        Assert.True(playback.IsMacroRunning(other.Id, context));
        Assert.False(playback.IsMacroRunning(inactiveProfileMacro.Id, context));
    }

    [Fact]
    public void ToggleKeyDisplay_ShowsToggleModeForRawAndSyntheticNodes()
    {
        var keyDown = new MacroNode
        {
            Type = MacroNodeType.KeyDown,
            KeyName = "Caps Lock",
            VirtualKey = 0x14,
            ToggleKeyMode = ToggleKeyMode.ToggleOn
        };
        var keyUp = new MacroNode
        {
            Type = MacroNodeType.KeyUp,
            KeyName = "Caps Lock",
            VirtualKey = 0x14
        };
        var timeline = new MacroTimeline
        {
            UseStandardDelay = true,
            ShowKeyUpDown = false
        };
        timeline.Nodes.Add(keyDown);
        timeline.Nodes.Add(keyUp);

        var synthetic = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes,
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown).Single();

        Assert.Equal("Caps Lock Toggle On", NodeDisplayFormatter.GetKeyText(keyDown));
        Assert.Equal("Caps Lock Toggle On", NodeDisplayFormatter.GetKeyText(synthetic));
    }
}
