using System.Collections.ObjectModel;

namespace KeyLine.Domain;

public sealed class MacroDocument
{
    public ObservableCollection<MacroTimeline> Timelines { get; } = new();

    public int ActiveTimelineIndex { get; private set; }

    public MacroTimeline ActiveTimeline
    {
        get
        {
            EnsureTimeline();
            return Timelines[ActiveTimelineIndex];
        }
    }

    public MacroDocument()
    {
        EnsureTimeline();
    }

    public void EnsureTimeline()
    {
        if (Timelines.Count == 0)
            Timelines.Add(CreateTimeline());

        if (ActiveTimelineIndex < 0)
            ActiveTimelineIndex = 0;

        if (ActiveTimelineIndex >= Timelines.Count)
            ActiveTimelineIndex = Timelines.Count - 1;
    }

    public MacroTimeline AddTimeline()
    {
        var timeline = CreateTimeline();
        Timelines.Add(timeline);
        ActiveTimelineIndex = Timelines.Count - 1;
        return timeline;
    }

    public void SelectTimeline(MacroTimeline timeline)
    {
        var index = Timelines.IndexOf(timeline);
        if (index >= 0)
            ActiveTimelineIndex = index;
    }

    public void SelectTimeline(int index)
    {
        if (index < 0 || index >= Timelines.Count)
            return;

        ActiveTimelineIndex = index;
    }

    public void SelectNextTimeline()
    {
        if (Timelines.Count == 0)
            return;

        ActiveTimelineIndex = (ActiveTimelineIndex + 1) % Timelines.Count;
    }

    public void SelectPreviousTimeline()
    {
        if (Timelines.Count == 0)
            return;

        ActiveTimelineIndex = (ActiveTimelineIndex - 1 + Timelines.Count) % Timelines.Count;
    }

    public void RemoveTimeline(MacroTimeline timeline)
    {
        var index = Timelines.IndexOf(timeline);
        if (index < 0)
            return;

        if (Timelines.Count == 1)
        {
            timeline.Nodes.Clear();
            ActiveTimelineIndex = 0;
            return;
        }

        Timelines.RemoveAt(index);

        if (ActiveTimelineIndex >= Timelines.Count)
            ActiveTimelineIndex = Timelines.Count - 1;
    }

    public void MoveTimeline(MacroTimeline timeline, int newIndex)
    {
        var oldIndex = Timelines.IndexOf(timeline);
        if (oldIndex < 0)
            return;

        newIndex = Math.Clamp(newIndex, 0, Timelines.Count - 1);

        if (oldIndex == newIndex)
            return;

        Timelines.Move(oldIndex, newIndex);
        ActiveTimelineIndex = newIndex;
    }

    /// <summary>
    /// Replaces the timeline order with <paramref name="newOrder"/>, which must contain exactly the
    /// same timelines. Used by multi-timeline (group) drag reordering. The active timeline is kept by
    /// reference so it stays current after the reorder.
    /// </summary>
    public void ReorderTimelines(IReadOnlyList<MacroTimeline> newOrder)
    {
        if (newOrder.Count != Timelines.Count)
            return;

        var existing = new HashSet<MacroTimeline>(Timelines);
        if (newOrder.Any(timeline => !existing.Contains(timeline)) ||
            newOrder.Distinct().Count() != newOrder.Count)
        {
            return;
        }

        var active = Timelines.Count > 0 ? Timelines[ActiveTimelineIndex] : null;

        Timelines.Clear();
        foreach (var timeline in newOrder)
            Timelines.Add(timeline);

        if (active != null)
            ActiveTimelineIndex = Math.Max(0, Timelines.IndexOf(active));
    }

    private MacroTimeline CreateTimeline()
    {
        return new MacroTimeline
        {
            Name = GetNextTimelineName()
        };
    }

    private string GetNextTimelineName()
    {
        var index = Timelines.Count + 1;
        var usedNames = Timelines
            .Select(timeline => timeline.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (usedNames.Contains($"T{index}"))
            index++;

        return $"T{index}";
    }
}
