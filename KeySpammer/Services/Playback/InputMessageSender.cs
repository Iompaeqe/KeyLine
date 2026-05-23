using KeySpammer.Interop;

namespace KeySpammer.Services.Playback;

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
}