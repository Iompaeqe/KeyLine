using System.Windows.Controls;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.MainWindow.Controls;

public partial class TargetWindowPanel : UserControl
{
    public TargetWindowPanel()
    {
        InitializeComponent();
    }

    public OptionsPillBlock SearchPill => TargetWindowSearchPill;
    public TextBox SearchTextBox => TargetWindowSearchPill.InputElement;

    public ComboBox WindowSelector => WindowComboBox;
    public ComboBox HandleSelector => HandleComboBox;
}