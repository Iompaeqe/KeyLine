using MacroSpammer.Interop;

namespace MacroSpammer.Services.Input;

public static class InputMessageSender
{
    public static void SendKeyDown(nint hwnd, int virtualKey)
    {
        var scanCode = NativeMethods.MapVirtualKey((uint)virtualKey, 0);
        var lParam = 1 | ((int)scanCode << 16);

        NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYDOWN, virtualKey, lParam);
    }

    public static void SendKeyUp(nint hwnd, int virtualKey)
    {
        var scanCode = NativeMethods.MapVirtualKey((uint)virtualKey, 0);
        var lParam = 1 | ((int)scanCode << 16) | (1 << 30) | unchecked((int)0x80000000);

        NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYUP, virtualKey, lParam);
    }

    public static void SendKeyPress(nint hwnd, int virtualKey)
    {
        SendKeyDown(hwnd, virtualKey);
        SendKeyUp(hwnd, virtualKey);
    }

    public static void SendText(nint hwnd, string text)
    {
        foreach (var ch in text)
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CHAR, ch, 0);
        }
    }

    public static void SendMouseDown(nint hwnd, int x, int y)
    {
        var lParam = MakeMouseLParam(x, y);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_MOUSEMOVE, 0, lParam);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONDOWN, NativeMethods.MK_LBUTTON, lParam);
    }

    public static void SendMouseUp(nint hwnd, int x, int y)
    {
        var lParam = MakeMouseLParam(x, y);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_MOUSEMOVE, NativeMethods.MK_LBUTTON, lParam);
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_LBUTTONUP, 0, lParam);
    }

    public static void SendMouseClick(nint hwnd, int x, int y)
    {
        SendMouseDown(hwnd, x, y);
        SendMouseUp(hwnd, x, y);
    }

    private static nint MakeMouseLParam(int x, int y)
    {
        var low = (ushort)(short)x;
        var high = (ushort)(short)y;
        return low | (high << 16);
    }
}
