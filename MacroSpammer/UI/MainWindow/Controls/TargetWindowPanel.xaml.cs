using System.Windows.Controls;
using MacroSpammer.UI.Common.EntryBlocks;

namespace MacroSpammer.UI.MainWindow.Controls;

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