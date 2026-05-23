using System;
using System.Collections.Generic;
using System.Text;
using KeySpammer.Domain;
using KeySpammer.Interop;

namespace KeySpammer.Services.Windows;

public static class ChildWindowFinder
{
    private static readonly string[] AllowedClassParts =
    {
        "Edit",
        "RichEdit",
        "TextBox",
        "NotepadTextBox",
        "Chrome_RenderWidgetHostHWND"
    };

    public static List<TargetWindowInfo> GetChildWindows(nint parent)
    {
        var result = new List<TargetWindowInfo>();

        NativeMethods.EnumChildWindows(parent, (hWnd, _) =>
        {
            var titleBuilder = new StringBuilder(256);
            NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

            var classBuilder = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, classBuilder, classBuilder.Capacity);

            var title = titleBuilder.ToString().Trim();
            var className = classBuilder.ToString().Trim();

            if (!IsLikelyInputTarget(className))
                return true;

            result.Add(new TargetWindowInfo
            {
                Handle = hWnd,
                Title = $"{className} {title}".Trim()
            });

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsLikelyInputTarget(string className)
    {
        foreach (var part in AllowedClassParts)
        {
            if (className.Contains(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}