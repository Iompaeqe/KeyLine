using System.Windows.Controls;

namespace MacroSpammer.UI.MainWindow.Controls;

public partial class MacroTabsBlock : UserControl
{
    public MacroTabsBlock()
    {
        InitializeComponent();
    }

    public ScrollViewer TabsScrollViewer => MacroTabsScrollViewer;
    public StackPanel TabsPanel => MacroTabsPanel;

    public Border LeftEdgeFade => MacroTabsLeftEdgeFade;
    public Border RightEdgeFade => MacroTabsRightEdgeFade;

    public Border LeftEdgeLine => MacroTabsLeftEdgeLine;
    public Border RightEdgeLine => MacroTabsRightEdgeLine;

    public Button AddButton => AddMacroTabButton;
}