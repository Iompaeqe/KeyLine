using KeyLine.Domain;

namespace KeyLine.Services.Timeline;

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

    public static bool IsRepeatBoundary(MacroNode node) =>
        node.Type is MacroNodeType.RepeatStart or MacroNodeType.RepeatEnd;

    public static bool IsControlNode(MacroNode node) =>
        IsRepeatBoundary(node);

    public static List<MacroNode> GetSelectionNodesForStep(MacroTimeline timeline, MacroNode node)
    {
        return TryGetRepeatBlockRange(timeline, node, out var range)
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
            if (TryGetRepeatBlockRange(timeline, node, out var range))
            {
                selectedSet.UnionWith(range);
                continue;
            }

            selectedSet.Add(node);
        }

        return OrderByTimeline(timeline, selectedSet);
    }

    public static bool TryGetRepeatBlockRange(
        MacroTimeline timeline,
        MacroNode boundary,
        out List<MacroNode> range)
    {
        range = new List<MacroNode>();

        if (!IsRepeatBoundary(boundary))
            return false;

        var nodes = timeline.Nodes.ToList();
        var boundaryIndex = nodes.IndexOf(boundary);
        if (boundaryIndex < 0)
            return false;

        var pairMap = BuildRepeatPairMap(nodes);
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

    public static Dictionary<int, int> BuildRepeatPairMap(IReadOnlyList<MacroNode> nodes)
    {
        var pairMap = new Dictionary<int, int>();
        var keyedStartIndexes = new Dictionary<string, Stack<int>>(StringComparer.Ordinal);
        var anonymousStartIndexes = new Stack<int>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            switch (node.Type)
            {
                case MacroNodeType.RepeatStart:
                    var startBlockId = NormalizeBlockId(node.RepeatBlockId);
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

                case MacroNodeType.RepeatEnd:
                    var endBlockId = NormalizeBlockId(node.RepeatBlockId);
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
