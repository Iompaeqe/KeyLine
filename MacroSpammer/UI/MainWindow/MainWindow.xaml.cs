using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Recording;
using MacroSpammer.State;

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
    private bool _isSyncingOptions;
    private bool _isSwitchingWorkspace;
    private bool _isRestoringWindowSelection;
    private MacroWorkspace? _pendingDeleteWorkspace;
    private MacroWorkspace? _renamingWorkspace;
    private bool _isDraggingMacroTabs;
    private bool _didDragMacroTabs;
    private bool _isTimelineEditingEnabled = true;
    private bool _shortcutsEnabled = true;
    private Point _macroTabsDragStartPoint;
    private double _macroTabsDragStartOffset;

    private MacroTimeline? _pendingClearTimeline;
    private bool _isClearConfirmationActive;
    private MacroTimeline? _pendingDeleteTimeline;

    private bool _didInitialTimelineRefresh;

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

        InitializeStatePersistence();
        ApplyShortcutHookState();
        ActivateWorkspace(_activeWorkspaceIndex, false);

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;

        if (_settings.StartMinimized)
            WindowState = WindowState.Minimized;
    }

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
}
