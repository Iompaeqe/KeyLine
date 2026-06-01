using System.Windows.Controls;
using MacroSpammer.UI.Common.EntryBlocks;

namespace MacroSpammer.UI.MainWindow.Controls;

public partial class MacroOptionsPanel : UserControl
{
    public MacroOptionsPanel()
    {
        InitializeComponent();
    }
    public OptionsPillBlock ShortcutPillControl => ShortcutPill;
    public OptionsPillBlock ShortcutTogglePillControl => ShortcutTogglePill;

    public Border ShortcutBorderControl => ShortcutPill.BorderElement;
    public TextBlock ShortcutText => ShortcutPill.TextElement;
    public TextBlock ShortcutToggleText => ShortcutTogglePill.TextElement;
    public PagerEntryBlock LoopTypePagerControl => LoopTypePager;
}