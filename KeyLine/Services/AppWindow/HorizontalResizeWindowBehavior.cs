using System;
using System.Windows;
using System.Windows.Interop;

namespace KeyLine.Services.AppWindow;

public sealed class HorizontalResizeWindowBehavior
{
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;

    private readonly Window _window;
    private readonly double _resizeBorderWidth;

    private HorizontalResizeWindowBehavior(Window window, double resizeBorderWidth)
    {
        _window = window;
        _resizeBorderWidth = resizeBorderWidth;
    }

    public static void Attach(Window window, double resizeBorderWidth = 8)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        if (source == null)
            return;

        var behavior = new HorizontalResizeWindowBehavior(window, resizeBorderWidth);
        source.AddHook(behavior.WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest)
            return IntPtr.Zero;

        var mouseScreenX = GetXLParam(lParam);
        var mouseScreenY = GetYLParam(lParam);
        var mouseWindowPos = _window.PointFromScreen(new Point(mouseScreenX, mouseScreenY));

        if (mouseWindowPos.X <= _resizeBorderWidth)
        {
            handled = true;
            return new IntPtr(HtLeft);
        }

        if (mouseWindowPos.X >= _window.ActualWidth - _resizeBorderWidth)
        {
            handled = true;
            return new IntPtr(HtRight);
        }

        return IntPtr.Zero;
    }

    private static int GetXLParam(IntPtr lParam)
    {
        return unchecked((short)(long)lParam);
    }

    private static int GetYLParam(IntPtr lParam)
    {
        return unchecked((short)((long)lParam >> 16));
    }
}
