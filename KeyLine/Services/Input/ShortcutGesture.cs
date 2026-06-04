using System.Windows.Input;
using KeyLine.Interop;

namespace KeyLine.Services.Input;

public static class ShortcutGesture
{
    public const int MaxKeyCount = 4;

    public static int NormalizeVirtualKey(int virtualKey)
    {
        return virtualKey switch
        {
            NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT => NativeMethods.VK_SHIFT,
            NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL => NativeMethods.VK_CONTROL,
            NativeMethods.VK_LMENU or NativeMethods.VK_RMENU => NativeMethods.VK_MENU,
            _ => virtualKey
        };
    }

    public static string Serialize(IEnumerable<int> virtualKeys)
    {
        return string.Join(",", virtualKeys
            .Select(NormalizeVirtualKey)
            .Where(key => key > 0)
            .Distinct()
            .Take(MaxKeyCount));
    }

    public static int[] Parse(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return Array.Empty<int>();

        return shortcut
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var key) ? NormalizeVirtualKey(key) : 0)
            .Where(key => key > 0)
            .Distinct()
            .Take(MaxKeyCount)
            .ToArray();
    }

    public static string Format(string shortcut)
    {
        return Format(Parse(shortcut));
    }

    public static string Format(IEnumerable<int> virtualKeys)
    {
        var names = virtualKeys
            .Select(NormalizeVirtualKey)
            .Where(key => key > 0)
            .Distinct()
            .Take(MaxKeyCount)
            .Select(GetDisplayName)
            .ToArray();

        return names.Length == 0
            ? "no shortcut"
            : string.Join(" + ", names);
    }

    public static bool Matches(IReadOnlySet<int> pressedKeys, IReadOnlyList<int> shortcutKeys)
    {
        return shortcutKeys.Count > 0 &&
               pressedKeys.Count == shortcutKeys.Count &&
               shortcutKeys.All(pressedKeys.Contains);
    }

    private static string GetDisplayName(int virtualKey)
    {
        return virtualKey switch
        {
            NativeMethods.VK_CONTROL => "Ctrl",
            NativeMethods.VK_SHIFT => "Shift",
            NativeMethods.VK_MENU => "Alt",
            NativeMethods.VK_XBUTTON1 => "Mouse4",
            NativeMethods.VK_XBUTTON2 => "Mouse5",
            _ => KeyNameDisplayRules.GetName(KeyInterop.KeyFromVirtualKey(virtualKey))
        };
    }
}
