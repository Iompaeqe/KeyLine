using KeyLine.Services.Timeline;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// The single, UI-free decision for whether a numeric/delay inspector field should write a value and,
/// if so, what value — shared by every inspector field (single or batch). Keeping this pure makes the
/// commit rules unit-testable and guarantees every field follows the same logic:
/// <list type="bullet">
/// <item>Unparseable text on a mixed field never writes (the field stays mixed).</item>
/// <item>Unparseable text on a normal field falls back to a defined default, then clamps.</item>
/// <item>A value equal to the current (non-mixed) value never writes.</item>
/// </list>
/// </summary>
public static class InspectorNumberCommit
{
    /// <summary>Decision for a plain integer field clamped to <paramref name="min"/>..<paramref name="max"/>.</summary>
    public static NumberCommitResult Decide(string text, BatchValue<int> batch, int min, int? max)
    {
        if (!int.TryParse(text, out var value))
        {
            // Empty / non-numeric while mixed means "leave it mixed" — never normalize a mixed field.
            if (batch.HasMixedValue)
                return NumberCommitResult.NoWrite();

            value = min;
        }

        value = Math.Max(min, value);
        if (max.HasValue)
            value = Math.Min(max.Value, value);

        if (!batch.HasMixedValue && batch.Value == value)
            return NumberCommitResult.NoWrite();

        return NumberCommitResult.Write(value);
    }

    /// <summary>Decision for a delay field expressed in milliseconds, clamped via <see cref="DelayFormatter"/>.</summary>
    public static NumberCommitResult DecideDelay(string text, BatchValue<int> batch)
    {
        if (!long.TryParse(text, out var raw))
        {
            if (batch.HasMixedValue)
                return NumberCommitResult.NoWrite();

            raw = string.IsNullOrWhiteSpace(text) ? 0 : DelayFormatter.MaxMilliseconds;
        }

        var clamped = DelayFormatter.ClampMilliseconds(raw);
        if (!batch.HasMixedValue && batch.Value == clamped)
            return NumberCommitResult.NoWrite();

        return NumberCommitResult.Write(clamped);
    }
}

/// <summary>Outcome of <see cref="InspectorNumberCommit"/>: whether to write, and the clamped value to write.</summary>
public readonly struct NumberCommitResult
{
    private NumberCommitResult(bool shouldWrite, int value)
    {
        ShouldWrite = shouldWrite;
        Value = value;
    }

    public bool ShouldWrite { get; }
    public int Value { get; }

    public static NumberCommitResult Write(int value) => new(true, value);
    public static NumberCommitResult NoWrite() => new(false, 0);
}
