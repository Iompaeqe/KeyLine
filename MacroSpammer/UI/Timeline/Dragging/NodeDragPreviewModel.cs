using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer.UI.Timeline;

public sealed class NodeDragPreviewModel
{
    private readonly List<NodePreviewSlot> _slots = new();
    private readonly List<MacroNode> _previewRawSteps = new();
    private readonly List<MacroNode> _draggedRawItems = new();

    private Point? _lastMousePoint;

    public IReadOnlyList<NodePreviewSlot> Slots => _slots;
    public IReadOnlyList<MacroNode> PreviewRawSteps => _previewRawSteps;
    public IReadOnlyList<MacroNode> DraggedRawItems => _draggedRawItems;

    public bool HasPreviewRawSteps => _previewRawSteps.Count > 0;
    public bool HasSlots => _slots.Count > 0;

    public void Begin(
        MacroTimeline timeline,
        IReadOnlyCollection<MacroNode> rawNodes,
        IReadOnlyList<MacroNode> draggedItems,
        MacroNode? draggedNode,
        Func<MacroNode, double> measureNodeWidth,
        double firstItemLeft,
        double itemGap)
    {
        Clear();

        if (draggedItems.Count == 0)
            return;

        _draggedRawItems.AddRange(draggedItems);
        _slots.AddRange(BuildSlots(
            timeline,
            rawNodes,
            draggedItems,
            draggedNode,
            measureNodeWidth,
            firstItemLeft,
            itemGap));

        SyncPreviewRawSteps();
    }

    public bool UpdateFromMouse(Point currentPoint, double moveEpsilon, double firstItemLeft, double itemGap)
    {
        if (_lastMousePoint.HasValue)
        {
            var last = _lastMousePoint.Value;
            if (Math.Abs(currentPoint.X - last.X) < moveEpsilon &&
                Math.Abs(currentPoint.Y - last.Y) < moveEpsilon)
            {
                return false;
            }
        }

        _lastMousePoint = currentPoint;
        return UpdateOrderFromMouse(currentPoint.X, firstItemLeft, itemGap);
    }

    public MacroNode? GetRawInsertAnchor()
    {
        if (_slots.Count == 0)
            return null;

        var lastDraggedSlotIndex = -1;
        for (var i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].IsDraggedSlot)
                lastDraggedSlotIndex = i;
        }

        if (lastDraggedSlotIndex < 0)
            return null;

        for (var i = lastDraggedSlotIndex + 1; i < _slots.Count; i++)
        {
            var nextRawItem = _slots[i].RawItems.FirstOrDefault(rawItem => !_draggedRawItems.Contains(rawItem));
            if (nextRawItem != null)
                return nextRawItem;
        }

        return null;
    }

    public void Clear()
    {
        _slots.Clear();
        _previewRawSteps.Clear();
        _draggedRawItems.Clear();
        _lastMousePoint = null;
    }

    private static List<NodePreviewSlot> BuildSlots(
        MacroTimeline timeline,
        IReadOnlyCollection<MacroNode> rawNodes,
        IReadOnlyCollection<MacroNode> draggedItems,
        MacroNode? draggedNode,
        Func<MacroNode, double> measureNodeWidth,
        double firstItemLeft,
        double itemGap)
    {
        var visibleNodes = MacroTimelineBuilder.BuildVisibleSteps(
            rawNodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var slots = new List<NodePreviewSlot>();
        foreach (var displayNode in visibleNodes)
        {
            var rawItems = TimelineNodeMutationService.GetRawStepsForDisplayStep(rawNodes, displayNode);
            if (rawItems.Count == 0)
                continue;

            var measuredNode = rawItems.Any(draggedItems.Contains) && draggedNode != null
                ? draggedNode
                : displayNode;

            slots.Add(new NodePreviewSlot
            {
                DisplayNode = displayNode,
                RawItems = rawItems,
                Width = Math.Max(1, measureNodeWidth(measuredNode)),
                IsDraggedSlot = rawItems.Any(draggedItems.Contains),
                Left = 0
            });
        }

        UpdateSlotPositions(slots, firstItemLeft, itemGap);
        return slots;
    }

    private bool UpdateOrderFromMouse(double mouseX, double firstItemLeft, double itemGap)
    {
        if (_slots.Count == 0 || _draggedRawItems.Count == 0)
            return false;

        UpdateSlotPositions(_slots, firstItemLeft, itemGap);

        var draggedSlotIndex = FindDraggedDisplaySlotIndex(_slots);
        if (draggedSlotIndex < 0)
            return false;

        if (ShouldMoveDraggedSlotLeft(_slots, draggedSlotIndex, mouseX))
            return MoveDraggedSlotsLeft(firstItemLeft, itemGap);

        if (ShouldMoveDraggedSlotRight(_slots, draggedSlotIndex, mouseX))
            return MoveDraggedSlotsRight(firstItemLeft, itemGap);

        return false;
    }

    private static void UpdateSlotPositions(IList<NodePreviewSlot> slots, double firstItemLeft, double itemGap)
    {
        var currentLeft = firstItemLeft;
        foreach (var slot in slots)
        {
            slot.Left = currentLeft;
            currentLeft += slot.Width + itemGap;
        }
    }

    private static bool ShouldMoveDraggedSlotLeft(IReadOnlyList<NodePreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex > 0 && mouseX < slots[draggedSlotIndex - 1].CenterX;
    }

    private static bool ShouldMoveDraggedSlotRight(IReadOnlyList<NodePreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex < slots.Count - 1 && mouseX > slots[draggedSlotIndex + 1].CenterX;
    }

    private int FindDraggedDisplaySlotIndex(IReadOnlyList<NodePreviewSlot> slots)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsDraggedSlot)
                return i;
        }

        return -1;
    }

    private bool MoveDraggedSlotsLeft(double firstItemLeft, double itemGap)
    {
        var changed = false;

        for (var i = 1; i < _slots.Count; i++)
        {
            if (!_slots[i].IsDraggedSlot || _slots[i - 1].IsDraggedSlot)
                continue;

            (_slots[i - 1], _slots[i]) = (_slots[i], _slots[i - 1]);
            changed = true;
        }

        if (!changed)
            return false;

        UpdateSlotPositions(_slots, firstItemLeft, itemGap);
        SyncPreviewRawSteps();
        return true;
    }

    private bool MoveDraggedSlotsRight(double firstItemLeft, double itemGap)
    {
        var changed = false;

        for (var i = _slots.Count - 2; i >= 0; i--)
        {
            if (!_slots[i].IsDraggedSlot || _slots[i + 1].IsDraggedSlot)
                continue;

            (_slots[i], _slots[i + 1]) = (_slots[i + 1], _slots[i]);
            changed = true;
        }

        if (!changed)
            return false;

        UpdateSlotPositions(_slots, firstItemLeft, itemGap);
        SyncPreviewRawSteps();
        return true;
    }

    private void SyncPreviewRawSteps()
    {
        _previewRawSteps.Clear();

        foreach (var slot in _slots)
        {
            foreach (var rawItem in slot.RawItems)
            {
                if (!_previewRawSteps.Contains(rawItem))
                    _previewRawSteps.Add(rawItem);
            }
        }
    }
}
