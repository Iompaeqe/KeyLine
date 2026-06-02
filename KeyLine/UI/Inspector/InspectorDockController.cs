using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.State;

namespace KeyLine.UI.Inspector;

public sealed class InspectorDockController
{
    private readonly Window _owner;
    private readonly TimelineSelectionState _selection;
    private readonly Func<MacroTimeline> _getCurrentTimeline;
    private readonly Func<bool> _canEdit;
    private readonly Action _saveUndoSnapshot;
    private readonly Action _refreshTimeline;
    private readonly Action _scheduleSaveState;
    private readonly Action<MacroTimeline> _selectTimeline;
    private readonly Func<MacroNode, Task> _pickMouseCoordinatesForNodeAsync;

    private InspectorWindow? _window;
    private InspectorController? _controller;

    private bool _isRequestedOpen;
    private bool _isAnimating;

    public InspectorDockController(
        Window owner,
        TimelineSelectionState selection,
        Func<MacroTimeline> getCurrentTimeline,
        Func<bool> canEdit,
        Action saveUndoSnapshot,
        Action refreshTimeline,
        Action scheduleSaveState,
        Action<MacroTimeline> selectTimeline,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync)
    {
        _owner = owner;
        _selection = selection;
        _getCurrentTimeline = getCurrentTimeline;
        _canEdit = canEdit;
        _saveUndoSnapshot = saveUndoSnapshot;
        _refreshTimeline = refreshTimeline;
        _scheduleSaveState = scheduleSaveState;
        _selectTimeline = selectTimeline;
        _pickMouseCoordinatesForNodeAsync = pickMouseCoordinatesForNodeAsync;

        _owner.Activated += Owner_Activated;
        _owner.LocationChanged += Owner_LocationChanged;
        _owner.SizeChanged += Owner_SizeChanged;
    }

    public void Refresh()
    {
        _controller?.Refresh();
    }

    public void Toggle()
    {
        EnsureWindow();

        if (_window!.IsVisible)
            Close(animate: true);
        else
            Open();
    }

    public void OpenFromSelection()
    {
        EnsureWindow();

        if (_window!.IsVisible)
        {
            Refresh();
            UpdatePosition();
            return;
        }

        Open();
    }

    public void Open()
    {
        EnsureWindow();

        _isRequestedOpen = true;
        _window!.BeginAnimation(Window.LeftProperty, null);
        _isAnimating = false;

        Refresh();
        PreparePosition(closed: true);

        _window.Opacity = 1;
        _window.Show();

        EnforceZOrder();

        _owner.Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (_window == null || !_window.IsVisible)
                    return;

                EnforceZOrder();

                AnimateTo(
                    GetOpenLeft(),
                    TimeSpan.FromMilliseconds(340),
                    EasingMode.EaseOut,
                    hideWhenDone: false);
            }));
    }

    public void Close(bool animate = true)
    {
        if (_window == null || !_window.IsVisible)
            return;

        _isRequestedOpen = false;

        if (!animate)
        {
            _window.Hide();
            return;
        }

        AnimateTo(
            GetClosedLeft(),
            TimeSpan.FromMilliseconds(240),
            EasingMode.EaseIn,
            hideWhenDone: true);
    }

    public void Hide()
    {
        _window?.Hide();
    }

    public void Shutdown()
    {
        _owner.Activated -= Owner_Activated;
        _owner.LocationChanged -= Owner_LocationChanged;
        _owner.SizeChanged -= Owner_SizeChanged;

        _window?.Close();
        _window = null;
        _controller = null;

        _isRequestedOpen = false;
        _isAnimating = false;
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = new InspectorWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false,
            Opacity = 0
        };

        new WindowInteropHelper(_window).EnsureHandle();

        _controller = new InspectorController(
            window: _window,
            selection: _selection,
            getCurrentTimeline: _getCurrentTimeline,
            canEdit: _canEdit,
            saveUndoSnapshot: _saveUndoSnapshot,
            refreshTimeline: _refreshTimeline,
            scheduleSaveState: _scheduleSaveState,
            selectTimeline: _selectTimeline,
            pickMouseCoordinatesForNodeAsync: _pickMouseCoordinatesForNodeAsync);

        Refresh();
    }

    private void Owner_LocationChanged(object? sender, EventArgs e)
    {
        if (_isAnimating)
        {
            _isAnimating = false;
            _window?.BeginAnimation(Window.LeftProperty, null);
        }

        UpdatePosition();
    }

    private void Owner_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePosition();
    }

    private void Owner_Activated(object? sender, EventArgs e)
    {
        if (_window == null || !_isRequestedOpen || _owner.WindowState == WindowState.Minimized)
            return;

        if (!_window.IsVisible)
            _window.Show();

        _owner.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                UpdatePosition();
                EnforceZOrder();
            }));
    }

    private void UpdatePosition(bool closed = false)
    {
        if (_window == null || !_window.IsVisible)
            return;

        if (_owner.WindowState == WindowState.Minimized)
        {
            _window.Hide();
            return;
        }

        var targetHeight = Math.Max(100, _owner.ActualHeight - 48);

        _window.Height = targetHeight;
        _window.Top = _owner.Top + (_owner.ActualHeight - targetHeight) / 2;
        _window.Left = closed ? GetClosedLeft() : GetOpenLeft();

        EnforceZOrder();
    }

    private void PreparePosition(bool closed)
    {
        if (_window == null)
            return;

        var targetHeight = Math.Max(100, _owner.ActualHeight - 48);

        _window.Height = targetHeight;
        _window.Top = _owner.Top + (_owner.ActualHeight - targetHeight) / 2;
        _window.Left = closed ? GetClosedLeft() : GetOpenLeft();
    }

    private double GetOpenLeft()
    {
        return _owner.Left + _owner.ActualWidth - 8;
    }

    private double GetClosedLeft()
    {
        var inspectorWidth = _window?.Width ?? 208;
        return _owner.Left + _owner.ActualWidth - inspectorWidth - 2;
    }

    private void AnimateTo(
        double targetLeft,
        TimeSpan duration,
        EasingMode easingMode,
        bool hideWhenDone)
    {
        if (_window == null)
            return;

        var animation = new DoubleAnimation
        {
            To = targetLeft,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };

        _isAnimating = true;

        animation.Completed += (_, _) =>
        {
            _isAnimating = false;

            if (_window == null)
                return;

            _window.BeginAnimation(Window.LeftProperty, null);
            _window.Left = targetLeft;

            if (hideWhenDone)
                _window.Hide();
            else
                EnforceZOrder();
        };

        _window.BeginAnimation(Window.LeftProperty, animation);
        EnforceZOrderWhileAnimating();
    }

    private void EnforceZOrderWhileAnimating()
    {
        var animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var ticks = 0;

        animationTimer.Tick += (_, _) =>
        {
            EnforceZOrder();
            ticks++;

            if (ticks > 30 || !_isAnimating)
                animationTimer.Stop();
        };

        animationTimer.Start();
    }

    private void EnforceZOrder()
    {
        if (_window == null || !_window.IsVisible)
            return;

        var helper = new WindowInteropHelper(_window);
        var ownerHelper = new WindowInteropHelper(_owner);

        if (helper.Handle == IntPtr.Zero || ownerHelper.Handle == IntPtr.Zero)
            return;

        SetWindowPos(
            helper.Handle,
            ownerHelper.Handle,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
}