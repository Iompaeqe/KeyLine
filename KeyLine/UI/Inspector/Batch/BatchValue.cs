using System.Collections.Generic;

namespace KeyLine.UI.Inspector.Batch;

/// <summary>
/// The shared value of a single field across a batch-selected set of items: either a common value
/// (all items agree) or "mixed" (at least two differ). Used by batch inspectors to drive mixed-value
/// indicators and to decide whether a field should be written on commit.
/// </summary>
public readonly struct BatchValue<T>
{
    private BatchValue(bool hasMixedValue, T value)
    {
        HasMixedValue = hasMixedValue;
        Value = value;
    }

    public bool HasMixedValue { get; }

    /// <summary>The common value when <see cref="HasMixedValue"/> is false; otherwise default.</summary>
    public T Value { get; }

    public static BatchValue<T> Common(T value) => new(false, value);
    public static BatchValue<T> Mixed() => new(true, default!);
}

public static class BatchValues
{
    /// <summary>
    /// Reads a field across all items, returning a common value when every item agrees or "mixed"
    /// when they differ. An empty set is treated as mixed (nothing to show).
    /// </summary>
    public static BatchValue<T> Read<TItem, T>(
        IReadOnlyList<TItem> items,
        Func<TItem, T> selector,
        IEqualityComparer<T>? comparer = null)
    {
        if (items.Count == 0)
            return BatchValue<T>.Mixed();

        comparer ??= EqualityComparer<T>.Default;
        var first = selector(items[0]);

        for (var i = 1; i < items.Count; i++)
        {
            if (!comparer.Equals(selector(items[i]), first))
                return BatchValue<T>.Mixed();
        }

        return BatchValue<T>.Common(first);
    }
}
