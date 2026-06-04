using KeyLine.Domain;
using KeyLine.Services.Input;

namespace KeyLine.Services.Timeline;

public enum TimelineBlockKind
{
    Repeat,
    Condition
}

public sealed record TimelineBlockRange(
    TimelineBlockKind Kind,
    int StartIndex,
    int EndIndex,
    MacroNode Start,
    MacroNode End);

public static class TimelineBlockService
{
    public const int DefaultRepeatCount = 2;

    public static (MacroNode Start, MacroNode End) CreateRepeatBlock(int repeatCount = DefaultRepeatCount)
    {
        var blockId = Guid.NewGuid().ToString("N");
        var start = new MacroNode
        {
            Type = MacroNodeType.RepeatStart,
            RepeatBlockId = blockId,
            RepeatCount = Math.Max(1, repeatCount)
        };
        var end = new MacroNode
        {
            Type = MacroNodeType.RepeatEnd,
            RepeatBlockId = blockId
        };

        return (start, end);
    }

    public static (MacroNode Start, MacroNode End) CreateConditionBlock()
    {
        var blockId = Guid.NewGuid().ToString("N");
        var start = new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionBlockId = blockId,
            ConditionType = MacroConditionType.KeyState,
            ConditionKeyName = "Shift",
            ConditionVirtualKey = 0x10,
            ConditionShortcutKeys = ConditionInputGesture.Serialize(new[] { 0x10 })
        };
        var end = new MacroNode
        {
            Type = MacroNodeType.ConditionEnd,
            ConditionBlockId = blockId
        };

        return (start, end);
    }

    public static bool IsRepeatBoundary(MacroNode node) =>
        node.Type is MacroNodeType.RepeatStart or MacroNodeType.RepeatEnd;

    public static bool IsConditionBoundary(MacroNode node) =>
        node.Type is MacroNodeType.ConditionStart or MacroNodeType.ConditionEnd;

    public static bool IsBlockBoundary(MacroNode node) =>
        IsRepeatBoundary(node) || IsConditionBoundary(node);

    public static bool IsBlockEnd(MacroNode node) =>
        node.Type is MacroNodeType.RepeatEnd or MacroNodeType.ConditionEnd;

    public static bool IsControlNode(MacroNode node) =>
        IsBlockBoundary(node);

    public static List<MacroNode> GetSelectionNodesForStep(MacroTimeline timeline, MacroNode node)
    {
        return TryGetBlockRange(timeline, node, out var range)
            ? range
            : new List<MacroNode> { node };
    }

    public static List<MacroNode> ExpandSelectionToFullBlocks(
        MacroTimeline timeline,
        IEnumerable<MacroNode> selectedNodes)
    {
        var selectedSet = new HashSet<MacroNode>();

        foreach (var node in selectedNodes)
        {
            if (TryGetBlockRange(timeline, node, out var range))
            {
                selectedSet.UnionWith(range);
                continue;
            }

            selectedSet.Add(node);
        }

        return OrderByTimeline(timeline, selectedSet);
    }

    public static bool TryGetBlockRange(
        MacroTimeline timeline,
        MacroNode boundary,
        out List<MacroNode> range)
    {
        if (TryGetRepeatBlockRange(timeline, boundary, out range))
            return true;

        return TryGetConditionBlockRange(timeline, boundary, out range);
    }

    public static bool TryGetRepeatBlockRange(
        MacroTimeline timeline,
        MacroNode boundary,
        out List<MacroNode> range)
    {
        return TryGetBlockRange(
            timeline,
            boundary,
            IsRepeatBoundary,
            BuildRepeatPairMap,
            out range);
    }

    public static bool TryGetConditionBlockRange(
        MacroTimeline timeline,
        MacroNode boundary,
        out List<MacroNode> range)
    {
        return TryGetBlockRange(
            timeline,
            boundary,
            IsConditionBoundary,
            BuildConditionPairMap,
            out range);
    }

    public static Dictionary<int, int> BuildRepeatPairMap(IReadOnlyList<MacroNode> nodes)
    {
        return BuildPairMap(
            nodes,
            MacroNodeType.RepeatStart,
            MacroNodeType.RepeatEnd,
            node => node.RepeatBlockId);
    }

    public static Dictionary<int, int> BuildConditionPairMap(IReadOnlyList<MacroNode> nodes)
    {
        return BuildPairMap(
            nodes,
            MacroNodeType.ConditionStart,
            MacroNodeType.ConditionEnd,
            node => node.ConditionBlockId);
    }

    public static List<TimelineBlockRange> BuildBlockRanges(IReadOnlyList<MacroNode> nodes)
    {
        var ranges = new List<TimelineBlockRange>();

        AddRanges(TimelineBlockKind.Repeat, BuildRepeatPairMap(nodes));
        AddRanges(TimelineBlockKind.Condition, BuildConditionPairMap(nodes));

        return ranges
            .OrderBy(range => range.StartIndex)
            .ThenByDescending(range => range.EndIndex)
            .ToList();

        void AddRanges(TimelineBlockKind kind, Dictionary<int, int> pairMap)
        {
            foreach (var pair in pairMap)
            {
                if (pair.Key >= pair.Value)
                    continue;

                ranges.Add(new TimelineBlockRange(
                    kind,
                    pair.Key,
                    pair.Value,
                    nodes[pair.Key],
                    nodes[pair.Value]));
            }
        }
    }

    private static bool TryGetBlockRange(
        MacroTimeline timeline,
        MacroNode boundary,
        Func<MacroNode, bool> isBoundary,
        Func<IReadOnlyList<MacroNode>, Dictionary<int, int>> buildPairMap,
        out List<MacroNode> range)
    {
        range = new List<MacroNode>();

        if (!isBoundary(boundary))
            return false;

        var nodes = timeline.Nodes.ToList();
        var boundaryIndex = nodes.IndexOf(boundary);
        if (boundaryIndex < 0)
            return false;

        var pairMap = buildPairMap(nodes);
        if (!pairMap.TryGetValue(boundaryIndex, out var pairIndex))
            return false;

        var startIndex = Math.Min(boundaryIndex, pairIndex);
        var endIndex = Math.Max(boundaryIndex, pairIndex);
        range = nodes
            .Skip(startIndex)
            .Take(endIndex - startIndex + 1)
            .ToList();

        return range.Count > 0;
    }

    private static Dictionary<int, int> BuildPairMap(
        IReadOnlyList<MacroNode> nodes,
        MacroNodeType startType,
        MacroNodeType endType,
        Func<MacroNode, string> getBlockId)
    {
        var pairMap = new Dictionary<int, int>();
        var keyedStartIndexes = new Dictionary<string, Stack<int>>(StringComparer.Ordinal);
        var anonymousStartIndexes = new Stack<int>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            switch (node.Type)
            {
                case var type when type == startType:
                    var startBlockId = NormalizeBlockId(getBlockId(node));
                    if (startBlockId.Length == 0)
                    {
                        anonymousStartIndexes.Push(i);
                    }
                    else
                    {
                        if (!keyedStartIndexes.TryGetValue(startBlockId, out var starts))
                        {
                            starts = new Stack<int>();
                            keyedStartIndexes[startBlockId] = starts;
                        }

                        starts.Push(i);
                    }
                    break;

                case var type when type == endType:
                    var endBlockId = NormalizeBlockId(getBlockId(node));
                    if (endBlockId.Length == 0)
                    {
                        PairWithStart(anonymousStartIndexes, i);
                    }
                    else if (keyedStartIndexes.TryGetValue(endBlockId, out var starts))
                    {
                        PairWithStart(starts, i);
                    }
                    break;
            }
        }

        return pairMap;

        void PairWithStart(Stack<int> starts, int endIndex)
        {
            if (starts.Count == 0)
                return;

            var startIndex = starts.Pop();
            pairMap[startIndex] = endIndex;
            pairMap[endIndex] = startIndex;
        }
    }

    private static List<MacroNode> OrderByTimeline(MacroTimeline timeline, IEnumerable<MacroNode> nodes)
    {
        var rawOrder = timeline.Nodes
            .Select((step, index) => (step, index))
            .ToDictionary(item => item.step, item => item.index);

        return nodes
            .Distinct()
            .OrderBy(step => rawOrder.TryGetValue(step, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private static string NormalizeBlockId(string? blockId) =>
        string.IsNullOrWhiteSpace(blockId) ? "" : blockId.Trim();
}
