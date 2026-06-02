using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer;

public partial class MainWindow
{
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
}