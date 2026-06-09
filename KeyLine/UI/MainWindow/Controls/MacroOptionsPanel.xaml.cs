using System.Windows.Controls;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.MainWindow.Controls;

public partial class MacroOptionsPanel : UserControl
{
    public MacroOptionsPanel()
    {
        InitializeComponent();
    }
    public OptionsPillBlock ShortcutPillControl => ShortcutPill;
    public OptionsPillBlock ShortcutTogglePillControl => ShortcutTogglePill;
    public OptionsPillBlock ShortcutRemapPillControl => ShortcutRemapPill;

    public Border ShortcutBorderControl => ShortcutPill.BorderElement;
    public TextBlock ShortcutText => ShortcutPill.TextElement;
    public TextBlock ShortcutToggleText => ShortcutTogglePill.TextElement;
    public TextBlock ShortcutRemapText => ShortcutRemapPill.TextElement;
    public ComboBox LoopModeSelector => LoopModeComboBox;

    public OptionsPillBlock HooksPillControl => HooksPill;
    public System.Windows.Controls.Primitives.Popup HooksPopupControl => HooksPopup;
    public CheckBox StartHookCheckBoxControl => StartHookCheckBox;
    public CheckBox EndHookCheckBoxControl => EndHookCheckBox;
    public OptionsPillBlock ResetPillControl => ResetPill;
    public OptionsPillBlock ResetShortcutPillControl => ResetShortcutPill;
}
