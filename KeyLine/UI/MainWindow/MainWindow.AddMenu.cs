using System.Windows;
using System.Windows.Controls.Primitives;
using KeyLine.Domain;
using KeyLine.Services.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    private sealed class AddNodeContext
    {
        public required MacroTimeline Timeline { get; init; }
        public MacroNode? RawInsertAnchor { get; init; }
    }

    private void AddTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        SaveUndoSnapshot();
        var timeline = _document.AddTimeline();
        ApplyDefaultSettingsToTimeline(timeline, _settings);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (AnyPlaybackRunning())
            return;

        var context = ResolveAddNodeContextFromSender(sender);
        _popupTimeline = context?.Timeline ?? ResolveTimelineFromSender(sender) ?? _document.ActiveTimeline;
        _popupRawInsertAnchor = context?.RawInsertAnchor;

        if (sender is UIElement placementTarget)
            AddPopup.PlacementTarget = placementTarget;

        AddPopup.Placement = PlacementMode.Top;
        UpdateExperimentalAddMenuVisibility();
        AddPopup.IsOpen = true;
    }

    private void UpdateExperimentalAddMenuVisibility()
    {
        if (ExperimentalAddMenuExpander != null)
            ExperimentalAddMenuExpander.Visibility = _settings.ExperimentalFeaturesEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private MacroTimeline? ResolveTimelineFromSender(object sender)
    {
        if (sender is FrameworkElement fe && fe.Tag is MacroTimeline timeline)
            return timeline;

        return null;
    }

    private AddNodeContext? ResolveAddNodeContextFromSender(object sender)
    {
        return sender is FrameworkElement { Tag: AddNodeContext context }
            ? context
            : null;
    }

    private MacroTimeline GetPopupTimeline()
    {
        return _popupTimeline ?? _document.ActiveTimeline;
    }

    private MacroNode? GetPopupRawInsertAnchor()
    {
        var timeline = GetPopupTimeline();
        return _popupRawInsertAnchor != null && timeline.Nodes.Contains(_popupRawInsertAnchor)
            ? _popupRawInsertAnchor
            : null;
    }

    private int GetPopupInsertIndex(MacroTimeline timeline)
    {
        var anchor = GetPopupRawInsertAnchor();
        if (anchor == null)
            return timeline.Nodes.Count;

        var anchorIndex = timeline.Nodes.IndexOf(anchor);
        return anchorIndex >= 0 ? anchorIndex : timeline.Nodes.Count;
    }

    private void InsertPopupSteps(MacroTimeline timeline, IReadOnlyList<MacroNode> steps)
    {
        var insertIndex = GetPopupInsertIndex(timeline);
        for (var i = 0; i < steps.Count; i++)
            timeline.Nodes.Insert(insertIndex + i, steps[i]);
    }

    private void RecordMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        StartRecording(GetPopupTimeline(), GetPopupRawInsertAnchor());
    }

    private void DelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        InsertPopupSteps(timeline, new[]
        {
            new MacroNode
            {
                Type = MacroNodeType.Delay,
                DelayMs = 100,
                IsRecordedDelay = false
            }
        });

        timeline.UseStandardDelay = false;
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void RandomDelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        InsertPopupSteps(timeline, new[]
        {
            new MacroNode
            {
                Type = MacroNodeType.RandomDelay,
                RandomDelayMinMs = 50,
                RandomDelayMaxMs = 150,
                IsRecordedDelay = false
            }
        });

        timeline.UseStandardDelay = false;
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void TextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        var dialog = new TextInputWindow { Owner = this };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        SaveUndoSnapshot();
        InsertPopupSteps(timeline, new[]
        {
            new MacroNode
            {
                Type = MacroNodeType.Text,
                Text = dialog.ResultText
            }
        });

        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void RepeatBlockMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        var (repeatStart, repeatEnd) = TimelineBlockService.CreateRepeatBlock();

        InsertPopupSteps(timeline, new[] { repeatStart, repeatEnd });

        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNodes(timeline, new[] { repeatStart, repeatEnd }, repeatStart);
        RefreshTimeline();
        RefreshInspector();
        ScheduleSaveState();
    }

    private void ConditionBlockMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();

        InsertPopupSteps(timeline, new[] { conditionStart, conditionEnd });

        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNodes(timeline, new[] { conditionStart, conditionEnd }, conditionStart);
        RefreshTimeline();
        RefreshInspector();
        ScheduleSaveState();
    }

    private void CursorMoveMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        var step = new MacroNode
        {
            Type = MacroNodeType.CursorMove,
            MouseX = 0,
            MouseY = 0
        };

        InsertPopupSteps(timeline, new[] { step });
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void MouseDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseDown);

    private void MouseUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseUp);

    private void MouseClickMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseClick);

    private void AddMouseStep(MacroNodeType type)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        InsertPopupSteps(timeline, new[]
        {
            new MacroNode
            {
                Type = type,
                MouseX = 0,
                MouseY = 0
            }
        });

        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

}
