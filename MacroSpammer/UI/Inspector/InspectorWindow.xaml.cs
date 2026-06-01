using System.Windows;

namespace MacroSpammer.UI.Inspector;

public partial class InspectorWindow : Window
{
    private bool _isSettingTimelineState;
    private bool _isEditingEnabled = true;
    private bool _isTimelineNameEditing;
    private string _timelineName = string.Empty;
    private int _loopDelayMs;
    private int _standardDelayMs;
    private int _loopCount;

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

    public void SetTimelineState(
        string timelineName,
        bool isNameEditing,
        bool isCollapsed,
        bool isEditingEnabled,
        int loopCount,
        int loopDelayMs,
        bool useTextInputMode,
        bool useStandardDelay,
        int standardDelayMs,
        bool showKeyUpDown)
    {
        _isSettingTimelineState = true;
        _isEditingEnabled = isEditingEnabled;
        _loopCount = Math.Max(0, loopCount);
        _loopDelayMs = Math.Max(0, loopDelayMs);
        _standardDelayMs = Math.Max(0, standardDelayMs);

        TimelineHeader.ArrowText = isCollapsed ? "▶" : "▼";
        TimelineContentPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        TimelineHeader.Margin = new Thickness(0, 0, 0, isCollapsed ? 0 : 4);

        _timelineName = timelineName;
        SetTimelineNameDisplay(timelineName);

        if (!_isTimelineNameEditing)
        {
            TimelineNameReadOnlyHost.Visibility = Visibility.Visible;
            TimelineNameEditTextBox.Visibility = Visibility.Collapsed;
            TimelineNameEditTextBox.Text = timelineName;
        }

        TimelineLoopsEntry.TextBox.Text = _loopCount.ToString();
        TimelineLoopDelayEntry.SetDisplay(_loopDelayMs);
        InputTypePager.Text = useTextInputMode ? "Text" : "Key";
        TimelineStandardDelayCheckBox.IsChecked = useStandardDelay;
        StandardDelayDetailsPanel.Visibility = useStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        TimelineStandardDelayEntry.SetDisplay(_standardDelayMs);
        TimelineShowKeyUpDownCheckBox.IsChecked = showKeyUpDown;

        SetTimelineControlsEnabled(isEditingEnabled);
        _isSettingTimelineState = false;

        RequestScrollVisibilityUpdate();
    }

    public void SetNodeContent(UIElement? content, bool hasContent, bool isCollapsed)
    {
        NodeSectionHost.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
        NodeHeader.ArrowText = isCollapsed ? "▶" : "▼";
        NodeContentPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        NodeHeader.Margin = new Thickness(0, 0, 0, isCollapsed ? 0 : 4);
        NodeContentPanel.Children.Clear();

        if (content != null)
            NodeContentPanel.Children.Add(content);

        RequestScrollVisibilityUpdate();
    }

    private void SetTimelineControlsEnabled(bool isEditingEnabled)
    {
        TimelineNameEditIcon.Opacity = isEditingEnabled ? 0.65 : 0.35;
        TimelineNameEditIcon.IsEnabled = isEditingEnabled;
        TimelineNameEditTextBox.IsEnabled = isEditingEnabled;
        TimelineLoopsEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineLoopDelayEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineStandardDelayCheckBox.IsEnabled = isEditingEnabled;
        TimelineStandardDelayEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineShowKeyUpDownCheckBox.IsEnabled = isEditingEnabled;
        InputTypePager.IsEnabled = isEditingEnabled;
    }
}
