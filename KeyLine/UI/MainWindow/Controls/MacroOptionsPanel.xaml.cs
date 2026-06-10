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
    public ComboBox SequenceModeSelector => SequenceModeComboBox;
    public System.Windows.Controls.Panel SequenceModePanelControl => SequenceModePanel;

    public System.Windows.Controls.Primitives.ToggleButton HooksToggleControl => HooksToggle;
    public System.Windows.Controls.Primitives.Popup HooksPopupControl => HooksPopup;
    public CheckBox StartHookCheckBoxControl => StartHookCheckBox;
    public CheckBox EndHookCheckBoxControl => EndHookCheckBox;
    public OptionsPillBlock ResetPillControl => ResetPill;
}
