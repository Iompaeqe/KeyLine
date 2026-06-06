using KeyLine.Domain;
using KeyLine.Interop;

namespace KeyLine.Services.Input;

public static class ToggleKeyService
{
    public static bool IsToggleKey(int virtualKey) =>
        virtualKey is NativeMethods.VK_CAPITAL or NativeMethods.VK_NUMLOCK or NativeMethods.VK_SCROLL;

    public static bool IsToggleOn(int virtualKey) =>
        IsToggleKey(virtualKey) && (NativeMethods.GetKeyState(virtualKey) & 0x0001) != 0;

    public static void Execute(int virtualKey, ToggleKeyMode mode)
    {
        if (!IsToggleKey(virtualKey) || mode == ToggleKeyMode.Normal)
            return;

        switch (mode)
        {
            case ToggleKeyMode.Toggle:
                SendToggleKeyPress(virtualKey);
                break;

            case ToggleKeyMode.ToggleOn:
                if (!IsToggleOn(virtualKey))
                    SendToggleKeyPress(virtualKey);
                break;

            case ToggleKeyMode.ToggleOff:
                if (IsToggleOn(virtualKey))
                    SendToggleKeyPress(virtualKey);
                break;
        }
    }

    private static void SendToggleKeyPress(int virtualKey)
    {
        NativeMethods.keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)virtualKey, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
