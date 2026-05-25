using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private const double MacroTabsDragThreshold = 4;
    private const double MacroTabsWheelScrollAmount = 48;

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

    private void DeleteWorkspace(MacroWorkspace workspace)
    {
        if (_workspaces.Count <= 1)
            return;

        var index = _workspaces.IndexOf(workspace);
        if (index < 0)
            return;

        StopAllRunners();
        _runners.Clear();
        SetStoppedStatus();

        if (_recorder.IsRecording)
            StopRecording();

        _workspaces.RemoveAt(index);
        _pendingDeleteWorkspace = null;

        if (_activeWorkspaceIndex > index)
            _activeWorkspaceIndex--;
        else if (_activeWorkspaceIndex >= _workspaces.Count)
            _activeWorkspaceIndex = _workspaces.Count - 1;

        ActivateWorkspace(_activeWorkspaceIndex);
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

    private void MacroTabsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var direction = e.Delta > 0 ? -1 : 1;
        MacroTabsScrollViewer.ScrollToHorizontalOffset(
            MacroTabsScrollViewer.HorizontalOffset + (direction * MacroTabsWheelScrollAmount));

        e.Handled = true;
    }

    private void MacroTabsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateMacroTabEdgeIndicators();
    }

    private void MacroTabsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMacroTabEdgeIndicators();
    }

    private void MacroTabsScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSourceInsideTextBox(e.OriginalSource as DependencyObject))
            return;

        _isDraggingMacroTabs = true;
        _didDragMacroTabs = false;
        _macroTabsDragStartPoint = e.GetPosition(MacroTabsScrollViewer);
        _macroTabsDragStartOffset = MacroTabsScrollViewer.HorizontalOffset;
    }

    private void MacroTabsScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingMacroTabs || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(MacroTabsScrollViewer);
        var deltaX = currentPoint.X - _macroTabsDragStartPoint.X;

        if (!_didDragMacroTabs && Math.Abs(deltaX) < MacroTabsDragThreshold)
            return;

        _didDragMacroTabs = true;
        MacroTabsScrollViewer.ScrollToHorizontalOffset(_macroTabsDragStartOffset - deltaX);

        if (!MacroTabsScrollViewer.IsMouseCaptured)
            MacroTabsScrollViewer.CaptureMouse();

        e.Handled = true;
    }

    private void MacroTabsScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndMacroTabsDrag();
    }

    private void MacroTabsScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndMacroTabsDrag();
    }

    private void EndMacroTabsDrag()
    {
        _isDraggingMacroTabs = false;

        if (MacroTabsScrollViewer?.IsMouseCaptured == true)
            MacroTabsScrollViewer.ReleaseMouseCapture();
    }

    private void UpdateMacroTabEdgeIndicators()
    {
        if (MacroTabsScrollViewer == null ||
            MacroTabsLeftEdgeFade == null ||
            MacroTabsRightEdgeFade == null ||
            MacroTabsLeftEdgeLine == null ||
            MacroTabsRightEdgeLine == null)
        {
            return;
        }

        var hasOverflow = MacroTabsScrollViewer.ScrollableWidth > 0.5;
        var canScrollLeft = hasOverflow && MacroTabsScrollViewer.HorizontalOffset > 0.5;
        var canScrollRight = hasOverflow &&
                             MacroTabsScrollViewer.HorizontalOffset < MacroTabsScrollViewer.ScrollableWidth - 0.5;

        MacroTabsLeftEdgeFade.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsLeftEdgeLine.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsRightEdgeFade.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsRightEdgeLine.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsSourceInsideTextBox(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is TextBox)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void BeginOrConfirmWorkspaceDelete(MacroWorkspace workspace)
    {
        if (_workspaces.Count <= 1)
            return;

        if (ReferenceEquals(_pendingDeleteWorkspace, workspace))
        {
            DeleteWorkspace(workspace);
            return;
        }

        _pendingDeleteWorkspace = workspace;
        _renamingWorkspace = null;
        RefreshMacroTabs();
    }

    private TextBox CreateRenameTextBox(MacroWorkspace workspace, int index)
    {
        var textBox = new TextBox
        {
            Text = workspace.Name,
            Height = 22,
            MinWidth = 92,
            MaxWidth = 128,
            Width = 104,
            Padding = new Thickness(8, 1, 8, 1),
            Margin = new Thickness(index == 0 ? 0 : 4, 0, 0, 0),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(10, 52, 84)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
            Foreground = new SolidColorBrush(Color.FromRgb(224, 242, 254)),
            Tag = workspace
        };

        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        textBox.LostKeyboardFocus += (_, _) => CommitWorkspaceRename(workspace, textBox.Text);
        textBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitWorkspaceRename(workspace, textBox.Text);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    CancelWorkspaceRename();
                    e.Handled = true;
                    break;
            }
        };

        return textBox;
    }

    private void BeginWorkspaceRename(MacroWorkspace workspace)
    {
        _pendingDeleteWorkspace = null;
        _renamingWorkspace = workspace;
        RefreshMacroTabs();
    }

    private void CommitWorkspaceRename(MacroWorkspace workspace, string name)
    {
        if (!ReferenceEquals(_renamingWorkspace, workspace))
            return;

        var normalizedName = NormalizeWorkspaceName(name);
        if (!string.IsNullOrWhiteSpace(normalizedName))
            workspace.Name = normalizedName;

        _renamingWorkspace = null;
        RefreshMacroTabs();
        ScheduleSaveState();
    }

    private void CancelWorkspaceRename()
    {
        if (_renamingWorkspace == null)
            return;

        _renamingWorkspace = null;
        RefreshMacroTabs();
    }

    private static string NormalizeWorkspaceName(string name)
    {
        var normalized = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= 24
            ? normalized
            : normalized[..24];
    }

    private void CaptureActiveWorkspaceState()
    {
        if (_isSwitchingWorkspace)
            return;

        _activeWorkspace.Document = _document;
        _activeWorkspace.LoopCount = GetLoopCount();
        CaptureSelectedTargetWindow(_activeWorkspace);
    }
}
