using System.Windows;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private void AddButton_Click(object sender, RoutedEventArgs e) => AddPopup.IsOpen = true;

    private void RecordMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        StartRecording();
    }

    private void DelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        _document.Steps.Add(new MacroStep { Type = MacroStepType.Delay, DelayMs = 100, IsRecordedDelay = false });
        UseStandardDelayCheckBox.IsChecked = false;
        RefreshTimeline();
    }

    private void TextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        var dialog = new TextInputWindow { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;
        _document.Steps.Add(new MacroStep { Type = MacroStepType.Text, Text = dialog.ResultText });
        RefreshTimeline();
    }
}
