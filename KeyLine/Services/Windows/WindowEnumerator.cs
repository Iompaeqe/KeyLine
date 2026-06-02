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
                    Title = title
                });
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }
}