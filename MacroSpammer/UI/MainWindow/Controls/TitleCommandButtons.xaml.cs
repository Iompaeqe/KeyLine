using System.Windows.Controls;

namespace MacroSpammer.UI.MainWindow.Controls;

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
}