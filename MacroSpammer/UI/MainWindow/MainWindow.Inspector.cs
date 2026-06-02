using System.Windows;
using MacroSpammer.UI.Inspector;

namespace MacroSpammer;

public partial class MainWindow
{
    private InspectorDockController? _inspectorDock;

    private void InitializeInspector()
    {
        _inspectorDock = new InspectorDockController(
            owner: this,
            selection: _selection,
            getCurrentTimeline: () => _selection.SelectedTimeline ?? _document.ActiveTimeline,
            canEdit: () => _isTimelineEditingEnabled,
            saveUndoSnapshot: SaveUndoSnapshot,
            refreshTimeline: () => RefreshTimeline(),
            scheduleSaveState: ScheduleSaveState,
            selectTimeline: SelectTimeline,
            pickMouseCoordinatesForNodeAsync: PickMouseCoordinatesForNodeAsync);
    }

    private void ToggleRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        _inspectorDock?.Toggle();
    }

    private void OpenInspectorFromSelection()
    {
        _inspectorDock?.OpenFromSelection();
    }

    private void RefreshInspector()
    {
        _inspectorDock?.Refresh();
    }

    private void HideInspector()
    {
        _inspectorDock?.Hide();
    }

    private void ShutdownInspector()
    {
        _inspectorDock?.Shutdown();
        _inspectorDock = null;
    }
}