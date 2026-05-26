using System.Windows;
using System.Windows.Input;
using MacroSpammer.Domain;
using System.Windows.Media;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Controls;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly List<MacroStep> _stepDragPreviewRawSteps = new();
    private readonly List<MacroStep> _stepDragRawItems = new();

    // Drag-preview layout cache.
    // Without this, every mouse move creates/measures WPF controls just to decide
    // whether the placeholder should move. That is the main performance killer.
    private readonly Dictionary<MacroStep, double> _stepDragPreviewWidthByFirstRawItem = new();
    private double _stepDragPreviewContentLeftX;
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
                SelectTimeline(timeline);
                _selection.SelectStep(timeline, step);

                if (!_isTimelineEditingEnabled)
                {
                    e.Handled = true;
                    return;
                }

                CancelTimelineDragState();
                _isDelayValueMouseEditPending = true;
                delayControl.FocusValueEditor(e.OriginalSource as DependencyObject);
                e.Handled = true;
                return;
            }

            if (e.ClickCount >= 2 && step.Type == MacroStepType.Text)
            {
                SelectTimeline(timeline);
                _selection.SelectStep(timeline, step);

                if (!_isTimelineEditingEnabled)
                {
                    e.Handled = true;
                    return;
                }

                EditTextStep(timeline, step);
                e.Handled = true;
                return;
            }

            SelectTimeline(timeline);
            _selection.SelectStep(timeline, step);

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
            SelectTimeline(timeline);
            _selection.SelectStep(timeline, step);

            if (!_isTimelineEditingEnabled)
            {
                e.Handled = true;
                return;
            }

            DeleteStep(timeline, step);

            e.Handled = true;
        };
    }
}
