using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    private bool _isSettingTimelineState;
    private bool _isEditingEnabled = true;
    private bool _isTimelineNameEditing;
    private string _timelineName = string.Empty;
    private int _loopDelayMs;
    private int _standardDelayMs;
    private int _loopCount;

    public void SetTimelineState(TimelineInspectorState state)
    {
        _isSettingTimelineState = true;
        _isEditingEnabled = state.IsEditingEnabled;
        _loopCount = Math.Max(0, state.LoopCount);
        _loopDelayMs = Math.Max(0, state.LoopDelayMs);
        _standardDelayMs = Math.Max(0, state.StandardDelayMs);

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
            if (_isEditingEnabled)
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
            value => TimelineLoopDelayCommitted?.Invoke(value));

        WireDelayTextBox(
            TimelineStandardDelayEntry,
            () => _standardDelayMs,
            value => TimelineStandardDelayCommitted?.Invoke(value));

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

    private void BeginTimelineNameEdit()
    {
        if (_isTimelineNameEditing)
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

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.Text = currentValue().ToString();
            entry.UnitText.Text = "ms";
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) => CommitDelayText(entry, currentValue(), commit);
        textBox.TextChanged += (_, _) => CommitDelayText(entry, currentValue(), commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitDelayText(entry, currentValue(), commit);
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
        if (!int.TryParse(textBox.Text, out var value))
            value = 0;

        value = Math.Max(0, value);
        if (value != originalValue)
        {
            commit(value);
            return;
        }

        entry.SetDisplay(value);
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
