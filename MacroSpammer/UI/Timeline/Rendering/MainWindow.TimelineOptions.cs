using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void SelectTimeline(MacroTimeline timeline)
    {
        if (_isClearConfirmationActive && !ReferenceEquals(_pendingClearTimeline, timeline))
            ResetClearConfirmation();

        _document.SelectTimeline(timeline);
        SyncOptionsFromActiveTimeline();
        ScheduleSaveState();
    }

    private void SyncOptionsFromActiveTimeline()
    {
        RefreshInspector();
    }

    private void UpdateTimelineOptionsPagerVisibility()
    {
    }
}
