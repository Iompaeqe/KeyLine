using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Input;
using KeyLine.Services.Windows;

namespace KeyLine;

public partial class MainWindow
{
    // From MainWindow.WindowSelection.cs
        private TargetWindowController? _targetWindowController;

        private TargetWindowController TargetWindowSelection =>
            _targetWindowController ??= new TargetWindowController(
                WindowComboBox,
                HandleComboBox,
                getActiveWorkspace: () => _activeWorkspace,
                clearMacroError: ClearMacroError,
                setMacroError: SetMacroError,
                scheduleSaveState: ScheduleSaveState,
                setRestoringWindowSelection: value => _isRestoringWindowSelection = value);

        private void LoadWindows()
        {
            TargetWindowSelection.LoadWindows();
        }

        private void WindowComboBox_DropDownOpened(object? sender, EventArgs e)
        {
            TargetWindowSelection.LoadWindows();
        }

        private void WindowComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TargetWindowSelection.WindowSelectionChanged();
        }

        private void HandleComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TargetWindowSelection.HandleSelectionChanged();
        }

        private TargetWindowInfo? GetTargetHandle()
        {
            return TargetWindowSelection.GetTargetHandle();
        }

        private bool HasResolvedTargetSelection()
        {
            return TargetWindowSelection.HasResolvedTargetSelection();
        }

        private TargetWindowInfo? GetPlaybackTarget(MacroWorkspace workspace, bool updateSelection)
        {
            return TargetWindowSelection.GetPlaybackTarget(workspace, updateSelection);
        }

        private void CaptureSelectedTargetWindow(MacroWorkspace workspace)
        {
            TargetWindowSelection.CaptureSelectedTargetWindow(workspace);
        }

        private void RestoreTargetWindowSelection(MacroWorkspace workspace)
        {
            TargetWindowSelection.RestoreTargetWindowSelection(workspace);
        }

        private bool TryResolveTargetWindowSearchName(MacroWorkspace workspace, bool updateSelection)
        {
            return TargetWindowSelection.TryResolveTargetWindowSearchName(workspace, updateSelection);
        }

        private static bool HasSelectedTarget(MacroWorkspace workspace) =>
            TargetWindowController.HasSelectedTarget(workspace);

        private void SetMacroError(MacroWorkspace workspace, string message)
        {
            workspace.ErrorMessage = message;

            if (ReferenceEquals(workspace, _activeWorkspace))
            {
                StatusText.Text = message;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }

            RefreshMacroTabs();
        }

        private void ClearMacroError(MacroWorkspace workspace)
        {
            if (string.IsNullOrWhiteSpace(workspace.ErrorMessage))
                return;

            var previousMessage = workspace.ErrorMessage;
            workspace.ErrorMessage = string.Empty;

            if (ReferenceEquals(workspace, _activeWorkspace) &&
                string.Equals(StatusText.Text, previousMessage, StringComparison.Ordinal))
            {
                RestoreDefaultStatusTextForActiveWorkspace();
            }

            RefreshMacroTabs();
        }

        private void RestoreDefaultStatusTextForActiveWorkspace()
        {
            if (_recorder.IsRecording && _recordingTimeline != null)
            {
                StatusText.Text = $"\u25CF Recording {_recordingTimeline.Name}";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                return;
            }

            if (IsWorkspaceRunning(_activeWorkspace))
            {
                StatusText.Text = IsWorkspacePaused(_activeWorkspace)
                    ? "Paused"
                    : $"Running {_activeWorkspace.Name}";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                return;
            }

            StatusText.Text = "Stopped";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
        }

    // From MainWindow.MouseCapture.cs
        private readonly MouseCoordinatePicker _mouseCoordinatePicker = new();

        private async Task PickMouseCoordinatesForNodeAsync(MacroNode node)
        {
            var target = GetTargetHandle();
            if (target == null || target.Handle == 0)
            {
                StatusText.Text = "Select a target window before picking mouse coordinates";
                return;
            }

            var previousStatus = StatusText.Text;
            StatusText.Text = "Click inside the target window to capture X/Y";

            var picked = await _mouseCoordinatePicker.PickAsync(this, target.Handle);

            if (picked == null)
            {
                StatusText.Text = previousStatus;
                return;
            }

            node.MouseX = Math.Max(0, (int)picked.Value.X);
            node.MouseY = Math.Max(0, (int)picked.Value.Y);

            StatusText.Text = $"Captured mouse point ({node.MouseX}, {node.MouseY})";
            RefreshTimeline();
            ScheduleSaveState();
        }

        private async Task PickConditionPixelForNodeAsync(MacroNode node)
        {
            var target = GetTargetHandle();
            if (target == null || target.Handle == 0)
            {
                StatusText.Text = "Select a target window before picking a pixel";
                return;
            }

            var previousStatus = StatusText.Text;
            StatusText.Text = "Click a target-window pixel to capture position and color";

            var picked = await _mouseCoordinatePicker.PickPixelAsync(this, target.Handle);

            if (picked == null)
            {
                StatusText.Text = previousStatus;
                return;
            }

            node.ConditionPixelX = Math.Max(0, (int)picked.ClientPoint.X);
            node.ConditionPixelY = Math.Max(0, (int)picked.ClientPoint.Y);
            node.ConditionPixelRed = picked.Color.Red;
            node.ConditionPixelGreen = picked.Color.Green;
            node.ConditionPixelBlue = picked.Color.Blue;

            StatusText.Text =
                $"Captured pixel ({node.ConditionPixelX}, {node.ConditionPixelY}) RGB({picked.Color.Red}, {picked.Color.Green}, {picked.Color.Blue})";

            RefreshTimeline();
            ScheduleSaveState();
        }

}
