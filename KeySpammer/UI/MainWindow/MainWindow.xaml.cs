using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeySpammer.Domain;
using KeySpammer.Services.Macro;
using KeySpammer.Services.Recording;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow : Window
{
    private readonly MacroDocument _document = new();
    private readonly Dictionary<MacroTimeline, MacroRunner> _runners = new();
    private readonly MacroRecorder _recorder = new();

    private readonly Dictionary<object, Point> _timelineVisualPositions = new();

    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    private MacroTimeline? _recordingTimeline;
    private MacroTimeline? _popupTimeline;
    private bool _isSyncingOptions;

    private MacroTimeline? _pendingClearTimeline;
    private bool _isClearConfirmationActive;

    private bool _didInitialTimelineRefresh;
    private bool _timelineScrollIndicatorUpdateQueued;

    public MainWindow()
    {
        InitializeComponent();

        LoadWindows();
        SelectTimeline(_document.ActiveTimeline);

        Loaded += MainWindow_Loaded;
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
