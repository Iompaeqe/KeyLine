using System.Windows;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.AppWindow;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.Services.Recording;
using KeyLine.State;
using KeyLine.UI.Profiles;

namespace KeyLine;

public partial class MainWindow : Window
{
    private readonly List<MacroWorkspace> _workspaces;
    private readonly List<MacroProfile> _profiles;
    private int _activeWorkspaceIndex;
    private string _activeProfileId;
    private MacroWorkspace _activeWorkspace;
    private MacroDocument _document;
    private readonly PlaybackController _playback = new();
    private readonly MacroRecorder _recorder = new();
    private readonly AppSettings _settings;

    private readonly Dictionary<object, Point> _timelineVisualPositions = new();
    private ProfileDropdownController? _profileDropdown;

    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    private MacroTimeline? _recordingTimeline;
    private MacroTimeline? _popupTimeline;
    private bool _isSwitchingWorkspace;
    private bool _isRestoringWindowSelection;
    private bool _isTimelineEditingEnabled = true;

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
        _profiles = savedState?.Profiles ?? new List<MacroProfile>();
        _activeProfileId = savedState?.ActiveProfileId ?? MacroProfile.NoProfileId;
        NormalizeProfileState();
        EnsureWorkspaceForProfile(_activeProfileId, _settings);
        _activeWorkspaceIndex = ResolveInitialWorkspaceIndex(savedState?.ActiveWorkspaceIndex ?? 0);
        _activeWorkspace = _workspaces[_activeWorkspaceIndex];
        _document = _activeWorkspace.Document;

        InitializeComponent();
        ApplySavedMainWindowWidth(savedState?.MainWindowWidth ?? 0);
        InitializeWorkspaceTabs();
        InitializeProfileDropdown();
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
        PreviewMouseDown += Window_PreviewMouseDown;
        
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
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;

        if (_settings.StartMinimized)
            WindowState = WindowState.Minimized;
    }

    private void ApplySavedMainWindowWidth(double savedWidth)
    {
        if (savedWidth <= 0)
            return;

        Width = Math.Max(MinWidth, savedWidth);
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
