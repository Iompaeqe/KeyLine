using System.Diagnostics;
using KeyLine.Domain;

namespace KeyLine.Services.SystemActions;

public static class SystemLaunchService
{
    public static bool TryOpen(MacroNode node)
    {
        var target = NormalizeTarget(node);
        if (string.IsNullOrWhiteSpace(target))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
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
