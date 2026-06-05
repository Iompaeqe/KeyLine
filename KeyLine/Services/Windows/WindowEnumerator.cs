using System;
using System.Collections.Generic;
using System.Text;
using KeyLine.Domain;
using KeyLine.Interop;

namespace KeyLine.Services.Windows;

public static class WindowEnumerator
{
    public static List<TargetWindowInfo> GetVisibleWindows()
    {
        var result = new List<TargetWindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            var sb = new StringBuilder(256);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);

            var title = sb.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(title))
            {
                result.Add(new TargetWindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessId = GetWindowProcessId(hWnd)
                });
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static List<TargetWindowInfo> GetVisibleWindowsForProcess(int processId)
    {
        if (processId <= 0)
            return new List<TargetWindowInfo>();

        return GetVisibleWindows()
            .Where(window => window.ProcessId == processId)
            .ToList();
    }

    private static int GetWindowProcessId(nint handle)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        return processId > int.MaxValue ? 0 : (int)processId;
    }
}
