using System.Windows;
using System.Windows.Controls;

namespace KeyLine.UI.MainWindow.Controls;

public partial class MacroTabsBlock : UserControl
{
    public MacroTabsBlock()
    {
        InitializeComponent();
    }

    public ScrollViewer TabsScrollViewer => MacroTabsScrollViewer;
    public StackPanel TabsPanel => MacroTabsPanel;
    public Canvas DragOverlay => MacroTabsDragOverlay;

    public Border LeftEdgeFade => MacroTabsLeftEdgeFade;
    public Border RightEdgeFade => MacroTabsRightEdgeFade;

    public Border LeftEdgeLine => MacroTabsLeftEdgeLine;
    public Border RightEdgeLine => MacroTabsRightEdgeLine;

    public Button AddButton => AddMacroTabButton;

    public void SetReorderNoticeVisible(bool isVisible)
    {
        MacroTabReorderNotice.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
