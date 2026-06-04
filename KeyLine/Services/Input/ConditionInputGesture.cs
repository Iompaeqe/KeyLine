using KeyLine.Domain;

namespace KeyLine.Services.Input;

public static class ConditionInputGesture
{
    public const int MaxKeyCount = 3;

    public static string GetGesture(MacroNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ConditionShortcutKeys))
            return Serialize(Parse(node.ConditionShortcutKeys));

        return node.ConditionVirtualKey > 0
            ? Serialize(new[] { node.ConditionVirtualKey })
            : "";
    }

    public static int[] GetVirtualKeys(MacroNode node)
    {
        return Parse(GetGesture(node));
    }

    public static string Serialize(IEnumerable<int> virtualKeys)
    {
        return string.Join(",", virtualKeys
            .Select(ShortcutGesture.NormalizeVirtualKey)
            .Where(key => key > 0)
            .Distinct()
            .Take(MaxKeyCount));
    }

    public static int[] Parse(string shortcut)
    {
        return ShortcutGesture.Parse(shortcut)
            .Take(MaxKeyCount)
            .ToArray();
    }

    public static string Format(MacroNode node)
    {
        return Format(GetGesture(node));
    }

    public static string Format(string shortcut)
    {
        var keys = Parse(shortcut);
        return keys.Length == 0
            ? "no input"
            : ShortcutGesture.Format(keys);
    }
}
