using KeyLine.Interop;

namespace KeyLine.Services.Input;

public static class InputMessageSender
{
    private static readonly int[] ModifierKeys =
    {
        NativeMethods.VK_SHIFT,
        NativeMethods.VK_CONTROL,
        NativeMethods.VK_MENU,
        NativeMethods.VK_LSHIFT,
        NativeMethods.VK_RSHIFT,
        NativeMethods.VK_LCONTROL,
        NativeMethods.VK_RCONTROL,
        NativeMethods.VK_LMENU,
        NativeMethods.VK_RMENU
    };

    public static bool IsModifierKey(int virtualKey) =>
        virtualKey is NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU
            or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT
            or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL
            or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU;

    public static void SendKeyDown(nint hwnd, int virtualKey, bool neutralizeModifiers = true)
    {
        if (neutralizeModifiers && !IsModifierKey(virtualKey))
            SendModifierKeyUps(hwnd);

        var scanCode = NativeMethods.MapVirtualKey((uint)virtualKey, 0);
        var lParam = 1 | ((int)scanCode << 16);

        NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYDOWN, virtualKey, lParam);
    }

    public static void SendKeyUp(nint hwnd, int virtualKey)
    {
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_KEYUP, virtualKey, MakeKeyUpLParam(virtualKey));
    }

    public static void SendKeyPress(nint hwnd, int virtualKey)
    {
        SendKeyDown(hwnd, virtualKey);
        SendKeyUp(hwnd, virtualKey);
    }

    public static bool TrySendCharacter(nint hwnd, int virtualKey)
    {
        var ch = ToLowerInvariantCharacter(virtualKey);
        if (ch == null)
            return false;

        NativeMethods.PostMessage(hwnd, NativeMethods.WM_CHAR, ch.Value, 0);
        return true;
    }

    public static void SendModifierKeyUps(nint hwnd)
    {
        foreach (var virtualKey in ModifierKeys)
            SendKeyUp(hwnd, virtualKey);

        NativeMethods.PostMessage(
            hwnd,
            NativeMethods.WM_SYSKEYUP,
            NativeMethods.VK_MENU,
            MakeKeyUpLParam(NativeMethods.VK_MENU));
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

    public static void SendForegroundMouseClick()
    {
        SendForegroundMouseDown(1);
        Thread.Sleep(8);
        SendForegroundMouseUp(1);
    }

    public static void SendForegroundMouseDown(int mouseButton)
    {
        var (flags, data) = GetMouseEvent(mouseButton, isDown: true);
        NativeMethods.mouse_event(flags, 0, 0, data, UIntPtr.Zero);
    }

    public static void SendForegroundMouseUp(int mouseButton)
    {
        var (flags, data) = GetMouseEvent(mouseButton, isDown: false);
        NativeMethods.mouse_event(flags, 0, 0, data, UIntPtr.Zero);
    }

    public static void MoveCursorToClientPoint(nint hwnd, int x, int y)
    {
        var point = new NativeMethods.POINT
        {
            X = x,
            Y = y
        };

        if (NativeMethods.ClientToScreen(hwnd, ref point))
            NativeMethods.SetCursorPos(point.X, point.Y);
    }

    private static nint MakeMouseLParam(int x, int y)
    {
        var low = (ushort)(short)x;
        var high = (ushort)(short)y;
        return low | (high << 16);
    }

    private static (uint Flags, uint Data) GetMouseEvent(int mouseButton, bool isDown)
    {
        return Math.Clamp(mouseButton, 1, 5) switch
        {
            2 => (isDown ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP, 0),
            3 => (isDown ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP, 0),
            4 => (isDown ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP, NativeMethods.XBUTTON1),
            5 => (isDown ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP, NativeMethods.XBUTTON2),
            _ => (isDown ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP, 0)
        };
    }

    private static nint MakeKeyUpLParam(int virtualKey)
    {
        var scanCode = NativeMethods.MapVirtualKey((uint)virtualKey, 0);
        return 1 | ((int)scanCode << 16) | (1 << 30) | unchecked((int)0x80000000);
    }

    private static int? ToLowerInvariantCharacter(int virtualKey)
    {
        if (virtualKey is >= 'A' and <= 'Z')
            return char.ToLowerInvariant((char)virtualKey);

        if (virtualKey is >= '0' and <= '9')
            return virtualKey;

        return virtualKey switch
        {
            0x20 => ' ',
            0xBA => ';',
            0xBB => '=',
            0xBC => ',',
            0xBD => '-',
            0xBE => '.',
            0xBF => '/',
            0xC0 => '`',
            0xDB => '[',
            0xDC => '\\',
            0xDD => ']',
            0xDE => '\'',
            _ => null
        };
    }
}
