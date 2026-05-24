using System.Windows;
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

    private void RequestTimelineScrollIndicatorUpdate()
    {
        if (_timelineScrollIndicatorUpdateQueued)
            return;

        _timelineScrollIndicatorUpdateQueued = true;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                _timelineScrollIndicatorUpdateQueued = false;
                UpdateTimelineScrollIndicator();
            }));
    }
}
