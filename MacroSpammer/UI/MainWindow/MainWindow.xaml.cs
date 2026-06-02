using System.Windows;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.Services.Playback;
using MacroSpammer.Services.Recording;
using MacroSpammer.Services.AppWindow;
using MacroSpammer.Services.Macro;
using MacroSpammer.State;

namespace MacroSpammer;

public partial class MainWindow : Window
{
    private readonly List<MacroWorkspace> _workspaces;
    private int _activeWorkspaceIndex;
    private MacroWorkspace _activeWorkspace;
    private MacroDocument _document;
    private readonly PlaybackController _playback = new();
    private readonly MacroRecorder _recorder = new();
    private readonly AppSettings _settings;

    private readonly Dictionary<object, Point> _timelineVisualPositions = new();

    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    private MacroTimeline? _recordingTimeline;
    private MacroTimeline? _popupTimeline;
    private bool _isSwitchingWorkspace;
    private bool _isRestoringWindowSelection;
    private bool _isTimelineEditingEnabled = true;
    private bool _shortcutsEnabled = false;

    private MacroTimeline? _pendingClearTimeline;
    private bool _isClearConfirmationActive;
    private MacroTimeline? _pendingDeleteTimeline;
    private bool _isShortcutClearConfirmationActive;

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
        InitializeWorkspaceTabs();
        InitializeSettingsModal();
        InitializeInspector();
        InitializeMacroOptions();
        InitializeStatePersistence();
        InitializeShortcuts();
        ApplyShortcutHookState();
        
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
        
        WindowComboBox.DropDownOpened += WindowComboBox_DropDownOpened;
        WindowComboBox.SelectionChanged += WindowComboBox_SelectionChanged;
        HandleComboBox.SelectionChanged += HandleComboBox_SelectionChanged;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HorizontalResizeWindowBehavior.Attach(this);
    }
}
