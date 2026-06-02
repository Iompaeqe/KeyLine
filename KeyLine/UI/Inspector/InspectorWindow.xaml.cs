using System.Windows;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow : Window
{
    public event Action? TimelineHeaderClicked;
    public event Action? NodeHeaderClicked;
    public event Action? TimelineNameEditStarted;
    public event Action<string>? TimelineNameCommitted;
    public event Action? TimelineNameEditCancelled;
    public event Action<int>? TimelineLoopsCommitted;
    public event Action<int>? TimelineLoopDelayCommitted;
    public event Action? TimelineInputTypeChangeRequested;
    public event Action<bool>? TimelineStandardDelayChanged;
    public event Action<int>? TimelineStandardDelayCommitted;
    public event Action<bool>? TimelineShowKeyUpDownChanged;

    public InspectorWindow()
    {
        InitializeComponent();
        ShowActivated = false;
        SizeChanged += (_, _) => UpdateScrollBarVisibility();
        WireStaticInspectorEvents();
    }
}