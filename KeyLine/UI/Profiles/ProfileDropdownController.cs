using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Profiles;

public sealed class ProfileDropdownController
{
    private const double DragThreshold = 4;
    private const double WheelScrollAmount = 36;
    private const double GhostOpacity = 0.86;
    private const double GhostScale = 1.03;
    private const double GhostFollowStrength = 0.65;

    private readonly Button _selectorButton;
    private readonly TextBlock _selectedProfileText;
    private readonly Popup _popup;
    private readonly ScrollViewer _scrollViewer;
    private readonly Panel _profilesPanel;
    private readonly Canvas _dragOverlay;
    private readonly Button _addButton;
    private readonly Dispatcher _dispatcher;

    private readonly Func<IReadOnlyList<MacroProfile>> _getProfiles;
    private readonly Func<string> _getActiveProfileId;
    private readonly Func<MacroProfile?> _addProfile;
    private readonly Action<string> _activateProfile;
    private readonly Action<MacroProfile, string> _renameProfile;
    private readonly Action<MacroProfile> _deleteProfile;
    private readonly Action<int, int> _reorderProfile;
    private readonly FeatureGate _featureGate;
    private readonly Action<FeatureId> _showLockedFeature;

    private MacroProfile? _pendingDeleteProfile;
    private MacroProfile? _renamingProfile;
    private bool _isAddingProfile;

    private bool _isDraggingProfiles;
    private bool _didDragProfiles;
    private bool _isReorderingProfile;
    private Point _dragStartPoint;
    private MacroProfile? _draggedProfile;
    private FrameworkElement? _dragGhost;
    private TranslateTransform? _dragGhostTransform;
    private Point _dragGhostCurrentPosition;
    private Point _dragGhostTargetPosition;
    private double _dragGhostWidth;
    private double _dragGhostHeight;
    private bool _isGhostAnimating;

    public ProfileDropdownController(
        Button selectorButton,
        TextBlock selectedProfileText,
        Popup popup,
        ScrollViewer scrollViewer,
        Panel profilesPanel,
        Canvas dragOverlay,
        Button addButton,
        Dispatcher dispatcher,
        Func<IReadOnlyList<MacroProfile>> getProfiles,
        Func<string> getActiveProfileId,
        Func<MacroProfile?> addProfile,
        Action<string> activateProfile,
        Action<MacroProfile, string> renameProfile,
        Action<MacroProfile> deleteProfile,
        Action<int, int> reorderProfile,
        FeatureGate featureGate,
        Action<FeatureId> showLockedFeature)
    {
        _selectorButton = selectorButton;
        _selectedProfileText = selectedProfileText;
        _popup = popup;
        _scrollViewer = scrollViewer;
        _profilesPanel = profilesPanel;
        _dragOverlay = dragOverlay;
        _addButton = addButton;
        _dispatcher = dispatcher;
        _getProfiles = getProfiles;
        _getActiveProfileId = getActiveProfileId;
        _addProfile = addProfile;
        _activateProfile = activateProfile;
        _renameProfile = renameProfile;
        _deleteProfile = deleteProfile;
        _reorderProfile = reorderProfile;
        _featureGate = featureGate;
        _showLockedFeature = showLockedFeature;

        WireEvents();
    }

    public bool HasPendingDelete => _pendingDeleteProfile != null;

    public void Refresh()
    {
        ApplyFeatureState();
        _selectedProfileText.Text = GetSelectedProfileName();

        var previousRowTops = CaptureRowTops();
        _profilesPanel.Children.Clear();

        AddProfileRow(
            id: MacroProfile.NoProfileId,
            name: MacroProfile.NoProfileName,
            profile: null,
            index: 0,
            isActive: MacroProfile.IsNoProfile(_getActiveProfileId()),
            isPendingDelete: false,
            isDragged: false);

        var profiles = _getProfiles();
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var isRenaming = ReferenceEquals(profile, _renamingProfile);
            var isPendingDelete = ReferenceEquals(profile, _pendingDeleteProfile);
            var isDragged = _isReorderingProfile && ReferenceEquals(profile, _draggedProfile);

            if (isRenaming)
            {
                _profilesPanel.Children.Add(CreateRenameTextBox(profile, i + 1));
                continue;
            }

            AddProfileRow(
                id: profile.Id,
                name: profile.Name,
                profile: profile,
                index: i + 1,
                isActive: string.Equals(
                    MacroProfile.NormalizeId(_getActiveProfileId()),
                    profile.Id,
                    StringComparison.OrdinalIgnoreCase),
                isPendingDelete: isPendingDelete,
                isDragged: isDragged);
        }

        _dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => AnimateRowsFromPreviousPositions(previousRowTops)));
    }

    public void ClearTransientState()
    {
        _pendingDeleteProfile = null;
        _renamingProfile = null;
        _isAddingProfile = false;
        EndDrag();
    }

    public void CancelPendingDelete()
    {
        if (_pendingDeleteProfile == null)
            return;

        _pendingDeleteProfile = null;
        _popup.StaysOpen = false;
        Refresh();
    }

    public bool IsSourcePendingDeleteProfile(DependencyObject? source)
    {
        if (_pendingDeleteProfile == null)
            return false;

        return ReferenceEquals(TryGetSourceProfile(source), _pendingDeleteProfile);
    }

    private void WireEvents()
    {
        _selectorButton.Click += (_, _) =>
        {
            if (!_featureGate.IsVisible(FeatureId.Profiles))
                return;

            if (!_featureGate.IsEnabled(FeatureId.Profiles))
            {
                _showLockedFeature(FeatureId.Profiles);
                return;
            }

            _popup.IsOpen = !_popup.IsOpen;
            if (_popup.IsOpen)
                Refresh();
        };

        _popup.Opened += (_, _) => Refresh();
        _popup.Closed += (_, _) =>
        {
            if (_renamingProfile == null)
            {
                _pendingDeleteProfile = null;
                _popup.StaysOpen = false;
                return;
            }

            CommitRename(_renamingProfile, _renamingProfile.Name);
        };

        _addButton.Click += (_, _) =>
        {
            if (!TryUseProfiles())
                return;

            var profile = _addProfile();
            if (profile == null)
                return;

            _renamingProfile = profile;
            _isAddingProfile = true;
            _popup.StaysOpen = true;
            _popup.IsOpen = true;
            Refresh();
        };

        _scrollViewer.PreviewMouseWheel += (_, e) =>
        {
            var direction = e.Delta > 0 ? -1 : 1;
            _scrollViewer.ScrollToVerticalOffset(
                _scrollViewer.VerticalOffset + (direction * WheelScrollAmount));
            e.Handled = true;
        };

        _scrollViewer.PreviewMouseLeftButtonDown += ScrollViewer_PreviewMouseLeftButtonDown;
        _scrollViewer.PreviewMouseMove += ScrollViewer_PreviewMouseMove;
        _scrollViewer.PreviewMouseLeftButtonUp += (_, _) => EndDrag();
        _scrollViewer.MouseLeave += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                EndDrag();
        };
    }

    private void AddProfileRow(
        string id,
        string name,
        MacroProfile? profile,
        int index,
        bool isActive,
        bool isPendingDelete,
        bool isDragged)
    {
        
        var grid = new Grid
        {
            Height = 30,
            Margin = new Thickness(0, index == 0 ? 0 : 4, 0, 0),
            Opacity = isDragged ? 0.72 : 1.0,
            Tag = profile != null ? profile : id
        };

        var row = CreateProfileRowElement(name, isActive, isPendingDelete, profile == null);
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (_renamingProfile != null)
                return;

            if (!TryUseProfiles())
            {
                e.Handled = true;
                return;
            }

            if (profile != null && ReferenceEquals(_pendingDeleteProfile, profile))
            {
                BeginOrConfirmDelete(profile!);
                e.Handled = true;
                return;
            }

            if (_didDragProfiles)
            {
                _didDragProfiles = false;
                return;
            }

            _activateProfile(id);
            _popup.IsOpen = false;
            e.Handled = true;
        };

        if (profile != null)
        {
            row.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount < 2)
                    return;

                BeginRename(profile);
                e.Handled = true;
            };

            row.ContextMenu = CreateProfileContextMenu(profile);
        }

        grid.Children.Add(row);
        _profilesPanel.Children.Add(grid);
    }

    private Border CreateProfileRowElement(string name, bool isActive, bool isPendingDelete, bool isNoProfile)
    {
        var normalBackground = isActive
            ? Color.FromRgb(30, 58, 95)
            : Color.FromArgb(0, 0, 0, 0);
        var hoverBackground = isActive
            ? Color.FromRgb(35, 72, 116)
            : Color.FromRgb(36, 50, 68);

        if (isPendingDelete)
        {
            normalBackground = Color.FromRgb(127, 29, 29);
            hoverBackground = Color.FromRgb(153, 27, 27);
        }

        var foreground = isPendingDelete
            ? Color.FromRgb(254, 202, 202)
            : isActive
            ? Color.FromRgb(189, 235, 255)
            : isNoProfile
                ? Color.FromRgb(148, 163, 184)
                : Color.FromRgb(226, 232, 240);

        var row = new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 0, 10, 1),
            Background = new SolidColorBrush(normalBackground),
            Cursor = Cursors.Hand,
            ToolTip = isPendingDelete
                ? "Left-click or middle-click to confirm delete."
                : null,
            Child = new TextBlock
            {
                Text = isPendingDelete ? $"{name} - Confirm" : name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(foreground),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(hoverBackground);
        row.MouseLeave += (_, _) => row.Background = new SolidColorBrush(normalBackground);

        return row;
    }

    private ContextMenu CreateProfileContextMenu(MacroProfile profile)
    {
        var contextMenu = new ContextMenu();
        contextMenu.SetResourceReference(FrameworkElement.StyleProperty, "KeyLineContextMenu");

        var editNameItem = new MenuItem
        {
            Header = _featureGate.IsEnabled(FeatureId.Profiles)
                ? "Edit name"
                : "Edit name (locked)"
        };
        editNameItem.Click += (_, _) => BeginRename(profile);

        var deleteItem = new MenuItem
        {
            Header = _featureGate.IsEnabled(FeatureId.Profiles)
                ? "Delete"
                : "Delete (locked)"
        };
        deleteItem.Click += (_, _) => BeginOrConfirmDelete(profile);

        contextMenu.Items.Add(editNameItem);
        contextMenu.Items.Add(deleteItem);

        return contextMenu;
    }

    private TextBox CreateRenameTextBox(MacroProfile profile, int index)
    {
        var textBox = new TextBox
        {
            Text = _isAddingProfile ? "" : profile.Name,
            Height = 30,
            Margin = new Thickness(0, index == 0 ? 0 : 4, 0, 0),
            Padding = new Thickness(3, 1, 3, 1),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(10, 52, 84)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
            Foreground = new SolidColorBrush(Color.FromRgb(224, 242, 254)),
            Tag = profile
        };

        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        textBox.LostKeyboardFocus += (_, e) =>
        {
            var focusStayedInPopup = IsSourceInsidePopup(e.NewFocus as DependencyObject);
            CommitRename(profile, textBox.Text, closePopup: !focusStayedInPopup);
        };
        textBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitRename(profile, textBox.Text);
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

    private void BeginRename(MacroProfile profile)
    {
        if (!TryUseProfiles())
            return;

        _pendingDeleteProfile = null;
        _renamingProfile = profile;
        _isAddingProfile = false;
        _popup.StaysOpen = true;
        Refresh();
    }

    private void BeginOrConfirmDelete(MacroProfile profile)
    {
        if (!TryUseProfiles())
            return;

        if (ReferenceEquals(_pendingDeleteProfile, profile))
        {
            _pendingDeleteProfile = null;
            _renamingProfile = null;
            _popup.StaysOpen = false;
            _deleteProfile(profile);
            return;
        }

        _pendingDeleteProfile = profile;
        _renamingProfile = null;
        _isAddingProfile = false;
        _popup.StaysOpen = false;
        _popup.IsOpen = true;
        Refresh();
    }

    private void CommitRename(MacroProfile profile, string name, bool closePopup = false)
    {
        if (!TryUseProfiles())
            return;

        if (!ReferenceEquals(_renamingProfile, profile))
            return;

        _renameProfile(profile, name);
        _renamingProfile = null;
        _isAddingProfile = false;
        _popup.StaysOpen = false;
        Refresh();

        if (closePopup)
            _popup.IsOpen = false;
    }

    private void CancelRename()
    {
        _renamingProfile = null;
        _isAddingProfile = false;
        _popup.StaysOpen = false;
        Refresh();
    }

    private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TryUseProfiles())
            return;

        if (IsSourceInsideTextBox(e.OriginalSource as DependencyObject))
            return;

        if (_renamingProfile != null)
            return;

        if (_pendingDeleteProfile != null)
            return;

        _draggedProfile = TryGetSourceProfile(e.OriginalSource as DependencyObject);
        if (_draggedProfile == null)
            return;

        _isDraggingProfiles = true;
        _didDragProfiles = false;
        _isReorderingProfile = false;
        _dragStartPoint = e.GetPosition(_scrollViewer);
    }

    private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_featureGate.IsEnabled(FeatureId.Profiles))
            return;

        if (!_isDraggingProfiles || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (_renamingProfile != null)
        {
            EndDrag();
            return;
        }

        var currentPoint = e.GetPosition(_scrollViewer);
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        if (!_didDragProfiles && Math.Abs(deltaY) < DragThreshold)
            return;

        _didDragProfiles = true;

        if (!_scrollViewer.IsMouseCaptured)
            _scrollViewer.CaptureMouse();

        _isReorderingProfile = true;
        EnsureDragGhost();
        UpdateDragGhostTarget(currentPoint);
        AutoScrollDuringProfileDrag(currentPoint);
        ReorderDraggedProfile(e.GetPosition(_profilesPanel));
        e.Handled = true;
    }

    private void ReorderDraggedProfile(Point panelPoint)
    {
        if (!_featureGate.IsEnabled(FeatureId.Profiles))
            return;

        if (_draggedProfile == null)
            return;

        var profiles = _getProfiles();
        var sourceIndex = IndexOfProfile(profiles, _draggedProfile);
        if (sourceIndex < 0)
            return;

        var insertionIndex = GetInsertionIndex(panelPoint.Y);
        var targetSlot = Math.Clamp(insertionIndex - 1, 0, profiles.Count);
        var targetIndex = sourceIndex < targetSlot
            ? targetSlot - 1
            : targetSlot;

        targetIndex = Math.Clamp(targetIndex, 0, profiles.Count - 1);
        if (targetIndex == sourceIndex)
            return;

        _reorderProfile(sourceIndex, targetIndex);
    }

    private int GetInsertionIndex(double panelY)
    {
        for (var i = 0; i < _profilesPanel.Children.Count; i++)
        {
            if (_profilesPanel.Children[i] is not FrameworkElement child)
                continue;

            var childMidpoint = child.TranslatePoint(
                new Point(0, child.ActualHeight / 2),
                _profilesPanel).Y;

            if (panelY < childMidpoint)
                return i;
        }

        return _profilesPanel.Children.Count;
    }

    private void AutoScrollDuringProfileDrag(Point point)
    {
        const double edgeHeight = 34;
        const double scrollAmount = 14;

        if (_scrollViewer.ScrollableHeight <= 0)
            return;

        if (point.Y < edgeHeight)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - scrollAmount);
            return;
        }

        if (point.Y > _scrollViewer.ViewportHeight - edgeHeight)
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + scrollAmount);
    }

    private void EndDrag()
    {
        var shouldRefresh = _isReorderingProfile;
        _isDraggingProfiles = false;
        _isReorderingProfile = false;
        _draggedProfile = null;

        if (_scrollViewer.IsMouseCaptured)
            _scrollViewer.ReleaseMouseCapture();

        EndDragGhost();

        if (shouldRefresh)
            _dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Refresh));

        if (_didDragProfiles)
            _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => _didDragProfiles = false));
    }

    private void EnsureDragGhost()
    {
        if (_dragGhost != null || _draggedProfile == null)
            return;

        var ghostRow = CreateProfileRowElement(
            _draggedProfile.Name,
            string.Equals(
                MacroProfile.NormalizeId(_getActiveProfileId()),
                _draggedProfile.Id,
                StringComparison.OrdinalIgnoreCase),
            isPendingDelete: false,
            isNoProfile: false);

        ghostRow.IsHitTestVisible = false;
        ghostRow.Opacity = GhostOpacity;
        ghostRow.RenderTransformOrigin = new Point(0.5, 0.5);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(GhostScale, GhostScale));
        _dragGhostTransform = new TranslateTransform();
        transformGroup.Children.Add(_dragGhostTransform);
        ghostRow.RenderTransform = transformGroup;

        _dragGhostWidth = GetDraggedProfileWidth();
        _dragGhostHeight = GetDraggedProfileHeight();
        if (_dragGhostWidth > 0)
            ghostRow.Width = _dragGhostWidth;

        _dragGhost = ghostRow;
        _dragOverlay.Children.Add(ghostRow);
        Canvas.SetLeft(ghostRow, 0);
        Canvas.SetTop(ghostRow, 0);
        Panel.SetZIndex(ghostRow, 1000);

        UpdateDragGhostTarget(_dragStartPoint, snap: true);
        StartDragGhostAnimation();
    }

    private void UpdateDragGhostTarget(Point scrollViewerPoint, bool snap = false)
    {
        if (_dragGhost == null)
            return;

        var overlayPoint = _scrollViewer.TranslatePoint(scrollViewerPoint, _dragOverlay);
        _dragGhostTargetPosition = new Point(
            Math.Max(0, (_dragOverlay.ActualWidth - _dragGhostWidth) / 2.0),
            overlayPoint.Y - (_dragGhostHeight / 2.0));

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

    private Dictionary<string, double> CaptureRowTops()
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var child in _profilesPanel.Children.OfType<FrameworkElement>())
        {
            var id = GetProfileIdFromTag(child.Tag);
            if (id == null)
                continue;

            result[id] = child.TranslatePoint(new Point(0, 0), _profilesPanel).Y;
        }

        return result;
    }

    private void AnimateRowsFromPreviousPositions(IReadOnlyDictionary<string, double> previousRowTops)
    {
        if (!_isReorderingProfile || previousRowTops.Count == 0)
            return;

        foreach (var child in _profilesPanel.Children.OfType<FrameworkElement>())
        {
            var id = GetProfileIdFromTag(child.Tag);
            if (id == null)
                continue;

            if (_draggedProfile != null &&
                string.Equals(id, _draggedProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!previousRowTops.TryGetValue(id, out var previousTop))
                continue;

            var currentTop = child.TranslatePoint(new Point(0, 0), _profilesPanel).Y;
            var deltaY = previousTop - currentTop;
            if (Math.Abs(deltaY) < 0.5)
                continue;

            TimelineAnimationService.AnimateRenderOffsetToRest(child, 0, deltaY, animateY: true);
        }
    }

    private double GetDraggedProfileWidth()
    {
        if (_draggedProfile == null)
            return 176;

        return _profilesPanel.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, _draggedProfile))
            ?.ActualWidth ?? 176;
    }

    private double GetDraggedProfileHeight()
    {
        if (_draggedProfile == null)
            return 30;

        return _profilesPanel.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, _draggedProfile))
            ?.ActualHeight ?? 30;
    }

    private string GetSelectedProfileName()
    {
        var activeProfileId = MacroProfile.NormalizeId(_getActiveProfileId());
        if (MacroProfile.IsNoProfile(activeProfileId))
            return MacroProfile.NoProfileName;

        return _getProfiles()
            .FirstOrDefault(profile => string.Equals(
                profile.Id,
                activeProfileId,
                StringComparison.OrdinalIgnoreCase))
            ?.Name ?? MacroProfile.NoProfileName;
    }

    private void ApplyFeatureState()
    {
        if (!_featureGate.IsVisible(FeatureId.Profiles))
        {
            _selectorButton.Visibility = Visibility.Collapsed;
            _popup.IsOpen = false;
            return;
        }

        _selectorButton.Visibility = Visibility.Visible;

        var isEnabled = _featureGate.IsEnabled(FeatureId.Profiles);
        _selectorButton.Opacity = isEnabled ? 1.0 : 0.55;
        _selectorButton.ToolTip = isEnabled ? null : _featureGate.GetLockedFeatureMessage(FeatureId.Profiles);
        _addButton.IsHitTestVisible = isEnabled;
        _addButton.Focusable = isEnabled;
        _addButton.Opacity = isEnabled ? 1.0 : 0.45;
        _addButton.ToolTip = isEnabled ? null : _featureGate.GetLockedFeatureMessage(FeatureId.Profiles);

        if (!isEnabled)
            _popup.IsOpen = false;
    }

    private bool TryUseProfiles()
    {
        if (_featureGate.IsEnabled(FeatureId.Profiles))
            return true;

        _showLockedFeature(FeatureId.Profiles);
        return false;
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

    private bool IsSourceInsidePopup(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, _scrollViewer) ||
                ReferenceEquals(source, _profilesPanel) ||
                ReferenceEquals(source, _addButton))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static MacroProfile? TryGetSourceProfile(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { Tag: MacroProfile profile })
                return profile;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static string? GetProfileIdFromTag(object? tag)
    {
        return tag switch
        {
            MacroProfile profile => profile.Id,
            string id => id,
            _ => null
        };
    }

    private static int IndexOfProfile(IReadOnlyList<MacroProfile> profiles, MacroProfile profile)
    {
        for (var i = 0; i < profiles.Count; i++)
        {
            if (ReferenceEquals(profiles[i], profile))
                return i;
        }

        return -1;
    }
}
