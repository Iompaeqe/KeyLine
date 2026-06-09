using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    private bool _isSettingTimelineState;
    private bool _isEditingEnabled = true;
    private bool _isNameReadOnly;
    private bool _isTimelineNameEditing;
    private string _timelineName = string.Empty;
    private int _loopDelayMs;
    private int _standardDelayMs;
    private int _loopCount;
    private int _cooldownMs;

    public void SetTimelineState(TimelineInspectorState state)
    {
        _isSettingTimelineState = true;
        _isEditingEnabled = state.IsEditingEnabled;
        _isNameReadOnly = state.IsNameReadOnly;
        _loopCount = Math.Max(0, state.LoopCount);
        _loopDelayMs = DelayFormatter.ClampMilliseconds(state.LoopDelayMs);
        _standardDelayMs = DelayFormatter.ClampMilliseconds(state.StandardDelayMs);
        _cooldownMs = DelayFormatter.ClampMilliseconds(state.CooldownMs);

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
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelTimelineNameEdit();
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
            value => TimelineLoopsCommitted?.Invoke(value));

        WireDelayTextBox(
            TimelineLoopDelayEntry,
            () => _loopDelayMs,
            value =>
            {
                _loopDelayMs = value;
                TimelineLoopDelayCommitted?.Invoke(value);
            });

        WireDelayTextBox(
            TimelineStandardDelayEntry,
            () => _standardDelayMs,
            value =>
            {
                _standardDelayMs = value;
                TimelineStandardDelayCommitted?.Invoke(value);
            });

        WireDelayTextBox(
            TimelineCooldownEntry,
            () => _cooldownMs,
            value =>
            {
                _cooldownMs = value;
                TimelineCooldownCommitted?.Invoke(value);
            });

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
        TimelineLoopsEntry.TextBox.Text = _loopCount.ToString();
        TimelineLoopDelayEntry.SetDisplay(_loopDelayMs);
        InputTypePager.Text = state.UseTextInputMode ? "Text" : "Key";

        TimelineStandardDelayCheckBox.IsChecked = state.UseStandardDelay;
        StandardDelayDetailsPanel.Visibility = state.UseStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        TimelineStandardDelayEntry.SetDisplay(_standardDelayMs);
        TimelineShowKeyUpDownCheckBox.IsChecked = state.ShowKeyUpDown;

        TimelineCooldownRow.Visibility = state.ShowCooldown ? Visibility.Visible : Visibility.Collapsed;
        TimelineCooldownEntry.SetDisplay(_cooldownMs);
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

    private void WireNumberTextBox(TextBox textBox, Func<int> currentValue, Action<int> commit)
    {
        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) => textBox.SelectAll();
        textBox.LostFocus += (_, _) => CommitNumberText(textBox, currentValue(), commit);
        textBox.TextChanged += (_, _) => CommitNumberText(textBox, currentValue(), commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitNumberText(textBox, currentValue(), commit);
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private void WireDelayTextBox(TimeEntryBlock entry, Func<int> currentValue, Action<int> commit)
    {
        var textBox = entry.TextBox;
        var isEditing = false;
        var isSettingText = false;

        void SetEditText(int value)
        {
            isSettingText = true;
            try
            {
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
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(value));
            }
            finally
            {
                isSettingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            SetEditText(currentValue());
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) =>
        {
            CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
        };
        textBox.TextChanged += (_, _) =>
        {
            if (!isEditing || isSettingText)
                return;

            CommitDelayText(entry, currentValue(), commit);
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private void CommitNumberText(TextBox textBox, int originalValue, Action<int> commit)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            value = 0;

        value = Math.Max(0, value);
        textBox.Text = value.ToString();

        if (value != originalValue)
            commit(value);
    }

    private void CommitDelayText(TimeEntryBlock entry, int originalValue, Action<int> commit)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return;

        var textBox = entry.TextBox;
        if (!long.TryParse(textBox.Text, out var value))
            value = string.IsNullOrWhiteSpace(textBox.Text) ? 0 : DelayFormatter.MaxMilliseconds;

        var clampedValue = DelayFormatter.ClampMilliseconds(value);
        if (clampedValue != originalValue)
            commit(clampedValue);
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
}
