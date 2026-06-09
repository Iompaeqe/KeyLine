using KeyLine.Interop;
using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public interface IMacroRunHost
{
    string? ResolveMacroName(string macroId, MacroRunContext context);
    bool IsMacroRunning(string macroId, MacroRunContext context);
    Task RunMacroAsync(string macroId, MacroRunContext context, Action<string>? reportFailure, CancellationToken token);
}

public sealed class MacroRunContext
{
    private readonly HashSet<string> _macroCallStack;
    private readonly Dictionary<MacroNode, DateTime> _timePassedStartsUtc = new();

    public MacroRunContext(
        nint selectedTargetWindowHandle,
        string currentMacroId = "",
        IReadOnlyList<MacroWorkspace>? activeProfileWorkspaces = null,
        IMacroRunHost? runHost = null)
        : this(
            selectedTargetWindowHandle,
            currentMacroId,
            activeProfileWorkspaces ?? Array.Empty<MacroWorkspace>(),
            runHost,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase))
    {
    }

    private MacroRunContext(
        nint selectedTargetWindowHandle,
        string currentMacroId,
        IReadOnlyList<MacroWorkspace> activeProfileWorkspaces,
        IMacroRunHost? runHost,
        HashSet<string> macroCallStack)
    {
        SelectedTargetWindowHandle = selectedTargetWindowHandle;
        CurrentTargetWindowHandle = selectedTargetWindowHandle;
        CurrentMacroId = NormalizeMacroId(currentMacroId);
        ActiveProfileWorkspaces = activeProfileWorkspaces;
        RunHost = runHost;
        _macroCallStack = macroCallStack;

        if (!string.IsNullOrWhiteSpace(CurrentMacroId))
            _macroCallStack.Add(CurrentMacroId);
    }

    public nint SelectedTargetWindowHandle { get; }

    public string CurrentMacroId { get; }

    public IReadOnlyList<MacroWorkspace> ActiveProfileWorkspaces { get; }

    public IMacroRunHost? RunHost { get; }

    public nint CurrentTargetWindowHandle { get; set; }

    /// <summary>
    /// When true the macro has no fixed target window and should send input to whatever
    /// window is in the foreground at the moment of each step ("Focused window" target).
    /// </summary>
    public bool FollowForegroundWindow { get; init; }

    public nint LastLaunchedWindowHandle { get; set; }

    public int? LastLaunchedProcessId { get; set; }

    public nint LastFoundWindowHandle { get; set; }

    public bool HasValidSelectedTargetWindow =>
        SelectedTargetWindowHandle != 0 && NativeMethods.IsWindow(SelectedTargetWindowHandle);

    public bool IsMacroInCallStack(string macroId) =>
        !string.IsNullOrWhiteSpace(macroId) && _macroCallStack.Contains(NormalizeMacroId(macroId));

    public MacroRunContext CreateChild(MacroWorkspace workspace)
    {
        var child = new MacroRunContext(
            SelectedTargetWindowHandle,
            workspace.Id,
            ActiveProfileWorkspaces,
            RunHost,
            new HashSet<string>(_macroCallStack, StringComparer.OrdinalIgnoreCase))
        {
            CurrentTargetWindowHandle = CurrentTargetWindowHandle,
            FollowForegroundWindow = FollowForegroundWindow,
            LastLaunchedWindowHandle = LastLaunchedWindowHandle,
            LastLaunchedProcessId = LastLaunchedProcessId,
            LastFoundWindowHandle = LastFoundWindowHandle
        };

        return child;
    }

    public bool HasTimePassed(MacroNode node, int intervalMs, DateTime utcNow)
    {
        intervalMs = Math.Max(1, intervalMs);

        if (!_timePassedStartsUtc.TryGetValue(node, out var startedUtc))
        {
            _timePassedStartsUtc[node] = utcNow;
            return false;
        }

        if ((utcNow - startedUtc).TotalMilliseconds < intervalMs)
            return false;

        _timePassedStartsUtc[node] = utcNow;
        return true;
    }

    private static string NormalizeMacroId(string? macroId) =>
        string.IsNullOrWhiteSpace(macroId) ? "" : macroId.Trim();
}
