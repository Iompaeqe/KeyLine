namespace KeySpammer.Core;

public sealed class TargetWindowInfo
{
    public nint Handle { get; init; }
    public string Title { get; init; } = "";

    public override string ToString()
    {
        return $"{Title} — 0x{Handle:X}";
    }
}