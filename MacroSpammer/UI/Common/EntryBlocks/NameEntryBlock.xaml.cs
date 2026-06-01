using System.Windows.Controls;

namespace MacroSpammer.UI.Common.EntryBlocks;

public partial class NameEntryBlock : UserControl
{
    public NameEntryBlock()
    {
        InitializeComponent();
    }

    public Grid ReadOnly => ReadOnlyHost;
    public TextBlock TextBlock => DisplayTextBlock;
    public TextBlock EditButton => EditIcon;
    public TextBox TextBox => EditTextBox;
}