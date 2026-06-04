using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.UI;

namespace KeyLine.UI.Tabs;

public sealed class WorkspaceTabController
{
    private const double DragThreshold = 4;
    private const double TabSpacing = 4;
    private const double ReorderModeTabSpacing = 12;
    private const double WheelScrollAmount = 48;
    private const double GhostOpacity = 0.86;
    private const double GhostScale = 1.04;
    private const double GhostFollowStrength = 0.65;

    private readonly Panel _tabsPanel;
    private readonly ScrollViewer _scrollViewer;
    private readonly Canvas _dragOverlay;
    private readonly UIElement _leftEdgeFade;
    private readonly UIElement _rightEdgeFade;
    private readonly UIElement _leftEdgeLine;
    private readonly UIElement _rightEdgeLine;
    private readonly Dispatcher _dispatcher;

    private readonly Func<IReadOnlyList<MacroWorkspace>> _getWorkspaces;
    private readonly Func<IReadOnlyList<MacroProfile>> _getProfiles;
    private readonly Func<int> _getActiveWorkspaceIndex;
    private readonly Func<MacroWorkspace, bool> _isWorkspaceRunning;
    private readonly Action<int> _activateWorkspace;
    private readonly Action<MacroWorkspace> _deleteWorkspace;
    private readonly Action<MacroWorkspace> _duplicateWorkspace;
    private readonly Action<MacroWorkspace, string> _moveWorkspaceToProfile;
    private readonly Action<int, int> _reorderWorkspace;
    private readonly Action<string> _showWarning;
    private readonly Action _scheduleSaveState;
    private readonly Action<bool> _setReorderNoticeVisible;
    private readonly FeatureGate _featureGate;
    private readonly Action<FeatureId> _showLockedFeature;

    private MacroWorkspace? _pendingDeleteWorkspace;
    private MacroWorkspace? _renamingWorkspace;

    private bool _isDraggingTabs;
    private bool _didDragTabs;
    private bool _isReorderModeEnabled;
    private bool _isReorderingTab;
    private Point _dragStartPoint;
    private double _dragStartOffset;
    private MacroWorkspace? _draggedTabWorkspace;
    private FrameworkElement? _dragGhost;
    private TranslateTransform? _dragGhostTransform;
    private Point _dragGhostCurrentPosition;
    private Point _dragGhostTargetPosition;
    private double _dragGhostWidth;
    private double _dragGhostHeight;
    private bool _isGhostAnimating;

    public WorkspaceTabController(
        Panel tabsPanel,
        ScrollViewer scrollViewer,
        Canvas dragOverlay,
        UIElement leftEdgeFade,
        UIElement rightEdgeFade,
        UIElement leftEdgeLine,
        UIElement rightEdgeLine,
        Dispatcher dispatcher,
        Func<IReadOnlyList<MacroWorkspace>> getWorkspaces,
        Func<IReadOnlyList<MacroProfile>> getProfiles,
        Func<int> getActiveWorkspaceIndex,
        Func<MacroWorkspace, bool> isWorkspaceRunning,
        Action<int> activateWorkspace,
        Action<MacroWorkspace> deleteWorkspace,
        Action<MacroWorkspace> duplicateWorkspace,
        Action<MacroWorkspace, string> moveWorkspaceToProfile,
        Action<int, int> reorderWorkspace,
        Action<string> showWarning,
        Action scheduleSaveState,
        Action<bool> setReorderNoticeVisible,
        FeatureGate featureGate,
        Action<FeatureId> showLockedFeature)
    {
        _tabsPanel = tabsPanel;
        _scrollViewer = scrollViewer;
        _dragOverlay = dragOverlay;
        _leftEdgeFade = leftEdgeFade;
        _rightEdgeFade = rightEdgeFade;
        _leftEdgeLine = leftEdgeLine;
        _rightEdgeLine = rightEdgeLine;
        _dispatcher = dispatcher;
        _getWorkspaces = getWorkspaces;
        _getProfiles = getProfiles;
        _getActiveWorkspaceIndex = getActiveWorkspaceIndex;
        _isWorkspaceRunning = isWorkspaceRunning;
        _activateWorkspace = activateWorkspace;
        _deleteWorkspace = deleteWorkspace;
        _duplicateWorkspace = duplicateWorkspace;
        _moveWorkspaceToProfile = moveWorkspaceToProfile;
        _reorderWorkspace = reorderWorkspace;
        _showWarning = showWarning;
        _scheduleSaveState = scheduleSaveState;
        _setReorderNoticeVisible = setReorderNoticeVisible;
        _featureGate = featureGate;
        _showLockedFeature = showLockedFeature;
    }

    public bool IsReorderModeEnabled => _isReorderModeEnabled;
    public bool HasPendingDelete => _pendingDeleteWorkspace != null;

    public void ClearTransientState()
    {
        _pendingDeleteWorkspace = null;
        _renamingWorkspace = null;
        DisableReorderMode();
    }

    public void DisableReorderMode()
    {
        if (!_isReorderModeEnabled)
            return;

        _isReorderModeEnabled = false;
        _setReorderNoticeVisible(false);
        EndDrag();
        Refresh();
    }

    public void CancelPendingDelete()
    {
        if (_pendingDeleteWorkspace == null)
            return;

        _pendingDeleteWorkspace = null;
        Refresh();
    }

    public bool IsSourcePendingDeleteTab(DependencyObject? source)
    {
        if (_pendingDeleteWorkspace == null)
            return false;

        return ReferenceEquals(TryGetSourceTabWorkspace(source), _pendingDeleteWorkspace);
    }

    public void Refresh()
    {
        var previousTabLefts = CaptureTabLefts();
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
            var isDragged = _isReorderingTab && ReferenceEquals(workspace, _draggedTabWorkspace);
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
                isDragged,
                isRunning,
                hasError));
        }

        _dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                AnimateTabsFromPreviousPositions(previousTabLefts);
                UpdateEdgeIndicators();
            }));
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

        if (_renamingWorkspace != null)
            return;

        _draggedTabWorkspace = TryGetSourceTabWorkspace(e.OriginalSource as DependencyObject);
        _isDraggingTabs = true;
        _didDragTabs = false;
        _dragStartPoint = e.GetPosition(_scrollViewer);
        _dragStartOffset = _scrollViewer.HorizontalOffset;
    }

    public void PreviewMouseMove(MouseEventArgs e)
    {
        if (!_isDraggingTabs || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (_renamingWorkspace != null)
        {
            EndDrag();
            return;
        }

        var currentPoint = e.GetPosition(_scrollViewer);
        var deltaX = currentPoint.X - _dragStartPoint.X;

        if (!_didDragTabs && Math.Abs(deltaX) < DragThreshold)
            return;

        _didDragTabs = true;

        if (!_scrollViewer.IsMouseCaptured)
            _scrollViewer.CaptureMouse();

        if (_draggedTabWorkspace != null && _isReorderModeEnabled)
        {
            _isReorderingTab = true;
            EnsureDragGhost();
            UpdateDragGhostTarget(currentPoint);
            AutoScrollDuringTabDrag(currentPoint);
            ReorderDraggedTab(e.GetPosition(_tabsPanel));
            e.Handled = true;
            return;
        }

        _scrollViewer.ScrollToHorizontalOffset(_dragStartOffset - deltaX);
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
        bool isDragged,
        bool isRunning,
        bool hasError)
    {
        var grid = new Grid
        {
            Margin = new Thickness(index == 0 ? 0 : GetTabSpacing(), 0, 0, 0),
            Opacity = isDragged ? 0.72 : 1.0,
            Tag = workspace
        };

        var button = CreateTabButton(workspace, isActive, isPendingDelete, isRunning, hasError);

        button.Click += (_, _) =>
        {
            if (_didDragTabs)
            {
                _didDragTabs = false;
                return;
            }

            if (ReferenceEquals(_pendingDeleteWorkspace, workspace))
            {
                BeginOrConfirmDelete(workspace);
                return;
            }

            _activateWorkspace(index);
        };
        button.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            BeginOrConfirmDelete(workspace);
            e.Handled = true;
        };
        button.ContextMenu = CreateWorkspaceContextMenu(workspace);

        grid.Children.Add(button);

        if (isPendingDelete)
        {
            button.Padding = new Thickness(10, 0, 10, 1);
        }
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
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(isPendingDelete
                ? Color.FromRgb(127, 29, 29)
                : hasError
                ? Color.FromRgb(97, 99, 09)
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
                ? Color.FromRgb(188, 183, 23)
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
                ? Color.FromRgb(254, 252, 202)
                : isRunning && isActive
                ? Color.FromRgb(236, 253, 245)
                : isRunning
                ? Color.FromRgb(167, 243, 208)
                : isActive
                ? Color.FromRgb(186, 230, 253)
                : Color.FromRgb(142, 160, 182)),
            Tag = workspace,
            ToolTip = isPendingDelete
                ? "Left-click or middle-click to confirm delete."
                : hasError
                ? workspace.ErrorMessage
                : isRunning
                    ? TooltipNotes.MacroTabRunning
                    : TooltipNotes.MacroTabSwitchRenameDelete,
            Content = isPendingDelete ? $"{workspace.Name} - Confirm" : workspace.Name
        };
    }

    private ContextMenu CreateWorkspaceContextMenu(MacroWorkspace workspace)
    {
        var contextMenu = new ContextMenu();
        contextMenu.SetResourceReference(FrameworkElement.StyleProperty, "KeyLineContextMenu");

        if (_featureGate.IsVisible(FeatureId.Profiles))
        {
            var moveToProfileItem = new MenuItem
            {
                Header = _featureGate.IsEnabled(FeatureId.Profiles)
                    ? "Move to profile"
                    : "Move to profile (locked)"
            };

            if (_featureGate.IsEnabled(FeatureId.Profiles))
            {
                foreach (var (profileId, profileName) in GetProfileMenuOptions())
                {
                    var targetProfileId = profileId;
                    var profileItem = new MenuItem
                    {
                        Header = profileName,
                        IsEnabled = !string.Equals(
                            MacroProfile.NormalizeId(workspace.ProfileId),
                            targetProfileId,
                            StringComparison.OrdinalIgnoreCase)
                    };
                    profileItem.Click += (_, _) => _moveWorkspaceToProfile(workspace, targetProfileId);
                    moveToProfileItem.Items.Add(profileItem);
                }
            }
            else
            {
                moveToProfileItem.Click += (_, _) => _showLockedFeature(FeatureId.Profiles);
            }

            contextMenu.Items.Add(moveToProfileItem);
            contextMenu.Items.Add(new Separator());
        }

        var duplicateItem = new MenuItem
        {
            Header = "Duplicate"
        };
        duplicateItem.Click += (_, _) => _duplicateWorkspace(workspace);

        var renameItem = new MenuItem
        {
            Header = "Rename"
        };
        renameItem.Click += (_, _) => BeginRename(workspace);

        var deleteItem = new MenuItem
        {
            Header = "Delete"
        };
        deleteItem.Click += (_, _) => BeginOrConfirmDelete(workspace);

        var reorderItem = new MenuItem
        {
            Header = "Reorder",
            IsEnabled = _getWorkspaces().Count > 1
        };
        reorderItem.Click += (_, _) => BeginReorderMode();

        contextMenu.Items.Add(duplicateItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(reorderItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(renameItem);
        contextMenu.Items.Add(deleteItem);

        return contextMenu;
    }

    private IEnumerable<(string Id, string Name)> GetProfileMenuOptions()
    {
        yield return (MacroProfile.NoProfileId, MacroProfile.NoProfileName);

        foreach (var profile in _getProfiles())
            yield return (MacroProfile.NormalizeId(profile.Id), profile.Name);
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
            Margin = new Thickness(index == 0 ? 0 : GetTabSpacing(), 0, 0, 0),
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

    private void BeginReorderMode()
    {
        if (_getWorkspaces().Count <= 1)
            return;

        _pendingDeleteWorkspace = null;
        _renamingWorkspace = null;
        _isReorderModeEnabled = true;
        _setReorderNoticeVisible(true);
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
        var shouldRefresh = _isReorderingTab;
        _isDraggingTabs = false;
        _isReorderingTab = false;
        _draggedTabWorkspace = null;

        if (_scrollViewer.IsMouseCaptured)
            _scrollViewer.ReleaseMouseCapture();

        EndDragGhost();

        if (shouldRefresh)
            _dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Refresh));

        if (_didDragTabs)
            _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => _didDragTabs = false));
    }

    private void ReorderDraggedTab(Point panelPoint)
    {
        if (_draggedTabWorkspace == null)
            return;

        var workspaces = _getWorkspaces();
        var sourceIndex = IndexOfWorkspace(workspaces, _draggedTabWorkspace);
        if (sourceIndex < 0)
            return;

        var insertionIndex = GetInsertionIndex(panelPoint.X);
        var targetIndex = sourceIndex < insertionIndex
            ? insertionIndex - 1
            : insertionIndex;

        targetIndex = Math.Clamp(targetIndex, 0, workspaces.Count - 1);
        if (targetIndex == sourceIndex)
            return;

        _reorderWorkspace(sourceIndex, targetIndex);
    }

    private int GetInsertionIndex(double panelX)
    {
        for (var i = 0; i < _tabsPanel.Children.Count; i++)
        {
            if (_tabsPanel.Children[i] is not FrameworkElement child)
                continue;

            var childMidpoint = child.TranslatePoint(
                new Point(child.ActualWidth / 2, 0),
                _tabsPanel).X;

            if (panelX < childMidpoint)
                return i;
        }

        return _tabsPanel.Children.Count;
    }

    private void AutoScrollDuringTabDrag(Point point)
    {
        const double edgeWidth = 34;
        const double scrollAmount = 18;

        if (_scrollViewer.ScrollableWidth <= 0)
            return;

        if (point.X < edgeWidth)
        {
            _scrollViewer.ScrollToHorizontalOffset(_scrollViewer.HorizontalOffset - scrollAmount);
            return;
        }

        if (point.X > _scrollViewer.ViewportWidth - edgeWidth)
            _scrollViewer.ScrollToHorizontalOffset(_scrollViewer.HorizontalOffset + scrollAmount);
    }

    private void EnsureDragGhost()
    {
        if (_dragGhost != null || _draggedTabWorkspace == null)
            return;

        var workspaces = _getWorkspaces();
        var activeIndex = _getActiveWorkspaceIndex();
        var index = IndexOfWorkspace(workspaces, _draggedTabWorkspace);
        if (index < 0)
            return;

        var workspace = workspaces[index];
        var ghostButton = CreateTabButton(
            workspace,
            index == activeIndex,
            false,
            _isWorkspaceRunning(workspace),
            !string.IsNullOrWhiteSpace(workspace.ErrorMessage));

        ghostButton.IsHitTestVisible = false;
        ghostButton.Opacity = GhostOpacity;
        ghostButton.RenderTransformOrigin = new Point(0.5, 0.5);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(GhostScale, GhostScale));
        _dragGhostTransform = new TranslateTransform();
        transformGroup.Children.Add(_dragGhostTransform);
        ghostButton.RenderTransform = transformGroup;

        _dragGhostWidth = GetDraggedTabWidth();
        _dragGhostHeight = GetDraggedTabHeight();
        if (_dragGhostWidth > 0)
            ghostButton.Width = _dragGhostWidth;

        _dragGhost = ghostButton;
        _dragOverlay.Children.Add(ghostButton);
        Canvas.SetLeft(ghostButton, 0);
        Canvas.SetTop(ghostButton, 0);
        Panel.SetZIndex(ghostButton, 1000);

        UpdateDragGhostTarget(_dragStartPoint, snap: true);
        StartDragGhostAnimation();
    }

    private void UpdateDragGhostTarget(Point scrollViewerPoint, bool snap = false)
    {
        if (_dragGhost == null)
            return;

        var overlayPoint = _scrollViewer.TranslatePoint(scrollViewerPoint, _dragOverlay);
        _dragGhostTargetPosition = new Point(
            overlayPoint.X - (_dragGhostWidth / 2.0),
            Math.Max(1, (_dragOverlay.ActualHeight - _dragGhostHeight) / 2.0));

        if (!snap)
            return;

        _dragGhostCurrentPosition = _dragGhostTargetPosition;
        ApplyDragGhostPosition();
    }

    private void StartDragGhostAnimation()
    {
        if (_isGhostAnimating)
            return;

        _isGhostAnimating = true;
        CompositionTarget.Rendering += DragGhost_Rendering;
    }

    private void StopDragGhostAnimation()
    {
        if (!_isGhostAnimating)
            return;

        _isGhostAnimating = false;
        CompositionTarget.Rendering -= DragGhost_Rendering;
    }

    private void DragGhost_Rendering(object? sender, EventArgs e)
    {
        if (_dragGhostTransform == null)
            return;

        var dx = _dragGhostTargetPosition.X - _dragGhostCurrentPosition.X;
        var dy = _dragGhostTargetPosition.Y - _dragGhostCurrentPosition.Y;

        if (Math.Abs(dx) < 0.2 && Math.Abs(dy) < 0.2)
        {
            _dragGhostCurrentPosition = _dragGhostTargetPosition;
        }
        else
        {
            _dragGhostCurrentPosition = new Point(
                _dragGhostCurrentPosition.X + (dx * GhostFollowStrength),
                _dragGhostCurrentPosition.Y + (dy * GhostFollowStrength));
        }

        ApplyDragGhostPosition();
    }

    private void ApplyDragGhostPosition()
    {
        if (_dragGhostTransform == null)
            return;

        _dragGhostTransform.X = _dragGhostCurrentPosition.X;
        _dragGhostTransform.Y = _dragGhostCurrentPosition.Y;
    }

    private void EndDragGhost()
    {
        StopDragGhostAnimation();

        if (_dragGhost != null)
            _dragOverlay.Children.Remove(_dragGhost);

        _dragGhost = null;
        _dragGhostTransform = null;
        _dragGhostCurrentPosition = default;
        _dragGhostTargetPosition = default;
        _dragGhostWidth = 0;
        _dragGhostHeight = 0;
    }

    private Dictionary<MacroWorkspace, double> CaptureTabLefts()
    {
        var result = new Dictionary<MacroWorkspace, double>();

        foreach (var child in _tabsPanel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not MacroWorkspace workspace)
                continue;

            result[workspace] = child.TranslatePoint(new Point(0, 0), _tabsPanel).X;
        }

        return result;
    }

    private void AnimateTabsFromPreviousPositions(IReadOnlyDictionary<MacroWorkspace, double> previousTabLefts)
    {
        if (!_isReorderingTab || previousTabLefts.Count == 0)
            return;

        foreach (var child in _tabsPanel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not MacroWorkspace workspace)
                continue;

            if (ReferenceEquals(workspace, _draggedTabWorkspace))
                continue;

            if (!previousTabLefts.TryGetValue(workspace, out var previousLeft))
                continue;

            var currentLeft = child.TranslatePoint(new Point(0, 0), _tabsPanel).X;
            var deltaX = previousLeft - currentLeft;
            if (Math.Abs(deltaX) < 0.5)
                continue;

            AnimateTabOffsetToRest(child, deltaX);
        }
    }

    private static void AnimateTabOffsetToRest(UIElement element, double deltaX)
    {
        var transform = new TranslateTransform(deltaX, 0);
        element.RenderTransform = transform;

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation
            {
                From = deltaX,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(135)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private double GetDraggedTabWidth()
    {
        if (_draggedTabWorkspace == null)
            return 92;

        return _tabsPanel.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, _draggedTabWorkspace))
            ?.ActualWidth ?? 92;
    }

    private double GetDraggedTabHeight()
    {
        if (_draggedTabWorkspace == null)
            return 22;

        return _tabsPanel.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, _draggedTabWorkspace))
            ?.ActualHeight ?? 22;
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

    private double GetTabSpacing()
    {
        return _isReorderModeEnabled ? ReorderModeTabSpacing : TabSpacing;
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

    private static MacroWorkspace? TryGetSourceTabWorkspace(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { Tag: MacroWorkspace workspace })
                return workspace;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static int IndexOfWorkspace(IReadOnlyList<MacroWorkspace> workspaces, MacroWorkspace workspace)
    {
        for (var i = 0; i < workspaces.Count; i++)
        {
            if (ReferenceEquals(workspaces[i], workspace))
                return i;
        }

        return -1;
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
