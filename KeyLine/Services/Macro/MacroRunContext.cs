using KeyLine.Interop;

namespace KeyLine.Services.Macro;

public sealed class MacroRunContext
{
    public MacroRunContext(nint selectedTargetWindowHandle)
    {
        SelectedTargetWindowHandle = selectedTargetWindowHandle;
        CurrentTargetWindowHandle = selectedTargetWindowHandle;
    }

    public nint SelectedTargetWindowHandle { get; }

    public nint CurrentTargetWindowHandle { get; set; }

    public nint LastLaunchedWindowHandle { get; set; }

    public int? LastLaunchedProcessId { get; set; }

    public nint LastFoundWindowHandle { get; set; }

    public bool HasValidSelectedTargetWindow =>
        SelectedTargetWindowHandle != 0 && NativeMethods.IsWindow(SelectedTargetWindowHandle);
}
