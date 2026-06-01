using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Recording;
using MacroSpammer.State;
using MacroSpammer.UI.Inspector;

namespace MacroSpammer;

public partial class MainWindow : Window
{
    private readonly List<MacroWorkspace> _workspaces;
    private int _activeWorkspaceIndex;
    private MacroWorkspace _activeWorkspace;
    private MacroDocument _document;
    private readonly Dictionary<MacroTimeline, MacroRunner> _runners = new();
    private readonly MacroRecorder _recorder = new();
    private readonly AppSettings _settings;

    private readonly Dictionary<object, Point> _timelineVisualPositions = new();

    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    private MacroTimeline? _recordingTimeline;
    private MacroTimeline? _popupTimeline;
    private bool _isSwitchingWorkspace;
    private bool _isRestoringWindowSelection;
    private MacroWorkspace? _pendingDeleteWorkspace;
    private MacroWorkspace? _renamingWorkspace;
    private bool _isDraggingMacroTabs;
    private bool _didDragMacroTabs;
    private bool _isTimelineEditingEnabled = true;
    private bool _shortcutsEnabled = false;
    private Point _macroTabsDragStartPoint;
    private double _macroTabsDragStartOffset;

    private MacroTimeline? _pendingClearTimeline;
    private bool _isClearConfirmationActive;
    private MacroTimeline? _pendingDeleteTimeline;
    private bool _isShortcutClearConfirmationActive;

    private bool _didInitialTimelineRefresh;
    private InspectorWindow? _inspectorWindow;
    private bool _isInspectorRequestedOpen;

    public MainWindow()
    {
        var savedState = MacroStateStore.Load();
        _settings = savedState?.Settings ?? new AppSettings();
        _workspaces = savedState?.Workspaces.Count > 0
            ? savedState.Workspaces
            : new List<MacroWorkspace> { CreateWorkspace(1, _settings) };
        _activeWorkspaceIndex = savedState?.ActiveWorkspaceIndex ?? 0;
        _shortcutsEnabled = savedState?.ShortcutsEnabled ?? false;
        _activeWorkspaceIndex = Math.Clamp(_activeWorkspaceIndex, 0, _workspaces.Count - 1);
        _activeWorkspace = _workspaces[_activeWorkspaceIndex];
        _document = _activeWorkspace.Document;

        InitializeComponent();
        
        MacroTabsScrollViewer.PreviewMouseWheel += MacroTabsScrollViewer_PreviewMouseWheel;
        MacroTabsScrollViewer.PreviewMouseLeftButtonDown += MacroTabsScrollViewer_PreviewMouseLeftButtonDown;
        MacroTabsScrollViewer.PreviewMouseMove += MacroTabsScrollViewer_PreviewMouseMove;
        MacroTabsScrollViewer.PreviewMouseLeftButtonUp += MacroTabsScrollViewer_PreviewMouseLeftButtonUp;
        MacroTabsScrollViewer.MouseLeave += MacroTabsScrollViewer_MouseLeave;
        MacroTabsScrollViewer.ScrollChanged += MacroTabsScrollViewer_ScrollChanged;
        MacroTabsScrollViewer.SizeChanged += MacroTabsScrollViewer_SizeChanged;
        
        ToggleRightPanelButton.Click += ToggleRightPanelButton_Click;
        SettingsButton.Click += SettingsButton_Click;
        MinimizeButton.Click += MinimizeButton_Click;
        CloseWindowButton.Click += CloseButton_Click;

        AddMacroTabButton.Click += AddMacroTabButton_Click;
        
        ShortcutBorder.MouseRightButtonDown += ShortcutBorder_MouseRightButtonDown;
        ShortcutPill.MouseLeftButtonDown += ShortcutTextBlock_MouseLeftButtonDown;
        ShortcutPill.PreviewKeyDown += ShortcutTextBlock_PreviewKeyDown;
        ShortcutPill.PreviewKeyUp += ShortcutTextBlock_PreviewKeyUp;
        ShortcutPill.LostKeyboardFocus += ShortcutTextBlock_LostKeyboardFocus;
        ShortcutTogglePill.MouseLeftButtonDown += ShortcutToggleTextBlock_MouseLeftButtonDown;
        LoopTypePager.PageRequested += LoopTypePager_PageRequested;
        
        TargetWindowSearchPill.MouseLeftButtonDown += TargetWindowSearchPill_MouseLeftButtonDown;
        TargetWindowSearchTextBox.LostFocus += TargetWindowSearchTextBox_LostFocus;
        TargetWindowSearchTextBox.KeyDown += TargetWindowSearchTextBox_KeyDown;
        WindowComboBox.DropDownOpened += WindowComboBox_DropDownOpened;
        WindowComboBox.SelectionChanged += WindowComboBox_SelectionChanged;
        HandleComboBox.SelectionChanged += HandleComboBox_SelectionChanged;

        InitializeStatePersistence();
        ApplyShortcutHookState();
        ActivateWorkspace(_activeWorkspaceIndex, false);

        Loaded += MainWindow_Loaded;
        Activated += MainWindow_Activated;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;

        if (_settings.StartMinimized)
            WindowState = WindowState.Minimized;
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (_isInspectorAnimating)
        {
            _isInspectorAnimating = false;
            _inspectorWindow?.BeginAnimation(Window.LeftProperty, null);
        }

        UpdateInspectorPosition();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateInspectorPosition();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (_inspectorWindow == null || !_isInspectorRequestedOpen || WindowState == WindowState.Minimized)
            return;

        if (!_inspectorWindow.IsVisible)
            _inspectorWindow.Show();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                UpdateInspectorPosition();
                EnforceInspectorZOrder();
            }));
    }

    private bool _isInspectorAnimating;

    private void UpdateInspectorPosition(bool closed = false)
    {
        if (_inspectorWindow == null || !_inspectorWindow.IsVisible)
            return;

        if (WindowState == WindowState.Minimized)
        {
            _inspectorWindow.Hide();
            return;
        }

        var targetHeight = Math.Max(100, ActualHeight - 48);
        _inspectorWindow.Height = targetHeight;
        _inspectorWindow.Top = Top + (ActualHeight - targetHeight) / 2;
        _inspectorWindow.Left = closed ? GetInspectorClosedLeft() : GetInspectorOpenLeft();

        EnforceInspectorZOrder();
    }

    private void PrepareInspectorPosition(bool closed)
    {
        if (_inspectorWindow == null)
            return;

        var targetHeight = Math.Max(100, ActualHeight - 48);
        _inspectorWindow.Height = targetHeight;
        _inspectorWindow.Top = Top + (ActualHeight - targetHeight) / 2;
        _inspectorWindow.Left = closed ? GetInspectorClosedLeft() : GetInspectorOpenLeft();
    }

    private double GetInspectorOpenLeft() => Left + ActualWidth - 8;

    private double GetInspectorClosedLeft()
    {
        var inspectorWidth = _inspectorWindow?.Width ?? 208;
        return Left + ActualWidth - inspectorWidth - 2;
    }

    private void EnforceInspectorZOrder()
    {
        if (_inspectorWindow == null || !_inspectorWindow.IsVisible)
            return;

        var helper = new WindowInteropHelper(_inspectorWindow);
        var mainHelper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero && mainHelper.Handle != IntPtr.Zero)
        {
            SetWindowPos(helper.Handle, mainHelper.Handle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_didInitialTimelineRefresh)
            return;

        _didInitialTimelineRefresh = true;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                RefreshTimeline();

                Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() =>
                    {
                        RefreshTimeline();
                        UpdateTimelineScrollIndicator();
                    }));
            }));
    }
    
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;

    private const double HorizontalResizeBorderWidth = 8;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest)
            return IntPtr.Zero;

        var mouseScreenX = GetXLParam(lParam);
        var mouseScreenY = GetYLParam(lParam);

        var mouseWindowPos = PointFromScreen(new Point(mouseScreenX, mouseScreenY));

        if (mouseWindowPos.X <= HorizontalResizeBorderWidth)
        {
            handled = true;
            return new IntPtr(HtLeft);
        }

        if (mouseWindowPos.X >= ActualWidth - HorizontalResizeBorderWidth)
        {
            handled = true;
            return new IntPtr(HtRight);
        }

        return IntPtr.Zero;
    }

    private static int GetXLParam(IntPtr lParam)
    {
        return unchecked((short)(long)lParam);
    }

    private static int GetYLParam(IntPtr lParam)
    {
        return unchecked((short)((long)lParam >> 16));
    }

    private void LoopTypePager_PageRequested(object? sender, EventArgs e)
    {
        if (!_isTimelineEditingEnabled)
            return;

        LoopTypePager.Text = LoopTypePager.Text == "asynced" ? "synced" : "asynced";
        _activeWorkspace.LoopType = LoopTypePager.Text == "synced" ? MacroLoopType.Sync : MacroLoopType.Async;
        CaptureActiveWorkspaceState(); // Ensure all state is synced
        ScheduleSaveState();
    }

    private void ToggleRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_inspectorWindow == null)
            CreateInspectorWindow();

        if (_inspectorWindow!.IsVisible)
            CloseInspector(animate: true);
        else
            OpenInspector();
    }

    private void OpenInspectorFromSelection()
    {
        if (_inspectorWindow == null)
            CreateInspectorWindow();

        if (_inspectorWindow!.IsVisible)
        {
            RefreshInspector();
            UpdateInspectorPosition();
            return;
        }

        OpenInspector();
    }

    private void CreateInspectorWindow()
    {
        _inspectorWindow = new InspectorWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false,
            Opacity = 0
        };

        new WindowInteropHelper(_inspectorWindow).EnsureHandle();
        InitializeInspectorWindowEvents(_inspectorWindow);
        RefreshInspector();
    }

    private void OpenInspector()
    {
        if (_inspectorWindow == null)
            return;

        _isInspectorRequestedOpen = true;
        _inspectorWindow.BeginAnimation(Window.LeftProperty, null);
        _isInspectorAnimating = false;

        RefreshInspector();
        PrepareInspectorPosition(closed: true);
        _inspectorWindow.Opacity = 1;
        _inspectorWindow.Show();
        EnforceInspectorZOrder();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                if (_inspectorWindow == null || !_inspectorWindow.IsVisible)
                    return;

                EnforceInspectorZOrder();
                AnimateInspectorTo(GetInspectorOpenLeft(), TimeSpan.FromMilliseconds(340), System.Windows.Media.Animation.EasingMode.EaseOut, hideWhenDone: false);
            }));
    }

    private void CloseInspector(bool animate = true)
    {
        if (_inspectorWindow == null || !_inspectorWindow.IsVisible)
            return;

        _isInspectorRequestedOpen = false;
        if (!animate)
        {
            _inspectorWindow.Hide();
            return;
        }

        AnimateInspectorTo(GetInspectorClosedLeft(), TimeSpan.FromMilliseconds(240), System.Windows.Media.Animation.EasingMode.EaseIn, hideWhenDone: true);
    }

    private void AnimateInspectorTo(
        double targetLeft,
        TimeSpan duration,
        System.Windows.Media.Animation.EasingMode easingMode,
        bool hideWhenDone)
    {
        if (_inspectorWindow == null)
            return;

        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = targetLeft,
            Duration = duration,
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = easingMode }
        };

        _isInspectorAnimating = true;
        animation.Completed += (_, _) =>
        {
            _isInspectorAnimating = false;
            if (_inspectorWindow == null)
                return;

            _inspectorWindow.BeginAnimation(Window.LeftProperty, null);
            _inspectorWindow.Left = targetLeft;

            if (hideWhenDone)
                _inspectorWindow.Hide();
            else
                EnforceInspectorZOrder();
        };

        _inspectorWindow.BeginAnimation(Window.LeftProperty, animation);
        EnforceInspectorZOrderWhileAnimating();
    }

    private void EnforceInspectorZOrderWhileAnimating()
    {
        var animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var ticks = 0;

        animationTimer.Tick += (_, _) =>
        {
            EnforceInspectorZOrder();
            ticks++;

            if (ticks > 30 || !_isInspectorAnimating)
                animationTimer.Stop();
        };

        animationTimer.Start();
    }
}
