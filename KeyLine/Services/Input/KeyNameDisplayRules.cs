using System.Windows.Input;

namespace KeyLine.Services.Input;

public static class KeyNameDisplayRules
{
    private static readonly Dictionary<Key, string> Names = new()
    {
        [Key.D0] = "0",
        [Key.D1] = "1",
        [Key.D2] = "2",
        [Key.D3] = "3",
        [Key.D4] = "4",
        [Key.D5] = "5",
        [Key.D6] = "6",
        [Key.D7] = "7",
        [Key.D8] = "8",
        [Key.D9] = "9",

        [Key.NumPad0] = "Num0",
        [Key.NumPad1] = "Num1",
        [Key.NumPad2] = "Num2",
        [Key.NumPad3] = "Num3",
        [Key.NumPad4] = "Num4",
        [Key.NumPad5] = "Num5",
        [Key.NumPad6] = "Num6",
        [Key.NumPad7] = "Num7",
        [Key.NumPad8] = "Num8",
        [Key.NumPad9] = "Num9"
    };

    public static string GetName(Key key)
    {
        return Names.TryGetValue(key, out var name)
            ? name
            : key.ToString();
    }
}
