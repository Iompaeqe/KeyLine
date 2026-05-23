using System.Windows;
using System.Windows.Controls;

namespace KeySpammer.UI.Controls;

public partial class AddStepControl : UserControl
{
    public event RoutedEventHandler? AddClicked;

    public AddStepControl()
    {
        InitializeComponent();
        AddButton.Click += (_, e) => AddClicked?.Invoke(this, e);
    }
}
