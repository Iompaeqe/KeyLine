using System.Windows;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.AppWindow;
using KeyLine.Services.Features;
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
    private readonly FeatureGate _featureGate = new(new FeatureConfig());
    private readonly MacroFeatureValidator _macroFeatureValidator;

    private readonly Dictionary<object, Point> _timelineVisualPositions = new();
    private ProfileDropdownController? _profileDropdown;

    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    private MacroTimeline? _recordingTimeline;
    private MacroNode? _recordingRawInsertAnchor;
    private MacroTimeline? _popupTimeline;
    private MacroNode? _popupRawInsertAnchor;
    private bool _isSwitchingWorkspace;
    private bool _isRestoringWindowSelection;
    private bool _isTimelineEditingEnabled = true;

    private MacroTimeline? _pendingClearTimeline;
    private bool _isClearConfirmationActive;
    private MacroTimeline? _pendingDeleteTimeline;
    private bool _isShortcutClearConfirmationActive;

    private bool _didInitialTimelineRefresh;
    private bool _didCompleteDeferredStartup;

    public MainWindow()
    {
        _macroFeatureValidator = new MacroFeatureValidator(_featureGate);

        var savedState = MacroStateStore.Load();
        _settings = savedState?.Settings ?? new AppSettings();
        _workspaces = savedState?.Workspaces.Count > 0
            ? savedState.Workspaces
            : new List<MacroWorkspace> { CreateWorkspace(1, _settings) };
        _profiles = savedState?.Profiles ?? new List<MacroProfile>();
        _activeProfileId = savedState?.ActiveProfileId ?? MacroProfile.NoProfileId;
        NormalizeProfileState();
        if (!_featureGate.IsEnabled(FeatureId.Profiles))
            _activeProfileId = MacroProfile.NoProfileId;

        EnsureWorkspaceForProfile(_activeProfileId, _settings);
        _activeWorkspaceIndex = ResolveInitialWorkspaceIndex(savedState?.ActiveWorkspaceIndex ?? 0);
        _activeWorkspace = _workspaces[_activeWorkspaceIndex];
        _document = _activeWorkspace.Document;

        InitializeComponent();
        TimelineAddMenu.ActionRequested += TimelineAddMenu_ActionRequested;
        ApplySavedMainWindowWidth(savedState?.MainWindowWidth ?? 0);
        InitializeWorkspaceTabs();
        InitializeProfileDropdown();
        InitializeSettingsModal();
        InitializeInspector();
        InitializeMacroOptions();
        InitializeStatePersistence();
        InitializeShortcuts();
        InitializeGlobalRemapToggle();
        
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

        ActivateWorkspace(_activeWorkspaceIndex, false, deferExpensiveWork: true);

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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_didInitialTimelineRefresh)
            return;

        _didInitialTimelineRefresh = true;

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

        RefreshTimeline();
        UpdateTimelineScrollIndicator();
        RefitTimelineWidthOnceViewportSettles();

        _ = CompleteDeferredStartupAsync();
    }

    // The first timeline layout can run before the scroll viewport has its final measured width,
    // which leaves nodes rendered off-position (they only snap to the correct left layout after the
    // next user action / relayout). Wait until the viewport width stops changing, then do one full
    // timeline rebuild - the same thing a node edit or resize does - so the macro opens correctly.
    private void RefitTimelineWidthOnceViewportSettles()
    {
        if (TimelineScrollViewer == null)
            return;

        var lastViewportWidth = -1.0;

        void OnLayoutUpdated(object? sender, EventArgs e)
        {
            var viewportWidth = TimelineScrollViewer.ViewportWidth;
            if (viewportWidth <= 0)
                return;

            // Keep widths fitted as the viewport settles, then rebuild once it is stable.
            EnsureTimelineCanvasWidthForAllRows(GetMinimumTimelineCanvasWidth());

            if (Math.Abs(viewportWidth - lastViewportWidth) < 0.5)
            {
                TimelineScrollViewer.LayoutUpdated -= OnLayoutUpdated;
                RefreshTimelineWithoutInspector();
                UpdateTimelineScrollIndicator();
            }

            lastViewportWidth = viewportWidth;
        }

        TimelineScrollViewer.LayoutUpdated += OnLayoutUpdated;
    }

    private async Task CompleteDeferredStartupAsync()
    {
        if (_didCompleteDeferredStartup)
            return;

        _didCompleteDeferredStartup = true;

        var startupWorkspace = _activeWorkspace;

        try
        {
            await RestoreTargetWindowSelectionAsync(startupWorkspace);

            if (ReferenceEquals(startupWorkspace, _activeWorkspace) &&
                !HasResolvedTargetSelection() &&
                !string.IsNullOrWhiteSpace(startupWorkspace.TargetWindowSearchName))
            {
                await TryResolveTargetWindowSearchNameAsync(startupWorkspace, updateSelection: true);
            }
        }
        catch
        {
            // Startup should never stay frozen because a window disappeared during async enumeration.
        }

        try
        {
            await Dispatcher.InvokeAsync(ApplyShortcutHookState, DispatcherPriority.Background);
        }
        catch
        {
            // Keep startup usable even if the OS rejects hook installation.
        }

        // Final guaranteed rebuild once startup is fully complete (styles/resources are live, so
        // node widths measure correctly). Ensures the opened macro is laid out left-aligned.
        await Dispatcher.InvokeAsync(() =>
        {
            RefreshTimelineWithoutInspector();
            UpdateTimelineScrollIndicator();
        }, DispatcherPriority.Background);

        BeginSettingsUpdateCheck();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HorizontalResizeWindowBehavior.Attach(this);
    }
}
