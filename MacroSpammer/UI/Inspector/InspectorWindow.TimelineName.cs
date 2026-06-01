using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MacroSpammer.UI.Inspector;

public partial class InspectorWindow
{
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
}
