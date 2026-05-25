using System.Windows.Input;

namespace MacroSpammer.Services.Input;

public static class VirtualKeyParser
{
    public static bool TryFromWpfKey(Key key, out int virtualKey, out string keyName)
    {
        if (key == Key.System)
            key = Keyboard.PrimaryDevice.ActiveSource == null ? key : key;

        keyName = key.ToString();
        virtualKey = KeyInterop.VirtualKeyFromKey(key);

        return virtualKey > 0;
    }

    public static bool TryFromRecordedKey(KeyEventArgs e, out int virtualKey, out string keyName)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        keyName = key.ToString();
        virtualKey = KeyInterop.VirtualKeyFromKey(key);

        return virtualKey > 0;
    }
}