using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void MoveStepBeforeRawAnchor(
        MacroTimeline timeline,
        MacroStep draggedStep,
        MacroStep? rawInsertAnchor)
    {
        var draggedItems = GetRawStepsForDisplayStep(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        if (rawInsertAnchor != null && draggedItems.Contains(rawInsertAnchor))
            return;

        var oldFirstIndex = timeline.Steps.IndexOf(draggedItems[0]);

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? timeline.Steps.Count
            : timeline.Steps.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = timeline.Steps.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return;

        RemoveDraggedItems(timeline, draggedItems);

        var insertIndex = rawInsertAnchor == null
            ? timeline.Steps.Count
            : timeline.Steps.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = timeline.Steps.Count;

        InsertDraggedItems(timeline, insertIndex, draggedItems);

        _selection.SelectStep(timeline, draggedStep);
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private static void RemoveDraggedItems(MacroTimeline timeline, IEnumerable<MacroStep> draggedItems)
    {
        foreach (var item in draggedItems)
            timeline.Steps.Remove(item);
    }

    private static void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroStep> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Steps.Insert(insertIndex + i, draggedItems[i]);
    }

    private static List<MacroStep> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(sourceStep => timeline.Steps.Contains(sourceStep))
                .ToList();
        }

        return timeline.Steps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }
}
