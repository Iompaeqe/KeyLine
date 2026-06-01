using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.UI;

namespace MacroSpammer;

public partial class MainWindow
{
    private static MacroWorkspace CreateWorkspace(int number, AppSettings? settings = null)
    {
        var workspace = new MacroWorkspace
        {
            Name = $"Macro {number}",
            TimerMs = settings?.DefaultTimerMs ?? 0,
            LoopCount = settings?.DefaultLoopCount ?? 0,
            BaseDelayMs = settings?.DefaultBaseDelayMs ?? 50
        };

        if (settings != null)
            ApplyDefaultSettingsToTimeline(workspace.Document.ActiveTimeline, settings);

        return workspace;
    }

    private static void ApplyDefaultSettingsToTimeline(MacroTimeline timeline, AppSettings settings)
    {
        timeline.UseStandardDelay = settings.DefaultStandardDelayEnabled;
        timeline.StandardDelayMs = Math.Max(0, settings.DefaultStandardDelayMs);
        timeline.ShowKeyUpDown = !settings.DefaultStandardDelayEnabled || settings.DefaultShowKeyUpDown;
        timeline.UseTextInputMode = settings.DefaultTextInputMode;
        timeline.LoopCount = Math.Max(0, settings.DefaultLoopCount);
        timeline.BaseDelayMs = Math.Max(0, settings.DefaultBaseDelayMs);
    }

    private int GetNextWorkspaceNumber()
    {
        var usedNumbers = new HashSet<int>();

        foreach (var workspace in _workspaces)
        {
            if (!TryParseDefaultWorkspaceNumber(workspace.Name, out var number))
                continue;

            usedNumbers.Add(number);
        }

        var candidate = 1;
        while (usedNumbers.Contains(candidate))
            candidate++;

        return candidate;
    }

    private static bool TryParseDefaultWorkspaceNumber(string name, out int number)
    {
        number = 0;

        const string prefix = "Macro ";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(name[prefix.Length..], out number) && number > 0;
    }

    private void AddMacroTabButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureActiveWorkspaceState();

        var workspace = CreateWorkspace(GetNextWorkspaceNumber(), _settings);
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

            TimerMinutesTextBox.Text = _activeWorkspace.TimerMs.ToString();
            LoopTypePager.Text = _activeWorkspace.LoopType == MacroLoopType.Sync ? "synced" : "asynced";
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, _activeWorkspace.TimerMs);
            TargetWindowSearchTextBox.Text = _activeWorkspace.TargetWindowSearchName;
            UpdateShortcutText();
            RefreshMacroTabs();
            RestoreTargetWindowSelection(_activeWorkspace);
            if (!HasResolvedTargetSelection() && !string.IsNullOrWhiteSpace(_activeWorkspace.TargetWindowSearchName))
                TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);
            SelectTimeline(_document.ActiveTimeline);
            RefreshTimeline();
            RefreshActiveWorkspacePlaybackUi();
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
            var isRunning = IsWorkspaceRunning(workspace);
            var hasError = !string.IsNullOrWhiteSpace(workspace.ErrorMessage);

            if (isRenaming)
            {
                MacroTabsPanel.Children.Add(CreateRenameTextBox(workspace, i));
                continue;
            }

            var grid = new Grid
            {
                Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0)
            };

            var button = new Button
            {
                Height = 22,
                MinWidth = isPendingDelete ? 104 : 68,
                MaxWidth = isPendingDelete ? 128 : 92,
                Padding = new Thickness(10, 0, isPendingDelete ? 10 : 20, 1),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(127, 29, 29)
                    : hasError
                    ? Color.FromRgb(127, 29, 29)
                    : isRunning && isActive
                    ? Color.FromRgb(5, 150, 105)
                    : isRunning
                    ? Color.FromRgb(5, 46, 38)
                    : isActive
                    ? Color.FromRgb(18, 58, 90)
                    : Color.FromRgb(17, 24, 39)),
                BorderBrush = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(248, 113, 113)
                    : hasError
                    ? Color.FromRgb(248, 113, 113)
                    : isRunning && isActive
                    ? Color.FromRgb(52, 211, 153)
                    : isRunning
                    ? Color.FromRgb(6, 95, 70)
                    : isActive
                    ? Color.FromRgb(37, 109, 157)
                    : Color.FromRgb(38, 50, 68)),
                Foreground = new SolidColorBrush(isPendingDelete
                    ? Color.FromRgb(254, 202, 202)
                    : hasError
                    ? Color.FromRgb(254, 202, 202)
                    : isRunning && isActive
                    ? Color.FromRgb(236, 253, 245)
                    : isRunning
                    ? Color.FromRgb(167, 243, 208)
                    : isActive
                    ? Color.FromRgb(186, 230, 253)
                    : Color.FromRgb(142, 160, 182)),
                Tag = workspace,
                ToolTip = hasError
                    ? workspace.ErrorMessage
                    : isRunning
                        ? TooltipNotes.MacroTabRunning
                        : TooltipNotes.MacroTabSwitchRenameDelete
            };

            button.Padding = new Thickness(10, 0, 10, 1);
            button.Content = isPendingDelete ? $"{workspace.Name} - Confirm" : workspace.Name;

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

            grid.Children.Add(button);

            if (!isPendingDelete)
            {
                var editIcon = new TextBlock
                {
                    Text = "✎",
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 6, 2),
                    Foreground = button.Foreground,
                    Visibility = Visibility.Collapsed,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    IsHitTestVisible = true,
                    ToolTip = TooltipNotes.RenameMacro
                };

                editIcon.MouseLeftButtonDown += (_, e) =>
                {
                    BeginWorkspaceRename(workspace);
                    e.Handled = true;
                };

                // Show icon on hover
                grid.MouseEnter += (_, _) =>
                {
                    editIcon.Visibility = Visibility.Visible;
                    button.Padding = new Thickness(10, 0, 20, 1);
                };
                grid.MouseLeave += (_, _) =>
                {
                    editIcon.Visibility = Visibility.Collapsed;
                    button.Padding = new Thickness(10, 0, 10, 1);
                };
                editIcon.MouseEnter += (_, _) => editIcon.Opacity = 1.0;
                editIcon.MouseLeave += (_, _) => editIcon.Opacity = 0.6;

                grid.Children.Add(editIcon);
            }
            else
            {
                button.Padding = new Thickness(10, 0, 10, 1);
            }

            MacroTabsPanel.Children.Add(grid);
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
        if (IsWorkspaceRunning(_activeWorkspace))
        {
            _activeWorkspace.TimerMs = Math.Max(0, _originalTimerMs);
        }
        else
        {
            _activeWorkspace.TimerMs = GetTimerMs();
        }

        _activeWorkspace.LoopCount = _document.ActiveTimeline.LoopCount;
        _activeWorkspace.BaseDelayMs = _document.ActiveTimeline.BaseDelayMs;
        _activeWorkspace.LoopType = LoopTypePager.Text == "synced" ? MacroLoopType.Sync : MacroLoopType.Async;
        _activeWorkspace.TargetWindowSearchName = TargetWindowSearchTextBox.Text.Trim();
        CaptureSelectedTargetWindow(_activeWorkspace);
    }
}
