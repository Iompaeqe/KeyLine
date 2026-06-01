using System.Windows.Controls;

namespace MacroSpammer.UI.Common.EntryBlocks;

public partial class NumberEntryBlock : UserControl
{
    public NumberEntryBlock()
    {
        InitializeComponent();
    }

    public TextBox TextBox => ValueTextBox;
}
