using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    private readonly List<Action> _finishInspectorEditActions = new();
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
                DefocusInspectorField(TimelineNameEditTextBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelTimelineNameEdit();
                DefocusInspectorField(TimelineNameEditTextBox);
                e.Handled = true;
            }
        };

        TimelineNameEditTextBox.LostFocus += (_, _) =>
        {
            if (!_isSettingTimelineState && _isTimelineNameEditing)
                CommitTimelineNameEdit();
        };

        WireNumberTextBox(
            TimelineLoopsEntry.TextBox,
            () => _loopCount,
            value => TimelineLoopsCommitted?.Invoke(value),
            () => _loopCountMixed);

        WireDelayTextBox(
            TimelineLoopDelayEntry,
            () => _loopDelayMs,
            value =>
            {
                _loopDelayMs = value;
                TimelineLoopDelayCommitted?.Invoke(value);
            },
            () => _loopDelayMixed);

        WireDelayTextBox(
            TimelineStandardDelayEntry,
            () => _standardDelayMs,
            value =>
            {
                _standardDelayMs = value;
                TimelineStandardDelayCommitted?.Invoke(value);
            },
            () => _standardDelayMixed);

        WireDelayTextBox(
            TimelineCooldownEntry,
            () => _cooldownMs,
            value =>
            {
                _cooldownMs = value;
                TimelineCooldownCommitted?.Invoke(value);
            },
            () => _cooldownMixed);
        
        Deactivated += (_, _) =>
        {
            if (_isTimelineNameEditing)
                CommitTimelineNameEdit();
        };
        
        InputManager.Current.PreProcessInput += OnApplicationPreProcessInput;
        Closed += (_, _) =>
        {
            InputManager.Current.PreProcessInput -= OnApplicationPreProcessInput;
        };

        InputTypePager.PageRequested += (_, _) => RequestInputTypeChange();

        TimelineStandardDelayCheckBox.Checked += (_, _) => RaiseStandardDelayChanged(true);
        TimelineStandardDelayCheckBox.Unchecked += (_, _) => RaiseStandardDelayChanged(false);

        TimelineShowKeyUpDownCheckBox.Checked += (_, _) => RaiseShowKeyUpDownChanged(true);
        TimelineShowKeyUpDownCheckBox.Unchecked += (_, _) => RaiseShowKeyUpDownChanged(false);
    }

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
        SetNumberDisplayOrMixed(TimelineLoopsEntry.TextBox, _loopCount, _loopCountMixed);
        SetDelayDisplayOrMixed(TimelineLoopDelayEntry, _loopDelayMs, _loopDelayMixed);

        InputTypePager.Text = state.UseTextInputModeMixed
            ? MixedIndicator
            : state.UseTextInputMode ? "Text" : "Key";

        TimelineStandardDelayCheckBox.IsThreeState = state.UseStandardDelayMixed;
        TimelineStandardDelayCheckBox.IsChecked = state.UseStandardDelayMixed ? null : state.UseStandardDelay;
        TimelineStandardDelayCheckBox.ToolTip = state.UseStandardDelayMixed ? "Mixed values" : null;
        StandardDelayDetailsPanel.Visibility = state.UseStandardDelay || state.UseStandardDelayMixed
            ? Visibility.Visible
            : Visibility.Collapsed;
        SetDelayDisplayOrMixed(TimelineStandardDelayEntry, _standardDelayMs, _standardDelayMixed);

        TimelineShowKeyUpDownCheckBox.IsThreeState = state.ShowKeyUpDownMixed;
        TimelineShowKeyUpDownCheckBox.IsChecked = state.ShowKeyUpDownMixed ? null : state.ShowKeyUpDown;
        TimelineShowKeyUpDownCheckBox.ToolTip = state.ShowKeyUpDownMixed ? "Mixed values" : null;

        TimelineCooldownRow.Visibility = state.ShowCooldown ? Visibility.Visible : Visibility.Collapsed;
        SetDelayDisplayOrMixed(TimelineCooldownEntry, _cooldownMs, _cooldownMixed);
    }

    private static void SetNumberDisplayOrMixed(TextBox textBox, int value, bool mixed)
    {
        if (mixed)
        {
            BatchUi.ShowMixed(textBox);
        }
        else
        {
            BatchUi.ClearMixedStyle(textBox);
            textBox.Text = value.ToString();
        }
    }

    private static void SetDelayDisplayOrMixed(TimeEntryBlock entry, int value, bool mixed)
    {
        if (mixed)
        {
            BatchUi.ShowMixed(entry.TextBox);
            entry.UnitText.Text = string.Empty;
        }
        else
        {
            BatchUi.ClearMixedStyle(entry.TextBox);
            entry.SetDisplay(value);
        }
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

    private void WireNumberTextBox(TextBox textBox, Func<int> currentValue, Action<int> commit, Func<bool> isMixed)
    {
        var isEditing = false;

        void FinishNumberEdit()
        {
            if (!isEditing)
                return;

            var committed = CommitNumberText(textBox, currentValue(), commit, isMixed());
            isEditing = false;

            // Restore the display when nothing was written (e.g. a mixed field left untouched), since
            // a successful commit rebuilds the inspector anyway.
            if (!committed)
                SetNumberDisplayOrMixed(textBox, currentValue(), isMixed());
        }

        _finishInspectorEditActions.Add(FinishNumberEdit);

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);

        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            if (isMixed())
            {
                // Never treat the "Mixed" indicator as a real value; clear it to a normal empty field.
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = string.Empty;
            }

            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            FinishNumberEdit();
        };

        textBox.TextChanged += (_, _) =>
        {
            if (!isEditing || _isBatch)
                return; // batch fields commit once on finish, to keep a single undo step

            CommitNumberText(textBox, currentValue(), commit, isMixed());
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            FinishNumberEdit();
            DefocusInspectorField(textBox);
            e.Handled = true;
        };
    }

    private void WireDelayTextBox(TimeEntryBlock entry, Func<int> currentValue, Action<int> commit, Func<bool> isMixed)
    {
        var textBox = entry.TextBox;
        var isEditing = false;
        var isSettingText = false;

        void SetEditText(int value)
        {
            isSettingText = true;
            try
            {
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = DelayFormatter.ClampMilliseconds(value).ToString();
                entry.UnitText.Text = "ms";
            }
            finally
            {
                isSettingText = false;
            }
        }

        void SetDisplayText(int value)
        {
            isSettingText = true;
            try
            {
                BatchUi.ClearMixedStyle(textBox);
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(value));
            }
            finally
            {
                isSettingText = false;
            }
        }

        void SetMixedText()
        {
            isSettingText = true;
            try
            {
                BatchUi.ShowMixed(textBox);
                entry.UnitText.Text = string.Empty;
            }
            finally
            {
                isSettingText = false;
            }
        }

        void FinishDelayEdit()
        {
            if (!isEditing)
                return;

            var committed = CommitDelayText(entry, currentValue(), commit, isMixed());
            isEditing = false;

            if (committed)
                return; // a successful commit rebuilds the inspector

            if (isMixed())
                SetMixedText();
            else
                SetDisplayText(currentValue());
        }

        _finishInspectorEditActions.Add(FinishDelayEdit);

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);

        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            if (isMixed())
            {
                isSettingText = true;
                try
                {
                    BatchUi.ClearMixedStyle(textBox);
                    textBox.Text = string.Empty;
                    entry.UnitText.Text = "ms";
                }
                finally
                {
                    isSettingText = false;
                }
            }
            else
            {
                SetEditText(currentValue());
            }

            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            FinishDelayEdit();
        };

        textBox.TextChanged += (_, _) =>
        {
            if (!isEditing || isSettingText || _isBatch)
                return; // batch fields commit once on finish, to keep a single undo step

            CommitDelayText(entry, currentValue(), commit, isMixed());
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            FinishDelayEdit();
            DefocusInspectorField(textBox);
            e.Handled = true;
        };

        Deactivated += (_, _) =>
        {
            FinishDelayEdit();
        };
    }

    // Returns true when a value was written.
    private bool CommitNumberText(TextBox textBox, int originalValue, Action<int> commit, bool mixed)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return false;

        if (!int.TryParse(textBox.Text, out var value))
        {
            if (mixed)
                return false; // empty / "Mixed" left untouched — do not write

            value = 0;
        }

        value = Math.Max(0, value);
        if (!mixed)
            textBox.Text = value.ToString();

        if (!mixed && value == originalValue)
            return false;

        commit(value);
        return true;
    }

    // Returns true when a value was written.
    private bool CommitDelayText(TimeEntryBlock entry, int originalValue, Action<int> commit, bool mixed)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return false;

        var textBox = entry.TextBox;
        if (!long.TryParse(textBox.Text, out var value))
        {
            if (mixed)
                return false; // empty / "Mixed" left untouched — do not write

            value = string.IsNullOrWhiteSpace(textBox.Text) ? 0 : DelayFormatter.MaxMilliseconds;
        }

        var clampedValue = DelayFormatter.ClampMilliseconds(value);
        if (!mixed && clampedValue == originalValue)
            return false;

        commit(clampedValue);
        return true;
    }

    private void RequestInputTypeChange()
    {
        if (_isEditingEnabled)
            TimelineInputTypeChangeRequested?.Invoke();
    }

    private void RaiseStandardDelayChanged(bool value)
    {
        if (!_isSettingTimelineState && _isEditingEnabled)
            TimelineStandardDelayChanged?.Invoke(value);
    }

    private void RaiseShowKeyUpDownChanged(bool value)
    {
        if (!_isSettingTimelineState && _isEditingEnabled)
            TimelineShowKeyUpDownChanged?.Invoke(value);
    }
    
    private void DefocusInspectorField(TextBox? sourceTextBox = null)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (sourceTextBox != null)
                {
                    var sourceScope = FocusManager.GetFocusScope(sourceTextBox);
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
        // (At pre-process time OriginalSource is the popup root, not the item, so checking the open
        // dropdown state is more reliable than walking the click source.)
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
            if (child is System.Windows.Controls.ComboBox { IsDropDownOpen: true })
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

            foreach (var finishEdit in _finishInspectorEditActions.ToArray())
                finishEdit();

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
