namespace MacroSpammer.Domain;

public sealed class TargetWindowInfo
{
    public nint Handle { get; init; }
    public string Title { get; init; } = "";

    public override string ToString()
    {
        if (Handle == 0)
            return Title;

        return $"{Title} — 0x{Handle:X}";
    }
}
