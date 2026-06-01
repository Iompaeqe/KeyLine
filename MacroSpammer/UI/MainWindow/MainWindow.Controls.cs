using System.Windows.Controls;
using MacroSpammer.UI.Common.EntryBlocks;

namespace MacroSpammer;

public partial class MainWindow
{
    private ScrollViewer MacroTabsScrollViewer => MacroTabsBlock.TabsScrollViewer;
    private StackPanel MacroTabsPanel => MacroTabsBlock.TabsPanel;

    private Border MacroTabsLeftEdgeFade => MacroTabsBlock.LeftEdgeFade;
    private Border MacroTabsRightEdgeFade => MacroTabsBlock.RightEdgeFade;

    private Border MacroTabsLeftEdgeLine => MacroTabsBlock.LeftEdgeLine;
    private Border MacroTabsRightEdgeLine => MacroTabsBlock.RightEdgeLine;

    private Button AddMacroTabButton => MacroTabsBlock.AddButton;
    
    
    private Button ToggleRightPanelButton => TitleCommandButtons.InspectorButton;
    private Button SettingsButton => TitleCommandButtons.SettingsButtonControl;
    private Button MinimizeButton => TitleCommandButtons.MinimizeButtonControl;
    private Button CloseWindowButton => TitleCommandButtons.CloseButtonControl;
    
    private Border ShortcutBorder => MacroOptionsPanel.ShortcutBorderControl;
    private OptionsPillBlock ShortcutPill => MacroOptionsPanel.ShortcutPillControl;
    private OptionsPillBlock ShortcutTogglePill => MacroOptionsPanel.ShortcutTogglePillControl;
    private TextBlock ShortcutTextBlock => MacroOptionsPanel.ShortcutText;
    private TextBlock ShortcutToggleTextBlock => MacroOptionsPanel.ShortcutToggleText;
    private PagerEntryBlock LoopTypePager => MacroOptionsPanel.LoopTypePagerControl;
    
    private OptionsPillBlock TargetWindowSearchPill => TargetWindowPanel.SearchPill;
    private TextBox TargetWindowSearchTextBox => TargetWindowPanel.SearchTextBox;
    private ComboBox WindowComboBox => TargetWindowPanel.WindowSelector;
    private ComboBox HandleComboBox => TargetWindowPanel.HandleSelector;
    
}
