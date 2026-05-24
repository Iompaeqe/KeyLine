using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KeySpammer.Services.Playback;
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

    public MainWindow()
    {
        InitializeComponent();
        LoadWindows();
        SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
    }
}