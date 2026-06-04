using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector;

public sealed class InspectorCommitService
{
    private readonly TimelineSelectionState _selection;
    private readonly Func<bool> _canEdit;
    private readonly Action _saveUndoSnapshot;
    private readonly Action _refreshTimeline;
    private readonly Action _refreshTimelineWithoutInspector;
    private readonly Action _refreshInspector;
    private readonly Action _scheduleSaveState;
    private readonly Action<MacroTimeline> _selectTimeline;

    private bool _isCommittingTimelineName;

    public InspectorCommitService(
        TimelineSelectionState selection,
        Func<bool> canEdit,
        Action saveUndoSnapshot,
        Action refreshTimeline,
        Action refreshTimelineWithoutInspector,
        Action refreshInspector,
        Action scheduleSaveState,
        Action<MacroTimeline> selectTimeline)
    {
        _selection = selection;
        _canEdit = canEdit;
        _saveUndoSnapshot = saveUndoSnapshot;
        _refreshTimeline = refreshTimeline;
        _refreshTimelineWithoutInspector = refreshTimelineWithoutInspector;
        _refreshInspector = refreshInspector;
        _scheduleSaveState = scheduleSaveState;
        _selectTimeline = selectTimeline;
    }

    public void CommitNodeChange(Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _refreshTimeline();
        _refreshInspector();
        _scheduleSaveState();
    }

    public void CommitNodeValueChange(Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _refreshTimelineWithoutInspector();
        _scheduleSaveState();
    }

    public void CommitTimelineChange(MacroTimeline timeline, Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _selectTimeline(timeline);
        _refreshTimeline();
        _scheduleSaveState();
    }

    public void CommitTimelineValueChange(MacroTimeline timeline, Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _selection.SelectTimeline(timeline);
        _refreshTimelineWithoutInspector();
        _scheduleSaveState();
    }

    public bool CommitTimelineName(MacroTimeline timeline, string name)
    {
        if (_isCommittingTimelineName || !_canEdit())
            return false;

        var trimmedName = string.IsNullOrWhiteSpace(name) ? timeline.Name : name.Trim();

        _isCommittingTimelineName = true;
        try
        {
            timeline.Name = trimmedName;
            _selection.SelectTimeline(timeline);
            _refreshTimeline();
            _refreshInspector();
            _scheduleSaveState();
            return true;
        }
        finally
        {
            _isCommittingTimelineName = false;
        }
    }

    public static void CommitNumberText(
        TextBox textBox,
        int originalValue,
        Action<int> commit,
        int min,
        int? max,
        bool isRefreshing)
    {
        if (isRefreshing)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            value = min;

        value = Math.Max(min, value);
        if (max.HasValue)
            value = Math.Min(max.Value, value);

        textBox.Text = value.ToString();

        if (value != originalValue)
            commit(value);
    }

    public static void CommitDelayText(TimeEntryBlock entry, int originalValue, Action<int> commit)
    {
        var textBox = entry.TextBox;
        if (!long.TryParse(textBox.Text, out var value))
            value = string.IsNullOrWhiteSpace(textBox.Text) ? 0 : DelayFormatter.MaxMilliseconds;

        var clampedValue = DelayFormatter.ClampMilliseconds(value);
        if (clampedValue != originalValue)
            commit(clampedValue);
    }
}
