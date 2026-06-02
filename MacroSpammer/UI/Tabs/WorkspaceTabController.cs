using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.UI;

namespace MacroSpammer.UI.Tabs;

public sealed class WorkspaceTabController
{
    private const double DragThreshold = 4;
    private const double WheelScrollAmount = 48;

    private readonly Panel _tabsPanel;
    private readonly ScrollViewer _scrollViewer;
    private readonly UIElement _leftEdgeFade;
    private readonly UIElement _rightEdgeFade;
    private readonly UIElement _leftEdgeLine;
    private readonly UIElement _rightEdgeLine;
    private readonly Dispatcher _dispatcher;

    private readonly Func<IReadOnlyList<MacroWorkspace>> _getWorkspaces;
    private readonly Func<int> _getActiveWorkspaceIndex;
    private readonly Func<MacroWorkspace, bool> _isWorkspaceRunning;
    private readonly Action<int> _activateWorkspace;
    private readonly Action<MacroWorkspace> _deleteWorkspace;
    private readonly Action<string> _showWarning;
    private readonly Action _scheduleSaveState;

    private MacroWorkspace? _pendingDeleteWorkspace;
    private MacroWorkspace? _renamingWorkspace;

    private bool _isDraggingTabs;
    private bool _didDragTabs;
    private Point _dragStartPoint;
    private double _dragStartOffset;

    public WorkspaceTabController(
        Panel tabsPanel,
        ScrollViewer scrollViewer,
        UIElement leftEdgeFade,
        UIElement rightEdgeFade,
        UIElement leftEdgeLine,
        UIElement rightEdgeLine,
        Dispatcher dispatcher,
        Func<IReadOnlyList<MacroWorkspace>> getWorkspaces,
        Func<int> getActiveWorkspaceIndex,
        Func<MacroWorkspace, bool> isWorkspaceRunning,
        Action<int> activateWorkspace,
        Action<MacroWorkspace> deleteWorkspace,
        Action<string> showWarning,
        Action scheduleSaveState)
    {
        _tabsPanel = tabsPanel;
        _scrollViewer = scrollViewer;
        _leftEdgeFade = leftEdgeFade;
        _rightEdgeFade = rightEdgeFade;
        _leftEdgeLine = leftEdgeLine;
        _rightEdgeLine = rightEdgeLine;
        _dispatcher = dispatcher;
        _getWorkspaces = getWorkspaces;
        _getActiveWorkspaceIndex = getActiveWorkspaceIndex;
        _isWorkspaceRunning = isWorkspaceRunning;
        _activateWorkspace = activateWorkspace;
        _deleteWorkspace = deleteWorkspace;
        _showWarning = showWarning;
        _scheduleSaveState = scheduleSaveState;
    }

    public void ClearTransientState()
    {
        _pendingDeleteWorkspace = null;
        _renamingWorkspace = null;
    }

    public void Refresh()
    {
        _tabsPanel.Children.Clear();

        var workspaces = _getWorkspaces();
        var activeWorkspaceIndex = _getActiveWorkspaceIndex();

        for (var i = 0; i < workspaces.Count; i++)
        {
            var index = i;
            var workspace = workspaces[i];
            var isActive = i == activeWorkspaceIndex;
            var isPendingDelete = ReferenceEquals(workspace, _pendingDeleteWorkspace);
            var isRenaming = ReferenceEquals(workspace, _renamingWorkspace);
            var isRunning = _isWorkspaceRunning(workspace);
            var hasError = !string.IsNullOrWhiteSpace(workspace.ErrorMessage);

            if (isRenaming)
            {
                _tabsPanel.Children.Add(CreateRenameTextBox(workspace, i));
                continue;
            }

            _tabsPanel.Children.Add(CreateTabGrid(
                workspace,
                index,
                isActive,
                isPendingDelete,
                isRunning,
                hasError));
        }

        _dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(UpdateEdgeIndicators));
    }

    public void PreviewMouseWheel(MouseWheelEventArgs e)
    {
        var direction = e.Delta > 0 ? -1 : 1;
        _scrollViewer.ScrollToHorizontalOffset(
            _scrollViewer.HorizontalOffset + (direction * WheelScrollAmount));

        e.Handled = true;
    }

    public void ScrollChanged()
    {
        UpdateEdgeIndicators();
    }

    public void SizeChanged()
    {
        UpdateEdgeIndicators();
    }

    public void PreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (IsSourceInsideTextBox(e.OriginalSource as DependencyObject))
            return;

        _isDraggingTabs = true;
        _didDragTabs = false;
        _dragStartPoint = e.GetPosition(_scrollViewer);
        _dragStartOffset = _scrollViewer.HorizontalOffset;
    }

    public void PreviewMouseMove(MouseEventArgs e)
    {
        if (!_isDraggingTabs || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(_scrollViewer);
        var deltaX = currentPoint.X - _dragStartPoint.X;

        if (!_didDragTabs && Math.Abs(deltaX) < DragThreshold)
            return;

        _didDragTabs = true;
        _scrollViewer.ScrollToHorizontalOffset(_dragStartOffset - deltaX);

        if (!_scrollViewer.IsMouseCaptured)
            _scrollViewer.CaptureMouse();

        e.Handled = true;
    }

    public void PreviewMouseLeftButtonUp()
    {
        EndDrag();
    }

    public void MouseLeave(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndDrag();
    }

    private Grid CreateTabGrid(
        MacroWorkspace workspace,
        int index,
        bool isActive,
        bool isPendingDelete,
        bool isRunning,
        bool hasError)
    {
        var grid = new Grid
        {
            Margin = new Thickness(index == 0 ? 0 : 4, 0, 0, 0)
        };

        var button = CreateTabButton(workspace, isActive, isPendingDelete, isRunning, hasError);

        button.Click += (_, _) =>
        {
            if (_didDragTabs)
            {
                _didDragTabs = false;
                return;
            }

            _activateWorkspace(index);
        };
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount < 2)
                return;

            BeginRename(workspace);
            e.Handled = true;
        };
        button.PreviewMouseRightButtonDown += (_, e) =>
        {
            BeginOrConfirmDelete(workspace);
            e.Handled = true;
        };

        grid.Children.Add(button);

        if (isPendingDelete)
        {
            button.Padding = new Thickness(10, 0, 10, 1);
            return grid;
        }

        var editIcon = CreateEditIcon(button, workspace);
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

        grid.Children.Add(editIcon);
        return grid;
    }

    private Button CreateTabButton(
        MacroWorkspace workspace,
        bool isActive,
        bool isPendingDelete,
        bool isRunning,
        bool hasError)
    {
        return new Button
        {
            Height = 22,
            MinWidth = isPendingDelete ? 104 : 68,
            MaxWidth = isPendingDelete ? 128 : 92,
            Padding = new Thickness(10, 0, 10, 1),
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
                    : TooltipNotes.MacroTabSwitchRenameDelete,
            Content = isPendingDelete ? $"{workspace.Name} - Confirm" : workspace.Name
        };
    }

    private TextBlock CreateEditIcon(Button button, MacroWorkspace workspace)
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
            Cursor = Cursors.Hand,
            IsHitTestVisible = true,
            ToolTip = TooltipNotes.RenameMacro,
            Opacity = 0.6
        };

        editIcon.MouseLeftButtonDown += (_, e) =>
        {
            BeginRename(workspace);
            e.Handled = true;
        };
        editIcon.MouseEnter += (_, _) => editIcon.Opacity = 1.0;
        editIcon.MouseLeave += (_, _) => editIcon.Opacity = 0.6;

        return editIcon;
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

        textBox.LostKeyboardFocus += (_, _) => CommitRename(workspace, textBox.Text);
        textBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitRename(workspace, textBox.Text);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    CancelRename();
                    e.Handled = true;
                    break;
            }
        };

        return textBox;
    }

    private void BeginOrConfirmDelete(MacroWorkspace workspace)
    {
        if (_getWorkspaces().Count <= 1)
            return;

        if (_isWorkspaceRunning(workspace))
        {
            _pendingDeleteWorkspace = null;
            Refresh();
            _showWarning("Stop this macro before deleting it");
            return;
        }

        if (ReferenceEquals(_pendingDeleteWorkspace, workspace))
        {
            _pendingDeleteWorkspace = null;
            _renamingWorkspace = null;
            _deleteWorkspace(workspace);
            return;
        }

        _pendingDeleteWorkspace = workspace;
        _renamingWorkspace = null;
        Refresh();
    }

    private void BeginRename(MacroWorkspace workspace)
    {
        if (_isWorkspaceRunning(workspace))
        {
            _showWarning("Stop this macro before renaming it");
            return;
        }

        _pendingDeleteWorkspace = null;
        _renamingWorkspace = workspace;
        Refresh();
    }

    private void CommitRename(MacroWorkspace workspace, string name)
    {
        if (!ReferenceEquals(_renamingWorkspace, workspace))
            return;

        var normalizedName = NormalizeWorkspaceName(name);
        if (!string.IsNullOrWhiteSpace(normalizedName))
            workspace.Name = normalizedName;

        _renamingWorkspace = null;
        Refresh();
        _scheduleSaveState();
    }

    private void CancelRename()
    {
        if (_renamingWorkspace == null)
            return;

        _renamingWorkspace = null;
        Refresh();
    }

    private void EndDrag()
    {
        _isDraggingTabs = false;

        if (_scrollViewer.IsMouseCaptured)
            _scrollViewer.ReleaseMouseCapture();
    }

    private void UpdateEdgeIndicators()
    {
        var hasOverflow = _scrollViewer.ScrollableWidth > 0.5;
        var canScrollLeft = hasOverflow && _scrollViewer.HorizontalOffset > 0.5;
        var canScrollRight = hasOverflow &&
                             _scrollViewer.HorizontalOffset < _scrollViewer.ScrollableWidth - 0.5;

        _leftEdgeFade.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        _leftEdgeLine.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        _rightEdgeFade.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        _rightEdgeLine.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
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

    private static string NormalizeWorkspaceName(string name)
    {
        var normalized = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= 24
            ? normalized
            : normalized[..24];
    }
}
