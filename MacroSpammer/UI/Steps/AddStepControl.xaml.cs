using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Steps;

public partial class AddStepControl : UserControl
{
    public event RoutedEventHandler? AddClicked;

    public AddStepControl()
    {
        InitializeComponent();
        ApplyConfig();
        AddButton.Click += (_, e) => AddClicked?.Invoke(this, e);
    }

    private void ApplyConfig()
    {
        var ui = GeneratedUiConfig.AddStep;

        AddButton.Width = ui.Width;
        AddButton.Height = ui.Height;
        AddButton.FontSize = ui.FontSize;
        AddButton.Margin = ui.Margin;
        AddButton.Foreground = new SolidColorBrush(ui.Text);
    }
}
