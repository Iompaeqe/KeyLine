using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Nodes;

public partial class AddNode : UserControl
{
    public event RoutedEventHandler? AddClicked;

    public AddNode()
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
