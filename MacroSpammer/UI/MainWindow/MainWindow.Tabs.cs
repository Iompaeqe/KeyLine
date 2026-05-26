using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private static MacroWorkspace CreateWorkspace(int number)
    {
        return new MacroWorkspace
        {
            Name = $"Macro {number}"
        };
    }

    private void AddMacroTabButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureActiveWorkspaceState();

        var workspace = CreateWorkspace(_workspaces.Count + 1);
        _workspaces.Add(workspace);
        ActivateWorkspace(_workspaces.Count - 1);
    }

    private void ActivateWorkspace(int index, bool saveCurrent = true)
    {
        if (index < 0 || index >= _workspaces.Count)
            return;

        _pendingDeleteWorkspace = null;
        _renamingWorkspace = null;

        if (saveCurrent)
            CaptureActiveWorkspaceState();

        StopAllRunners();
        _runners.Clear();
        SetStoppedStatus();

        if (_recorder.IsRecording)
            StopRecording();

        _isSwitchingWorkspace = true;

        try
        {
            _activeWorkspaceIndex = index;
            _activeWorkspace = _workspaces[_activeWorkspaceIndex];
            _document = _activeWorkspace.Document;

            _selection.Clear();
            _timelineVisualPositions.Clear();
            ResetClearConfirmation();

            LoopCountTextBox.Text = _activeWorkspace.LoopCount.ToString();
            TimerMinutesTextBox.Text = _activeWorkspace.TimerMs.ToString();
            BaseDelayTextBox.Text = _activeWorkspace.BaseDelayMs.ToString();
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, _activeWorkspace.TimerMs);
            SetFormattedDelayInput(BaseDelayTextBox, BaseDelayUnitTextBlock, _activeWorkspace.BaseDelayMs);
            UpdateShortcutText();
            RefreshMacroTabs();
            RestoreTargetWindowSelection(_activeWorkspace);
            SelectTimeline(_document.ActiveTimeline);
            RefreshTimeline();
        }
        finally
        {
            _isSwitchingWorkspace = false;
        }

        if (saveCurrent)
            ScheduleSaveState();
    }

    private void RefreshMacroTabs()
    {
        if (MacroTabsPanel == null)
            return;

        MacroTabsPanel.Children.Clear();

        for (var i = 0; i < _workspaces.Count; i++)
        {
            var index = i;
            var workspace = _workspaces[i];
            var isActive = i == _activeWorkspaceIndex;
            var isPendingDelete = ReferenceEquals(workspace, _pendingDeleteWorkspace);
            var isRenaming = ReferenceEquals(workspace, _renamingWorkspace);

            if (isRenaming)
            {
                MacroTabsPanel.Children.Add(CreateRenameTextBox(workspace, i));
                continue;
            }

            var button = new Button
            {
                Content = isPendingDelete ? $"{workspace.Name} - Confirm" : workspace.Name,
                Height = 22,
                MinWidth = isPendingDelete ? 104 : 68,
                MaxWidth = isPendingDelete ? 128 : 92,
                Padding = new Thickness(10, 0, 10, 1),
                Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(127, 29, 29)
                    : isActive
                    ? Color.FromRgb(18, 58, 90)
                    : Color.FromRgb(17, 24, 39)),
                BorderBrush = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(248, 113, 113)
                    : isActive
                    ? Color.FromRgb(37, 109, 157)
                    : Color.FromRgb(38, 50, 68)),
                Foreground = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(254, 202, 202)
                    : isActive
                    ? Color.FromRgb(186, 230, 253)
                    : Color.FromRgb(142, 160, 182)),
                Tag = workspace
            };

            button.Click += (_, _) =>
            {
                if (_didDragMacroTabs)
                {
                    _didDragMacroTabs = false;
                    return;
                }

                ActivateWorkspace(index);
            };
            button.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount < 2)
                    return;

                BeginWorkspaceRename(workspace);
                e.Handled = true;
            };
            button.PreviewMouseRightButtonDown += (_, e) =>
            {
                BeginOrConfirmWorkspaceDelete(workspace);
                e.Handled = true;
            };
            MacroTabsPanel.Children.Add(button);
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(UpdateMacroTabEdgeIndicators));
    }

    private void CaptureActiveWorkspaceState()
    {
        if (_isSwitchingWorkspace)
            return;

        _activeWorkspace.Document = _document;
        if (_runners.Values.Any(runner => runner.IsRunning))
        {
            _activeWorkspace.LoopCount = int.TryParse(_originalLoopText, out var loops) ? Math.Max(0, loops) : 0;
            _activeWorkspace.TimerMs = Math.Max(0, _originalTimerMs);
        }
        else
        {
            _activeWorkspace.LoopCount = GetLoopCount();
            _activeWorkspace.TimerMs = GetTimerMs();
        }

        _activeWorkspace.BaseDelayMs = GetBaseDelayMs();
        CaptureSelectedTargetWindow(_activeWorkspace);
    }
}
