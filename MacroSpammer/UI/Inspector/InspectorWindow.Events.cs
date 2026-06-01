using System.Windows.Input;

namespace MacroSpammer.UI.Inspector;

public partial class InspectorWindow
{
    
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

        WireNumberTextBox(TimelineLoopsEntry.TextBox, () => _loopCount, value => TimelineLoopsCommitted?.Invoke(value));
        WireDelayTextBox(TimelineLoopDelayEntry, () => _loopDelayMs, value => TimelineLoopDelayCommitted?.Invoke(value));
        WireDelayTextBox(TimelineStandardDelayEntry, () => _standardDelayMs, value => TimelineStandardDelayCommitted?.Invoke(value));

        InputTypePager.PageRequested += (_, _) => RequestInputTypeChange();

        TimelineStandardDelayCheckBox.Checked += (_, _) => RaiseStandardDelayChanged(true);
        TimelineStandardDelayCheckBox.Unchecked += (_, _) => RaiseStandardDelayChanged(false);
        TimelineShowKeyUpDownCheckBox.Checked += (_, _) => RaiseShowKeyUpDownChanged(true);
        TimelineShowKeyUpDownCheckBox.Unchecked += (_, _) => RaiseShowKeyUpDownChanged(false);
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