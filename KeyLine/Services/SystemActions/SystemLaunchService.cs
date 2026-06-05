using System.Diagnostics;
using KeyLine.Domain;

namespace KeyLine.Services.SystemActions;

public static class SystemLaunchService
{
    public static SystemLaunchResult TryOpen(MacroNode node)
    {
        var target = NormalizeTarget(node);
        if (string.IsNullOrWhiteSpace(target))
            return SystemLaunchResult.Failed;

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });

            return new SystemLaunchResult
            {
                Succeeded = true,
                ProcessId = TryGetProcessId(process),
                WindowHandle = TryGetMainWindowHandle(process)
            };
        }
        catch
        {
            return SystemLaunchResult.Failed;
        }
    }

    private static int? TryGetProcessId(Process? process)
    {
        if (process == null)
            return null;

        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    private static nint TryGetMainWindowHandle(Process? process)
    {
        if (process == null)
            return 0;

        try
        {
            process.Refresh();
            return process.MainWindowHandle;
        }
        catch
        {
            return 0;
        }
    }

    private static string NormalizeTarget(MacroNode node)
    {
        var target = node.SystemLaunchTarget?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(target))
            return "";

        if (node.SystemLaunchKind != SystemLaunchKind.Url)
            return target;

        return Uri.TryCreate(target, UriKind.Absolute, out _)
            ? target
            : "https://" + target;
    }
}

public sealed class SystemLaunchResult
{
    public static SystemLaunchResult Failed { get; } = new();

    public bool Succeeded { get; init; }

    public int? ProcessId { get; init; }

    public nint WindowHandle { get; init; }
}
