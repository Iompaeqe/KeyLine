using System.Collections.Generic;
using System.Linq;
using KeyLine.Domain;

namespace KeyLine.Services.Timeline;

public static class TimelineNodeMutationService
{
    public static List<MacroNode> GetRawStepsForDisplayStep(
        IReadOnlyCollection<MacroNode> rawSteps,
        MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(rawSteps.Contains)
                .ToList();
        }

        return rawSteps.Contains(node)
            ? new List<MacroNode> { node }
            : new List<MacroNode>();
    }

    public static List<MacroNode> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(sourceStep => timeline.Nodes.Contains(sourceStep))
                .ToList();
        }

        if (!timeline.Nodes.Contains(node))
            return new List<MacroNode>();

        var rawSteps = new List<MacroNode> { node };
        AddAttachedStandardDelaySteps(timeline, node, rawSteps);
        return rawSteps
            .Distinct()
            .OrderBy(rawStep => timeline.Nodes.IndexOf(rawStep))
            .ToList();
    }

    public static bool MoveRawStepsBeforeAnchor(
        MacroTimeline timeline,
        IReadOnlyList<MacroNode> draggedItems,
        MacroNode? rawInsertAnchor)
    {
        if (draggedItems.Count == 0)
            return false;

        if (rawInsertAnchor != null && draggedItems.Contains(rawInsertAnchor))
            return false;

        var oldFirstIndex = timeline.Nodes.IndexOf(draggedItems[0]);
        if (oldFirstIndex < 0)
            return false;

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? timeline.Nodes.Count
            : timeline.Nodes.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = timeline.Nodes.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return false;

        RemoveDraggedItems(timeline, draggedItems);

        var insertIndex = rawInsertAnchor == null
            ? timeline.Nodes.Count
            : timeline.Nodes.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = timeline.Nodes.Count;

        InsertDraggedItems(timeline, insertIndex, draggedItems);
        return true;
    }

    public static bool MergeAdjacentDelayNodes(MacroTimeline timeline)
    {
        var changed = false;

        for (var i = 1; i < timeline.Nodes.Count; i++)
        {
            var previous = timeline.Nodes[i - 1];
            var current = timeline.Nodes[i];

            if (previous.Type != MacroNodeType.Delay || current.Type != MacroNodeType.Delay)
                continue;

            previous.DelayMs += current.DelayMs;
            previous.IsRecordedDelay = previous.IsRecordedDelay && current.IsRecordedDelay;
            timeline.Nodes.RemoveAt(i);
            changed = true;
            i--;
        }

        return changed;
    }

    public static List<MacroNode> GetStepsToRemoveForDelete(MacroTimeline timeline, MacroNode node)
    {
        if (TimelineBlockService.TryGetRepeatBlockRange(timeline, node, out var blockRange))
            return blockRange;

        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(timeline.Nodes.Contains)
                .ToList();
        }

        return timeline.Nodes.Contains(node)
            ? new List<MacroNode> { node }
            : new List<MacroNode>();
    }

    public static void AddStandardDelayCleanupSteps(MacroTimeline timeline, List<MacroNode> stepsToRemove)
    {
        if (stepsToRemove.Count == 0)
            return;

        var indexes = stepsToRemove
            .Select(timeline.Nodes.IndexOf)
            .Where(index => index >= 0)
            .Order()
            .ToList();

        if (indexes.Count == 0)
            return;

        var cleanupSteps = GetContiguousDelayStepsBefore(timeline, indexes[0]);
        if (cleanupSteps.Count == 0)
            cleanupSteps = GetContiguousDelayStepsAfter(timeline, indexes[^1]);

        foreach (var cleanupStep in cleanupSteps)
        {
            if (!stepsToRemove.Contains(cleanupStep))
                stepsToRemove.Add(cleanupStep);
        }
    }

    public static bool IsDelayCleanupActionStep(MacroNode node) =>
        !IsDelayCleanupStep(node);

    public static List<MacroNode> GetContiguousDelayStepsBefore(MacroTimeline timeline, int stepIndex)
    {
        var result = new List<MacroNode>();

        for (var i = stepIndex - 1; i >= 0 && IsDelayCleanupStep(timeline.Nodes[i]); i--)
            result.Add(timeline.Nodes[i]);

        return result;
    }

    public static List<MacroNode> GetContiguousDelayStepsAfter(MacroTimeline timeline, int stepIndex)
    {
        var result = new List<MacroNode>();

        for (var i = stepIndex + 1; i < timeline.Nodes.Count && IsDelayCleanupStep(timeline.Nodes[i]); i++)
            result.Add(timeline.Nodes[i]);

        return result;
    }

    private static void AddAttachedStandardDelaySteps(MacroTimeline timeline, MacroNode node, List<MacroNode> rawSteps)
    {
        if (!timeline.UseStandardDelay ||
            node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay ||
            TimelineBlockService.IsControlNode(node))
        {
            return;
        }

        var stepIndex = timeline.Nodes.IndexOf(node);
        if (stepIndex < 0)
            return;

        var delaySteps = GetContiguousDelayStepsBefore(timeline, stepIndex);
        if (delaySteps.Count == 0)
            delaySteps = GetContiguousDelayStepsAfter(timeline, stepIndex);

        foreach (var delayStep in delaySteps)
            rawSteps.Add(delayStep);
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private static void RemoveDraggedItems(MacroTimeline timeline, IEnumerable<MacroNode> draggedItems)
    {
        foreach (var item in draggedItems)
            timeline.Nodes.Remove(item);
    }

    private static void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroNode> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Nodes.Insert(insertIndex + i, draggedItems[i]);
    }

    private static bool IsDelayCleanupStep(MacroNode node) =>
        node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay;
}
