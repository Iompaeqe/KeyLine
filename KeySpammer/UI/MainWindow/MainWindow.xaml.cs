using System.Windows;
using KeySpammer.Domain;
using KeySpammer.Services.Macro;
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

    public MainWindow()
    {
        InitializeComponent();
        LoadWindows();
        SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
    }
}