using System.Windows.Controls;

namespace KeyLine.UI.Common.EntryBlocks;

public partial class NumberEntryBlock : UserControl
{
    public NumberEntryBlock()
    {
        InitializeComponent();
    }

    public TextBox TextBox => ValueTextBox;
}
