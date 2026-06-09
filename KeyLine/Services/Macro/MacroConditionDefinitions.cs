using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public sealed record MacroConditionDefinition(
    MacroConditionType Type,
    string Label,
    bool CanInvert);

public static class MacroConditionDefinitions
{
    private static readonly IReadOnlyDictionary<MacroConditionType, MacroConditionDefinition> Definitions =
        new Dictionary<MacroConditionType, MacroConditionDefinition>
        {
            [MacroConditionType.KeyState] = new(MacroConditionType.KeyState, "Key held", false),
            [MacroConditionType.PixelColor] = new(MacroConditionType.PixelColor, "Pixel matches", false),
            [MacroConditionType.RandomChance] = new(MacroConditionType.RandomChance, "Random chance", false),
            [MacroConditionType.LoopContext] = new(MacroConditionType.LoopContext, "Loop context", false),
            [MacroConditionType.TargetWindowFocused] = new(MacroConditionType.TargetWindowFocused, "Window Focused", true),
            [MacroConditionType.WindowExists] = new(MacroConditionType.WindowExists, "Window Exists", true),
            [MacroConditionType.MacroRunning] = new(MacroConditionType.MacroRunning, "Macro Running", true),
            [MacroConditionType.TimePassed] = new(MacroConditionType.TimePassed, "Time Passed", false)
        };

    public static MacroConditionDefinition Get(MacroConditionType type)
    {
        return Definitions.TryGetValue(type, out var definition)
            ? definition
            : Definitions[MacroConditionType.KeyState];
    }

    public static bool CanInvert(MacroConditionType type) => Get(type).CanInvert;

    public static bool ShouldInvert(MacroNode node) =>
        node.ConditionIsInverted && CanInvert(node.ConditionType);
}
