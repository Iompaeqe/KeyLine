using System.Windows;
using System.Windows.Controls;
using KeyLine.UI;

namespace KeyLine.UI.MainWindow.Controls;

public partial class TitleCommandButtons : UserControl
{
    public TitleCommandButtons()
    {
        InitializeComponent();
    }

    public Button InspectorButton => ToggleRightPanelButton;
    public Button SettingsButtonControl => SettingsButton;
    public Button MinimizeButtonControl => MinimizeButton;
    public Button CloseButtonControl => CloseWindowButton;

    public void SetSettingsUpdateAvailable(bool isAvailable, string? updateText = null)
    {
        SettingsUpdateBadge.Visibility = isAvailable ? Visibility.Visible : Visibility.Collapsed;
        SettingsButton.ToolTip = isAvailable
            ? string.IsNullOrWhiteSpace(updateText)
                ? "Update available."
                : $"{updateText}."
            : TooltipNotes.OpenSettings;
    }
}
