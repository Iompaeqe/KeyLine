using KeyLine.Domain;
using KeyLine.Services.Edit;
using KeyLine.Services.Macro;

namespace KeyLine.Tests;

public sealed class MacroDocumentHistoryControllerTests
{
    [Fact]
    public void UndoRedoSnapshotsRestoreDocumentWithoutWorkspaceOptions()
    {
        var workspace = new MacroWorkspace
        {
            Name = "Current macro name",
            TimerMs = 100,
            LoopMode = MacroLoopMode.Async,
            ShortcutKeys = "17,65",
            ShortcutsEnabled = true,
            ShortcutTriggerBehavior = ShortcutTriggerBehavior.PassThrough,
            TargetWindowSearchName = "Before target",
            TargetWindowHandle = 123,
            TargetWindowTitle = "Before title",
            TargetChildWindowHandle = 456,
            TargetChildWindowTitle = "Before child title"
        };
        workspace.Document.ActiveTimeline.Name = "Before timeline";

        var history = new MacroDocumentHistoryController();
        history.SaveSnapshot(workspace);

        workspace.Name = "Changed macro name";
        workspace.TimerMs = 200;
        workspace.LoopMode = MacroLoopMode.Chain;
        workspace.ShortcutKeys = "17,66";
        workspace.ShortcutsEnabled = false;
        workspace.ShortcutTriggerBehavior = ShortcutTriggerBehavior.RemapConsume;
        workspace.TargetWindowSearchName = "Changed target";
        workspace.TargetWindowHandle = 789;
        workspace.TargetWindowTitle = "Changed title";
        workspace.TargetChildWindowHandle = 987;
        workspace.TargetChildWindowTitle = "Changed child title";
        workspace.Document.ActiveTimeline.Name = "Changed timeline";

        Assert.True(history.TryUndo(workspace, out var undoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(undoSnapshot);

        Assert.Equal("Before timeline", workspace.Document.ActiveTimeline.Name);
        Assert.Equal("Changed macro name", workspace.Name);
        Assert.Equal(200, workspace.TimerMs);
        Assert.Equal(MacroLoopMode.Chain, workspace.LoopMode);
        Assert.Equal("17,66", workspace.ShortcutKeys);
        Assert.False(workspace.ShortcutsEnabled);
        Assert.Equal(ShortcutTriggerBehavior.RemapConsume, workspace.ShortcutTriggerBehavior);
        Assert.Equal("Changed target", workspace.TargetWindowSearchName);
        Assert.Equal(789, workspace.TargetWindowHandle);
        Assert.Equal("Changed title", workspace.TargetWindowTitle);
        Assert.Equal(987, workspace.TargetChildWindowHandle);
        Assert.Equal("Changed child title", workspace.TargetChildWindowTitle);

        Assert.True(history.TryRedo(workspace, out var redoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(redoSnapshot);

        Assert.Equal("Changed timeline", workspace.Document.ActiveTimeline.Name);
        Assert.Equal("Changed macro name", workspace.Name);
        Assert.Equal(200, workspace.TimerMs);
        Assert.Equal(MacroLoopMode.Chain, workspace.LoopMode);
        Assert.Equal("17,66", workspace.ShortcutKeys);
        Assert.False(workspace.ShortcutsEnabled);
        Assert.Equal(ShortcutTriggerBehavior.RemapConsume, workspace.ShortcutTriggerBehavior);
        Assert.Equal("Changed target", workspace.TargetWindowSearchName);
        Assert.Equal(789, workspace.TargetWindowHandle);
        Assert.Equal("Changed title", workspace.TargetWindowTitle);
        Assert.Equal(987, workspace.TargetChildWindowHandle);
        Assert.Equal("Changed child title", workspace.TargetChildWindowTitle);
    }

    [Fact]
    public void UndoHistoryIsIndependentPerMacro()
    {
        var macro1 = CreateWorkspace("Macro 1", "Macro 1 before");
        var macro2 = CreateWorkspace("Macro 2", "Macro 2 before");
        var history = new MacroDocumentHistoryController();

        history.SaveSnapshot(macro1);
        history.SaveSnapshot(macro2);

        macro1.Document.ActiveTimeline.Name = "Macro 1 changed";
        macro2.Document.ActiveTimeline.Name = "Macro 2 changed";

        Assert.True(history.TryUndo(macro1, out var macro1UndoSnapshot));
        macro1.Document = MacroCloneService.CloneDocument(macro1UndoSnapshot);

        Assert.Equal("Macro 1 before", macro1.Document.ActiveTimeline.Name);
        Assert.Equal("Macro 2 changed", macro2.Document.ActiveTimeline.Name);

        Assert.True(history.TryUndo(macro2, out var macro2UndoSnapshot));
        macro2.Document = MacroCloneService.CloneDocument(macro2UndoSnapshot);

        Assert.Equal("Macro 1 before", macro1.Document.ActiveTimeline.Name);
        Assert.Equal("Macro 2 before", macro2.Document.ActiveTimeline.Name);
    }

    [Fact]
    public void UndoHistoryIsLimitedToThirtySnapshotsPerMacro()
    {
        var workspace = CreateWorkspace("Macro", "T0");
        var history = new MacroDocumentHistoryController();

        for (var i = 0; i < 35; i++)
        {
            workspace.Document.ActiveTimeline.Name = $"T{i}";
            history.SaveSnapshot(workspace);
        }

        workspace.Document.ActiveTimeline.Name = "Current";

        var undoCount = 0;
        MacroDocument? lastSnapshot = null;
        while (history.TryUndo(workspace, out var snapshot))
        {
            undoCount++;
            lastSnapshot = snapshot;
            workspace.Document = MacroCloneService.CloneDocument(snapshot);
        }

        Assert.Equal(30, undoCount);
        Assert.NotNull(lastSnapshot);
        Assert.Equal("T5", lastSnapshot.ActiveTimeline.Name);
    }

    [Fact]
    public void UndoRedoSnapshotsRestoreTimelineAndNodeData()
    {
        var workspace = CreateWorkspace("Macro", "Before");
        var history = new MacroDocumentHistoryController();
        var firstNode = new MacroNode
        {
            Type = MacroNodeType.Text,
            Text = "first"
        };
        workspace.Document.ActiveTimeline.Nodes.Add(firstNode);
        workspace.Document.ActiveTimeline.LoopCount = 2;

        history.SaveSnapshot(workspace);

        var timeline = workspace.Document.ActiveTimeline;
        timeline.Name = "After";
        timeline.LoopCount = 5;
        firstNode.Text = "edited";
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.Delay,
            DelayMs = 250
        });
        timeline.Nodes.Move(1, 0);

        Assert.True(history.TryUndo(workspace, out var undoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(undoSnapshot);

        Assert.Equal("Before", workspace.Document.ActiveTimeline.Name);
        Assert.Equal(2, workspace.Document.ActiveTimeline.LoopCount);
        Assert.Single(workspace.Document.ActiveTimeline.Nodes);
        Assert.Equal("first", workspace.Document.ActiveTimeline.Nodes[0].Text);

        Assert.True(history.TryRedo(workspace, out var redoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(redoSnapshot);

        Assert.Equal("After", workspace.Document.ActiveTimeline.Name);
        Assert.Equal(5, workspace.Document.ActiveTimeline.LoopCount);
        Assert.Equal(2, workspace.Document.ActiveTimeline.Nodes.Count);
        Assert.Equal(MacroNodeType.Delay, workspace.Document.ActiveTimeline.Nodes[0].Type);
        Assert.Equal("edited", workspace.Document.ActiveTimeline.Nodes[1].Text);
    }

    [Fact]
    public void UndoRedoRoundTripsTimelineDisabledState()
    {
        var workspace = CreateWorkspace("Macro", "Timeline");
        var timeline = workspace.Document.ActiveTimeline;
        timeline.IsDisabled = false;
        timeline.IsCollapsed = true;

        var history = new MacroDocumentHistoryController();
        history.SaveSnapshot(workspace);

        timeline.IsDisabled = true;

        Assert.True(history.TryUndo(workspace, out var undoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(undoSnapshot);
        Assert.False(workspace.Document.ActiveTimeline.IsDisabled);
        Assert.True(workspace.Document.ActiveTimeline.IsCollapsed);

        Assert.True(history.TryRedo(workspace, out var redoSnapshot));
        workspace.Document = MacroCloneService.CloneDocument(redoSnapshot);
        Assert.True(workspace.Document.ActiveTimeline.IsDisabled);
    }

    [Fact]
    public void CloneTimelinePreservesDisabledAndCollapsedState()
    {
        var source = new MacroTimeline
        {
            Name = "Source",
            IsDisabled = true,
            IsCollapsed = true
        };

        var clone = MacroCloneService.CloneTimeline(source);

        Assert.True(clone.IsDisabled);
        Assert.True(clone.IsCollapsed);
        Assert.Equal("Source", clone.Name);
    }

    private static MacroWorkspace CreateWorkspace(string name, string timelineName)
    {
        var workspace = new MacroWorkspace
        {
            Name = name
        };
        workspace.Document.ActiveTimeline.Name = timelineName;
        return workspace;
    }
}
