using System.Windows;
using System.Windows.Input;
using MacroSpammer.Domain;
using System.Windows.Media;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Steps;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly List<MacroStep> _stepDragPreviewRawSteps = new();
    private readonly List<MacroStep> _stepDragRawItems = new();
    private readonly List<MacroStep> _stepDragOriginalRawSteps = new();

    // Drag-preview layout cache.
    // Without this, every mouse move creates/measures WPF controls just to decide
    // whether the placeholder should move. That is the main performance killer.
    private readonly Dictionary<MacroStep, double> _stepDragPreviewWidthByFirstRawItem = new();
    private double _stepDragPreviewContentLeftX;
    private double _stepDragSlotWidth;
    private Point? _lastStepDragPreviewMousePoint;

    private FrameworkElement? _draggedStepGhost;
    private TranslateTransform? _draggedStepGhostTransform;

    private double _draggedStepGhostWidth;
    private double _draggedStepGhostHeight;

    private Point? _lastDroppedGhostRowsPanelPosition;

    private Point _dragGhostCurrentPosition;
    private Point _dragGhostTargetPosition;

    private bool _isDragGhostAnimating;
    private bool _timelineDragGlobalHandlersAttached;
    private bool _isDelayValueMouseEditPending;
    private bool _isMouseNodeEditPending;
    private MacroTimeline? _pendingClickSelectionTimeline;
    private MacroStep? _pendingClickSelectionStep;
    private ModifierKeys _pendingClickSelectionModifiers;
    private bool _pendingClickWasSelected;

    private static DragUiConfig DragUi => GeneratedUiConfig.Drag;

    private static double StepDragThreshold => DragUi.StepDragThreshold;
    private static double TimelineHeaderDragThreshold => DragUi.TimelineHeaderDragThreshold;

    private void AttachStepMouseHandlers(FrameworkElement element, MacroTimeline timeline, MacroStep step)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            EnsureTimelineDragGlobalHandlers();

            if (step.Type is MacroStepType.Delay or MacroStepType.RandomDelay &&
                element is DelayStepControl delayControl &&
                delayControl.IsValueEditorSource(e.OriginalSource as DependencyObject))
            {
                SelectStepFromPointer(timeline, step);

                if (!_isTimelineEditingEnabled)
                {
                    e.Handled = true;
                    return;
                }

                CancelTimelineDragState();
                SaveUndoSnapshot();
                _isDelayValueMouseEditPending = true;
                delayControl.FocusValueEditor(e.OriginalSource as DependencyObject);
                e.Handled = true;
                return;
            }

            if (step.Type is MacroStepType.CursorMove or MacroStepType.MouseDown or MacroStepType.MouseUp or MacroStepType.MouseClick &&
                element is MouseStepControl mouseControl &&
                mouseControl.IsEditorSource(e.OriginalSource as DependencyObject))
            {
                CancelTimelineDragState();
                SaveUndoSnapshot();
                _isMouseNodeEditPending = true;
                SelectStepFromPointer(timeline, step);
                e.Handled = false;
                return;
            }

            if (e.ClickCount >= 2 && !step.IsSyntheticDisplayStep)
            {
                SelectStepFromPointer(timeline, step);
                OpenInspectorFromSelection();
                e.Handled = true;
                return;
            }

            SetPendingClickSelection(
                timeline,
                step,
                Keyboard.Modifiers,
                _selection.IsStepSelected(timeline, step));

            if (!_isTimelineEditingEnabled)
            {
                e.Handled = true;
                return;
            }

            _drag.BeginStepDrag(timeline, step, e.GetPosition(TimelineRowsPanel));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (!_isTimelineEditingEnabled)
                return;

            if (_drag.DraggedStep == null ||
                _drag.DraggedStepTimeline == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(TimelineRowsPanel);

            if (!_drag.IsDraggingStep)
            {
                if (!_drag.ShouldStartStepDrag(currentPoint, StepDragThreshold))
                    return;

                _drag.MarkStepDragging();

                ApplyPendingSelectionForDrag();
                BeginStepDragPreviewModel(_drag.DraggedStepTimeline, _drag.DraggedStep);
                UpdateStepDragPreviewFromMouse(currentPoint);

                BeginWindowLevelStepDragCapture(element);
                BeginDraggedStepGhost();

                RefreshTimelineDragPreview();
            }

            var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

            if (previewChanged)
                RefreshTimelineDragPreview();

            UpdateDraggedStepGhostTargetPosition();

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_isDelayValueMouseEditPending)
            {
                _isDelayValueMouseEditPending = false;
                e.Handled = true;
                return;
            }

            if (_isMouseNodeEditPending)
            {
                _isMouseNodeEditPending = false;
                return;
            }

            if (!_drag.IsDraggingStep)
                ApplyPendingClickSelection();

            CompleteStepDrop();
            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed && _drag.DraggedStep != null)
                CancelTimelineDragState();
        };

        element.PreviewMouseRightButtonDown += (_, e) =>
        {
            CancelTimelineDragState();
            SelectStepFromPointer(timeline, step);

            if (!_isTimelineEditingEnabled)
            {
                e.Handled = true;
                return;
            }

            DeleteStep(timeline, step);

            e.Handled = true;
        };
    }

    private void SetPendingClickSelection(MacroTimeline timeline, MacroStep step, ModifierKeys modifiers, bool wasSelected)
    {
        _pendingClickSelectionTimeline = timeline;
        _pendingClickSelectionStep = step;
        _pendingClickSelectionModifiers = modifiers;
        _pendingClickWasSelected = wasSelected;
    }

    private void ApplyPendingClickSelection()
    {
        if (_pendingClickSelectionTimeline != null && _pendingClickSelectionStep != null)
            SelectStepFromStoredClick(
                _pendingClickSelectionTimeline,
                _pendingClickSelectionStep,
                _pendingClickSelectionModifiers);

        ClearPendingClickSelection();
    }

    private void ApplyPendingSelectionForDrag()
    {
        if (_pendingClickSelectionTimeline == null || _pendingClickSelectionStep == null)
            return;

        if (!_pendingClickWasSelected && _pendingClickSelectionModifiers == ModifierKeys.None)
        {
            SelectTimeline(_pendingClickSelectionTimeline);
            _selection.SelectStep(_pendingClickSelectionTimeline, _pendingClickSelectionStep);
        }

        ClearPendingClickSelection();
    }

    private void SelectStepFromStoredClick(MacroTimeline timeline, MacroStep step, ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            SelectTimeline(timeline);
            ToggleStepSelection(timeline, step);
            RefreshInspector();
            return;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift) && _selection.AnchorStep != null &&
            ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            SelectTimeline(timeline);
            SelectStepRange(timeline, step);
            RefreshInspector();
            return;
        }

        SelectTimeline(timeline);
        _selection.SelectStep(timeline, step);
        RefreshInspector();
    }

    private void ClearPendingClickSelection()
    {
        _pendingClickSelectionTimeline = null;
        _pendingClickSelectionStep = null;
        _pendingClickSelectionModifiers = ModifierKeys.None;
        _pendingClickWasSelected = false;
    }
}
