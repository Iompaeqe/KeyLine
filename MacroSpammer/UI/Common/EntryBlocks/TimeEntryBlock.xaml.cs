using System.Windows.Controls;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer.UI.Common.EntryBlocks;

public partial class TimeEntryBlock : UserControl
{
    public TimeEntryBlock()
    {
        InitializeComponent();
    }

    public TextBox TextBox => ValueTextBox;

    public TextBlock UnitText => UnitTextBlock;

    public void SetDisplay(int milliseconds)
    {
        var formatted = DelayFormatter.Split(milliseconds);
        ValueTextBox.Text = formatted.Value;
        UnitTextBlock.Text = formatted.Unit;
    }
}
