using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KeyLine.Domain;
using KeyLine.Services.Features;
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
        AddPopup.PlacementTarget = placementTarget;

        AddPopup.Placement = PlacementMode.Top;
        UpdateFeatureAddMenuVisibility();
        UpdateExperimentalAddMenuVisibility();
        AddPopup.IsOpen = true;
    }

    private void UpdateFeatureAddMenuVisibility()
    {
        ApplyFeatureAddMenuButtonState(RepeatBlockMenuButton, FeatureId.RepeatBlocks, "Repeat block");
        ApplyFeatureAddMenuButtonState(ConditionBlockMenuButton, FeatureId.ConditionBlocks, "Condition block");
        ApplySystemAddMenuState();
    }

    private void ApplyFeatureAddMenuButtonState(Button button, FeatureId feature, string label)
    {
        if (_featureGate.IsHidden(feature))
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        button.Visibility = Visibility.Visible;

        if (_featureGate.IsEnabled(feature))
        {
            button.Content = label;
            button.Opacity = 1.0;
            button.ToolTip = null;
            return;
        }

        button.Content = $"{label} (locked)";
        button.Opacity = 0.55;
        button.ToolTip = _featureGate.GetLockedFeatureMessage(feature);
    }

    private void ApplySystemAddMenuState()
    {
        if (SystemAddMenuExpander == null)
            return;

        if (_featureGate.IsHidden(FeatureId.SystemNodes))
        {
            SystemAddMenuExpander.Visibility = Visibility.Collapsed;
            return;
        }

        SystemAddMenuExpander.Visibility = Visibility.Visible;
        ApplyFeatureAddMenuButtonState(SystemOpenLaunchMenuButton, FeatureId.SystemNodes, "Open/Launch");
        ApplyFeatureAddMenuButtonState(SystemVolumeControlMenuButton, FeatureId.SystemNodes, "Volume control");
        ApplyFeatureAddMenuButtonState(SystemWaitUntilWindowOpensMenuButton, FeatureId.SystemNodes, "Wait until window opens");
        ApplyFeatureAddMenuButtonState(SystemFocusWindowMenuButton, FeatureId.SystemNodes, "Focus window");
        ApplyFeatureAddMenuButtonState(SystemSelectTargetWindowMenuButton, FeatureId.SystemNodes, "Set target window");
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

    private void RandomDelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

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

    private void TextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

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

    private void RepeatBlockMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
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

    private void ConditionBlockMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
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

    private void CursorMoveMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

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

    private void MouseDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseDown);

    private void MouseUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseUp);

    private void MouseClickMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroNodeType.BackgroundMouseClick);

    private void SystemOpenLaunchMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddConfigurableSystemStep(new MacroNode
        {
            Type = MacroNodeType.SystemOpenLaunch,
            SystemLaunchKind = SystemLaunchKind.Application,
            SystemLaunchTarget = ""
        });
    }

    private void SystemVolumeControlMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddConfigurableSystemStep(new MacroNode
        {
            Type = MacroNodeType.SystemVolumeControl,
            SystemVolumeAction = SystemVolumeAction.VolumeUp,
            SystemVolumePercent = 50
        });
    }

    private void SystemWaitUntilWindowOpensMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddConfigurableSystemStep(new MacroNode
        {
            Type = MacroNodeType.SystemWaitUntilWindowOpens,
            SystemWaitWindowTitle = "",
            SystemWaitPollIntervalMs = 250,
            WindowReference = WindowReference.Custom("")
        });
    }

    private void SystemFocusWindowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddConfigurableSystemStep(new MacroNode
        {
            Type = MacroNodeType.SystemFocusWindow,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.SelectedTarget
            }
        });
    }

    private void SystemSelectTargetWindowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddConfigurableSystemStep(new MacroNode
        {
            Type = MacroNodeType.SystemSelectTargetWindow,
            SystemTargetWindowTitle = "",
            WindowReference = WindowReference.Custom("")
        });
    }

    private void AddConfigurableSystemStep(MacroNode step)
    {
        AddPopup.IsOpen = false;
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

    private void MouseScrollUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseScrollStep(MacroNodeType.MouseScrollUp);

    private void MouseScrollDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseScrollStep(MacroNodeType.MouseScrollDown);

    private void MouseScrollLeftMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseScrollStep(MacroNodeType.MouseScrollLeft);

    private void MouseScrollRightMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseScrollStep(MacroNodeType.MouseScrollRight);

    private void AddMouseStep(MacroNodeType type)
    {
        AddPopup.IsOpen = false;

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
        AddPopup.IsOpen = false;

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
