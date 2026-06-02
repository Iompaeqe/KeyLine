using System.Windows.Controls;
using MacroSpammer.UI.Common.EntryBlocks;

namespace MacroSpammer.UI.Inspector;

public partial class InspectorWindow
{
    private NameEntryBlock TimelineNameEntry => TimelineNameRow.GetContent<NameEntryBlock>()!;

    private Grid TimelineNameReadOnlyHost => TimelineNameEntry.ReadOnly;

    private TextBlock TimelineNameTextBlock => TimelineNameEntry.TextBlock;

    private TextBlock TimelineNameEditIcon => TimelineNameEntry.EditButton;

    private TextBox TimelineNameEditTextBox => TimelineNameEntry.TextBox;

    private NumberEntryBlock TimelineLoopsEntry => TimelineLoopsRow.GetContent<NumberEntryBlock>()!;

    private TimeEntryBlock TimelineLoopDelayEntry => TimelineLoopDelayRow.GetContent<TimeEntryBlock>()!;

    private PagerEntryBlock InputTypePager => InputTypeRow.GetContent<PagerEntryBlock>()!;

    private CheckBox TimelineStandardDelayCheckBox => TimelineStandardDelayRow.GetContent<CheckBox>()!;

    private TimeEntryBlock TimelineStandardDelayEntry => TimelineStandardDelayValueRow.GetContent<TimeEntryBlock>()!;

    private CheckBox TimelineShowKeyUpDownCheckBox => TimelineShowKeyUpDownRow.GetContent<CheckBox>()!;
}