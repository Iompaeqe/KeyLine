using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyLine.Services.Timeline;
using KeyLine.UI.Inspector.Batch;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    private bool _isFinishingInspectorEdits;

    private bool _isSettingTimelineState;
    private bool _isEditingEnabled = true;
    private bool _isNameReadOnly;
    private bool _isTimelineNameEditing;
    private string _timelineName = string.Empty;
    private int _loopDelayMs;
    private int _standardDelayMs;
    private int _loopCount;
    private int _cooldownMs;

    // Batch (multi-timeline) editing state and per-field "values differ" flags.
    private bool _isBatch;
    private bool _loopCountMixed;
    private bool _loopDelayMixed;
    private bool _cooldownMixed;
    private bool _standardDelayMixed;
    private bool _useStandardDelay;
    private bool _useStandardDelayMixed;
    private bool _showKeyUpDown;
    private bool _showKeyUpDownMixed;

    // Persistent timeline fields, refreshed (not re-wired) on every selection change.
    private IInspectorField _loopCountField = null!;
    private IInspectorField _loopDelayField = null!;
    private IInspectorField _standardDelayField = null!;
    private IInspectorField _cooldownField = null!;
    private IInspectorField _useStandardDelayField = null!;
    private IInspectorField _showKeyUpDownField = null!;

    private const string MixedIndicator = BatchUi.IndicatorText;

    public void SetTimelineState(TimelineInspectorState state)
    {
        _isSettingTimelineState = true;
        _isEditingEnabled = state.IsEditingEnabled;
        _isNameReadOnly = state.IsNameReadOnly;
        _loopCount = Math.Max(0, state.LoopCount);
        _loopDelayMs = DelayFormatter.ClampMilliseconds(state.LoopDelayMs);
        _standardDelayMs = DelayFormatter.ClampMilliseconds(state.StandardDelayMs);
        _cooldownMs = DelayFormatter.ClampMilliseconds(state.CooldownMs);

        _isBatch = state.IsBatch;
        _loopCountMixed = state.LoopCountMixed;
        _loopDelayMixed = state.LoopDelayMixed;
        _cooldownMixed = state.CooldownMixed;
        _standardDelayMixed = state.StandardDelayMixed;
        _useStandardDelay = state.UseStandardDelay;
        _useStandardDelayMixed = state.UseStandardDelayMixed;
        _showKeyUpDown = state.ShowKeyUpDown;
        _showKeyUpDownMixed = state.ShowKeyUpDownMixed;

        ApplyTimelineCollapsedState(state.IsCollapsed);
        ApplyTimelineNameState(state.TimelineName, state.IsNameEditing);
        ApplyTimelineValueState(state);
        SetTimelineControlsEnabled(state.IsEditingEnabled);

        _isSettingTimelineState = false;
        RequestScrollVisibilityUpdate();
    }

    private void WireStaticInspectorEvents()
    {
        TimelineHeader.MouseLeftButtonDown += (_, e) =>
        {
            TimelineHeaderClicked?.Invoke();
            e.Handled = true;
        };

        NodeHeader.MouseLeftButtonDown += (_, e) =>
        {
            NodeHeaderClicked?.Invoke();
            e.Handled = true;
        };

        TimelineNameEditIcon.MouseEnter += (_, _) => TimelineNameEditIcon.Opacity = 1;
        TimelineNameEditIcon.MouseLeave += (_, _) => TimelineNameEditIcon.Opacity = _isEditingEnabled ? 0.65 : 0.35;
        TimelineNameEditIcon.MouseLeftButtonDown += (_, e) =>
        {
            if (_isEditingEnabled && !_isNameReadOnly)
                BeginTimelineNameEdit();

            e.Handled = true;
        };

        TimelineNameEditTextBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitTimelineNameEdit();
                DefocusActiveField(TimelineNameEditTextBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelTimelineNameEdit();
                DefocusActiveField(TimelineNameEditTextBox);
                e.Handled = true;
            }
        };

        TimelineNameEditTextBox.LostFocus += (_, _) =>
        {
            if (!_isSettingTimelineState && _isTimelineNameEditing)
                CommitTimelineNameEdit();
        };

        _loopCountField = InspectorFieldBinder.BindNumber(
            _timelineFieldHost,
            TimelineLoopsEntry,
            read: MixedOrInt(() => _loopCountMixed, () => _loopCount),
            apply: value => TimelineLoopsCommitted?.Invoke(value),
            min: 0,
            max: null,
            tooltip: TooltipNotes.TimelineLoops,
            isEnabled: true);

        _loopDelayField = InspectorFieldBinder.BindDelay(
            _timelineFieldHost,
            TimelineLoopDelayEntry,
            read: MixedOrInt(() => _loopDelayMixed, () => _loopDelayMs),
            apply: value =>
            {
                _loopDelayMs = value;
                TimelineLoopDelayCommitted?.Invoke(value);
            },
            tooltip: TooltipNotes.TimelineLoopDelayEdit,
            isEnabled: true);

        _standardDelayField = InspectorFieldBinder.BindDelay(
            _timelineFieldHost,
            TimelineStandardDelayEntry,
            read: MixedOrInt(() => _standardDelayMixed, () => _standardDelayMs),
            apply: value =>
            {
                _standardDelayMs = value;
                TimelineStandardDelayCommitted?.Invoke(value);
            },
            tooltip: TooltipNotes.TimelineStandardDelayEdit,
            isEnabled: true);

        _cooldownField = InspectorFieldBinder.BindDelay(
            _timelineFieldHost,
            TimelineCooldownEntry,
            read: MixedOrInt(() => _cooldownMixed, () => _cooldownMs),
            apply: value =>
            {
                _cooldownMs = value;
                TimelineCooldownCommitted?.Invoke(value);
            },
            tooltip: "Cooldown before this timeline can be picked again by Sequence/Random.",
            isEnabled: true);

        _useStandardDelayField = InspectorFieldBinder.BindCheckBox(
            _timelineFieldHost,
            TimelineStandardDelayCheckBox,
            read: () => _useStandardDelayMixed
                ? BatchValue<bool>.Mixed()
                : BatchValue<bool>.Common(_useStandardDelay),
            apply: value => TimelineStandardDelayChanged?.Invoke(value),
            isEnabled: true);

        _showKeyUpDownField = InspectorFieldBinder.BindCheckBox(
            _timelineFieldHost,
            TimelineShowKeyUpDownCheckBox,
            read: () => _showKeyUpDownMixed
                ? BatchValue<bool>.Mixed()
                : BatchValue<bool>.Common(_showKeyUpDown),
            apply: value => TimelineShowKeyUpDownChanged?.Invoke(value),
            isEnabled: true);

        InputManager.Current.PreProcessInput += OnApplicationPreProcessInput;
        Closed += (_, _) =>
        {
            InputManager.Current.PreProcessInput -= OnApplicationPreProcessInput;
        };

        Deactivated += (_, _) =>
        {
            if (_isTimelineNameEditing)
                CommitTimelineNameEdit();

            FieldRegistry.FinishAll();
        };

        InputTypePager.PageRequested += (_, _) => RequestInputTypeChange();
    }

    private static Func<BatchValue<int>> MixedOrInt(Func<bool> isMixed, Func<int> value) =>
        () => isMixed() ? BatchValue<int>.Mixed() : BatchValue<int>.Common(value());

    private void ApplyTimelineCollapsedState(bool isCollapsed)
    {
        TimelineHeader.ArrowText = isCollapsed ? "▶" : "▼";
        TimelineContentPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        TimelineHeader.Margin = new Thickness(0, 0, 0, isCollapsed ? 0 : 4);
    }

    private void ApplyTimelineNameState(string timelineName, bool isNameEditing)
    {
        _timelineName = timelineName;
        SetTimelineNameDisplay(timelineName);

        if (_isTimelineNameEditing)
            return;

        TimelineNameReadOnlyHost.Visibility = Visibility.Visible;
        TimelineNameEditTextBox.Visibility = Visibility.Collapsed;
        TimelineNameEditTextBox.Text = timelineName;

        if (isNameEditing)
            BeginTimelineNameEdit();
    }

    private void ApplyTimelineValueState(TimelineInspectorState state)
    {
        _loopCountField.LoadFromModel();
        _loopDelayField.LoadFromModel();

        InputTypePager.Text = state.UseTextInputModeMixed
            ? MixedIndicator
            : state.UseTextInputMode ? "Text" : "Key";

        _useStandardDelayField.LoadFromModel();
        StandardDelayDetailsPanel.Visibility = state.UseStandardDelay || state.UseStandardDelayMixed
            ? Visibility.Visible
            : Visibility.Collapsed;
        _standardDelayField.LoadFromModel();

        _showKeyUpDownField.LoadFromModel();

        TimelineCooldownRow.Visibility = state.ShowCooldown ? Visibility.Visible : Visibility.Collapsed;
        _cooldownField.LoadFromModel();
    }

    private void SetTimelineControlsEnabled(bool isEditingEnabled)
    {
        // Hook timelines (Start/End) keep their Name row but it is read-only: hide the edit pencil.
        TimelineNameEditIcon.Visibility = _isNameReadOnly ? Visibility.Collapsed : Visibility.Visible;
        TimelineNameEditIcon.Opacity = isEditingEnabled ? 0.65 : 0.35;
        TimelineNameEditIcon.IsEnabled = isEditingEnabled && !_isNameReadOnly;
        TimelineNameEditTextBox.IsEnabled = isEditingEnabled && !_isNameReadOnly;
        TimelineLoopsEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineLoopDelayEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineStandardDelayCheckBox.IsEnabled = isEditingEnabled;
        TimelineStandardDelayEntry.TextBox.IsEnabled = isEditingEnabled;
        TimelineShowKeyUpDownCheckBox.IsEnabled = isEditingEnabled;
        TimelineCooldownEntry.TextBox.IsEnabled = isEditingEnabled;
        InputTypePager.IsEnabled = isEditingEnabled;
    }

    private void BeginTimelineNameEdit()
    {
        if (_isTimelineNameEditing || _isNameReadOnly)
            return;

        TimelineNameEditStarted?.Invoke();
        _isTimelineNameEditing = true;
        TimelineNameEditTextBox.Text = _timelineName;
        TimelineNameReadOnlyHost.Visibility = Visibility.Collapsed;
        TimelineNameEditTextBox.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                Activate();
                TimelineNameEditTextBox.Focus();
                Keyboard.Focus(TimelineNameEditTextBox);
                TimelineNameEditTextBox.SelectAll();
            }));
    }

    private void CommitTimelineNameEdit()
    {
        if (_isSettingTimelineState || !_isEditingEnabled || !_isTimelineNameEditing)
            return;

        var name = TimelineNameEditTextBox.Text;
        _isTimelineNameEditing = false;
        TimelineNameReadOnlyHost.Visibility = Visibility.Visible;
        TimelineNameEditTextBox.Visibility = Visibility.Collapsed;
        TimelineNameCommitted?.Invoke(name);
    }

    private void CancelTimelineNameEdit()
    {
        if (!_isTimelineNameEditing)
            return;

        _isTimelineNameEditing = false;
        TimelineNameEditTextBox.Text = _timelineName;
        TimelineNameReadOnlyHost.Visibility = Visibility.Visible;
        TimelineNameEditTextBox.Visibility = Visibility.Collapsed;
        TimelineNameEditCancelled?.Invoke();
    }

    private void SetTimelineNameDisplay(string timelineName)
    {
        var displayName = string.IsNullOrWhiteSpace(timelineName) ? "-" : timelineName;
        TimelineNameTextBlock.Text = displayName;
        TimelineNameTextBlock.ToolTip = displayName;
    }

    private void RequestInputTypeChange()
    {
        if (_isEditingEnabled)
            TimelineInputTypeChangeRequested?.Invoke();
    }

    /// <summary>
    /// Moves keyboard focus to the inspector's invisible sink so the active field's focus visual (the cyan
    /// border) clears and its LostFocus commit runs. Used by every inspector field on Enter/Escape and by
    /// the click-outside handler — the single, reliable way to defocus inside a WPF window.
    /// </summary>
    public void DefocusActiveField(DependencyObject? source = null)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (source != null)
                {
                    var sourceScope = FocusManager.GetFocusScope(source);
                    FocusManager.SetFocusedElement(sourceScope, InspectorFocusSink);
                }

                var sinkScope = FocusManager.GetFocusScope(InspectorFocusSink);
                FocusManager.SetFocusedElement(sinkScope, InspectorFocusSink);

                Keyboard.ClearFocus();
                InspectorFocusSink.Focus();
                Keyboard.Focus(InspectorFocusSink);
            }));
    }

    private void OnApplicationPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (_isFinishingInspectorEdits)
            return;

        if (e.StagingItem.Input is not MouseButtonEventArgs mouseArgs)
            return;

        if (mouseArgs.RoutedEvent != Mouse.PreviewMouseDownEvent &&
            mouseArgs.RoutedEvent != Mouse.MouseDownEvent)
            return;

        // A ComboBox dropdown is its own popup window, so clicking an item looks like an "outside"
        // click here. Finishing edits / clearing focus would close the dropdown before the selection
        // commits (the pick is lost). While any inspector dropdown is open, leave input alone.
        if (HasOpenComboBox(this))
            return;

        var source = mouseArgs.OriginalSource as DependencyObject;

        // Click is inside the inspector, normal WPF focus/lost-focus can handle it.
        if (source != null && ReferenceEquals(Window.GetWindow(source), this))
            return;

        // Click is outside the inspector, but the inspector currently owns keyboard focus.
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return;

        if (!ReferenceEquals(Window.GetWindow(focusedElement), this))
            return;

        FinishInspectorEditsFromOutsideClick();
    }

    private static bool HasOpenComboBox(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is ComboBox { IsDropDownOpen: true })
                return true;

            if (HasOpenComboBox(child))
                return true;
        }

        return false;
    }

    private void FinishInspectorEditsFromOutsideClick()
    {
        if (_isFinishingInspectorEdits)
            return;

        _isFinishingInspectorEdits = true;

        try
        {
            if (_isTimelineNameEditing)
                CommitTimelineNameEdit();

            // Commits the focused field (timeline or node) and restores any untouched mixed field.
            FieldRegistry.FinishAll();

            if (Keyboard.FocusedElement is DependencyObject focusedElement)
            {
                var focusScope = FocusManager.GetFocusScope(focusedElement);
                FocusManager.SetFocusedElement(focusScope, null);
            }

            Keyboard.ClearFocus();
        }
        finally
        {
            _isFinishingInspectorEdits = false;
        }
    }
}
