using System.Windows;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow : Window
{
    /// <summary>Tracks every live inspector field so edits can be finished/cleared from one place.</summary>
    public InspectorFieldRegistry FieldRegistry { get; } = new();

    private InspectorFieldHost _timelineFieldHost = null!;

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
    public event Action<int>? TimelineCooldownCommitted;

    public InspectorWindow()
    {
        InitializeComponent();
        ShowActivated = false;
        SizeChanged += (_, _) => UpdateScrollBarVisibility();

        _timelineFieldHost = new InspectorFieldHost(
            register: FieldRegistry.RegisterPersistent,
            isRefreshing: () => _isSettingTimelineState,
            requestDefocus: DefocusActiveField,
            canEdit: () => _isEditingEnabled);

        WireStaticInspectorEvents();
    }

    /// <summary>Builds a field host for the rebuilt-per-selection node inspectors.</summary>
    public InspectorFieldHost CreateNodeFieldHost(Func<bool> isRefreshing, Func<bool> canEdit) =>
        new(
            register: FieldRegistry.RegisterNode,
            isRefreshing: isRefreshing,
            requestDefocus: DefocusActiveField,
            canEdit: canEdit);

    /// <summary>Drops stale node-field registrations before the node panel is rebuilt.</summary>
    public void ClearNodeFields() => FieldRegistry.ClearNodeFields();
}