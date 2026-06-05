using System.Windows;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Timeline;
using KeyLine.UI.MainWindow.Controls;

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
        SaveDocumentUndoSnapshot();
        var timeline = _document.AddTimeline();
        ApplyDefaultSettingsToTimeline(timeline, _settings);
        if (IsRemapShortcutMode())
            timeline.LoopCount = 1;

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
            TimelineAddMenu.Open(placementTarget, _featureGate, _settings.ExperimentalFeaturesEnabled);
    }

    private void TimelineAddMenu_ActionRequested(object? sender, TimelineAddMenuActionEventArgs e)
    {
        switch (e.Action)
        {
            case TimelineAddMenuAction.RecordInput:
                StartRecordingFromAddMenu();
                break;
            case TimelineAddMenuAction.Delay:
                AddDelayStep();
                break;
            case TimelineAddMenuAction.RandomDelay:
                AddRandomDelayStep();
                break;
            case TimelineAddMenuAction.Text:
                AddTextStep();
                break;
            case TimelineAddMenuAction.RepeatBlock:
                AddRepeatBlock();
                break;
            case TimelineAddMenuAction.ConditionBlock:
                AddConditionBlock();
                break;
            case TimelineAddMenuAction.CursorMove:
                AddCursorMoveStep();
                break;
            case TimelineAddMenuAction.MouseScrollUp:
                AddMouseScrollStep(MacroNodeType.MouseScrollUp);
                break;
            case TimelineAddMenuAction.MouseScrollDown:
                AddMouseScrollStep(MacroNodeType.MouseScrollDown);
                break;
            case TimelineAddMenuAction.MouseScrollLeft:
                AddMouseScrollStep(MacroNodeType.MouseScrollLeft);
                break;
            case TimelineAddMenuAction.MouseScrollRight:
                AddMouseScrollStep(MacroNodeType.MouseScrollRight);
                break;
            case TimelineAddMenuAction.SystemOpenLaunch:
                AddConfigurableSystemStep(new MacroNode
                {
                    Type = MacroNodeType.SystemOpenLaunch,
                    SystemLaunchKind = SystemLaunchKind.Application,
                    SystemLaunchTarget = ""
                });
                break;
            case TimelineAddMenuAction.SystemVolumeControl:
                AddConfigurableSystemStep(new MacroNode
                {
                    Type = MacroNodeType.SystemVolumeControl,
                    SystemVolumeAction = SystemVolumeAction.VolumeUp,
                    SystemVolumePercent = 50
                });
                break;
            case TimelineAddMenuAction.SystemWaitUntilWindowOpens:
                AddConfigurableSystemStep(new MacroNode
                {
                    Type = MacroNodeType.SystemWaitUntilWindowOpens,
                    SystemWaitWindowTitle = "",
                    SystemWaitPollIntervalMs = 250,
                    WindowReference = WindowReference.Custom("")
                });
                break;
            case TimelineAddMenuAction.SystemFocusWindow:
                AddConfigurableSystemStep(new MacroNode
                {
                    Type = MacroNodeType.SystemFocusWindow,
                    WindowReference = new WindowReference
                    {
                        Type = WindowReferenceType.SelectedTarget
                    }
                });
                break;
            case TimelineAddMenuAction.SystemSelectTargetWindow:
                AddConfigurableSystemStep(new MacroNode
                {
                    Type = MacroNodeType.SystemSelectTargetWindow,
                    SystemTargetWindowTitle = "",
                    WindowReference = WindowReference.Custom("")
                });
                break;
            case TimelineAddMenuAction.BackgroundMouseDown:
                AddMouseStep(MacroNodeType.BackgroundMouseDown);
                break;
            case TimelineAddMenuAction.BackgroundMouseUp:
                AddMouseStep(MacroNodeType.BackgroundMouseUp);
                break;
            case TimelineAddMenuAction.BackgroundMouseClick:
                AddMouseStep(MacroNodeType.BackgroundMouseClick);
                break;
        }
    }

    private void UpdateExperimentalAddMenuVisibility()
    {
        TimelineAddMenu?.SetExperimentalFeaturesVisible(_settings.ExperimentalFeaturesEnabled);
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

    private void StartRecordingFromAddMenu()
    {
        TimelineAddMenu.Close();
        StartRecording(GetPopupTimeline(), GetPopupRawInsertAnchor());
    }

    private void AddDelayStep()
    {
        TimelineAddMenu.Close();

        SaveDocumentUndoSnapshot();
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

    private void AddRandomDelayStep()
    {
        TimelineAddMenu.Close();

        SaveDocumentUndoSnapshot();
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

    private void AddTextStep()
    {
        TimelineAddMenu.Close();

        var timeline = GetPopupTimeline();
        var dialog = new TextInputWindow { Owner = this };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        SaveDocumentUndoSnapshot();
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

    private void AddRepeatBlock()
    {
        TimelineAddMenu.Close();
        if (!TryUseFeature(FeatureId.RepeatBlocks))
            return;

        SaveDocumentUndoSnapshot();
        var timeline = GetPopupTimeline();
        var (repeatStart, repeatEnd) = TimelineBlockService.CreateRepeatBlock();

        InsertPopupSteps(timeline, new[] { repeatStart, repeatEnd });

        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNodes(timeline, new[] { repeatStart, repeatEnd }, repeatStart);
        RefreshTimeline();
        RefreshInspector();
        ScheduleSaveState();
    }

    private void AddConditionBlock()
    {
        TimelineAddMenu.Close();
        if (!TryUseFeature(FeatureId.ConditionBlocks))
            return;

        SaveDocumentUndoSnapshot();
        var timeline = GetPopupTimeline();
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();

        InsertPopupSteps(timeline, new[] { conditionStart, conditionEnd });

        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNodes(timeline, new[] { conditionStart, conditionEnd }, conditionStart);
        RefreshTimeline();
        RefreshInspector();
        ScheduleSaveState();
    }

    private void AddCursorMoveStep()
    {
        TimelineAddMenu.Close();

        SaveDocumentUndoSnapshot();
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

    private void AddConfigurableSystemStep(MacroNode step)
    {
        TimelineAddMenu.Close();
        if (!TryUseFeature(FeatureId.SystemNodes))
            return;

        SaveDocumentUndoSnapshot();
        var timeline = GetPopupTimeline();
        InsertPopupSteps(timeline, new[] { step });

        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNode(timeline, step);
        RefreshTimeline();
        OpenInspectorFromSelection();
        ScheduleSaveState();
    }

    private void AddMouseStep(MacroNodeType type)
    {
        TimelineAddMenu.Close();

        SaveDocumentUndoSnapshot();
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

    private void AddMouseScrollStep(MacroNodeType type)
    {
        TimelineAddMenu.Close();

        SaveDocumentUndoSnapshot();
        var timeline = GetPopupTimeline();
        var step = new MacroNode
        {
            Type = type,
            KeyName = type switch
            {
                MacroNodeType.MouseScrollLeft => "Wheel Left",
                MacroNodeType.MouseScrollRight => "Wheel Right",
                MacroNodeType.MouseScrollDown => "Wheel Down",
                _ => "Wheel Up"
            },
            MouseWheelDelta = type is MacroNodeType.MouseScrollDown or MacroNodeType.MouseScrollLeft
                ? -KeyLine.Interop.NativeMethods.WHEEL_DELTA
                : KeyLine.Interop.NativeMethods.WHEEL_DELTA,
            MouseScrollAmount = 1
        };

        InsertPopupSteps(timeline, new[] { step });
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline, refreshInspector: false);
        _selection.SelectNode(timeline, step);
        RefreshTimeline();
        OpenInspectorFromSelection();
        ScheduleSaveState();
    }
}
