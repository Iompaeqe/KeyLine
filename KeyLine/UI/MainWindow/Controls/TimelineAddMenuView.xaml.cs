using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KeyLine.Services.Features;

namespace KeyLine.UI.MainWindow.Controls;

public enum TimelineAddMenuAction
{
    RecordInput,
    Delay,
    RandomDelay,
    Text,
    RepeatBlock,
    ConditionBlock,
    CursorMove,
    MouseScrollUp,
    MouseScrollDown,
    MouseScrollLeft,
    MouseScrollRight,
    SystemOpenLaunch,
    SystemVolumeControl,
    SystemWaitUntilWindowOpens,
    SystemFocusWindow,
    SystemSelectTargetWindow,
    BackgroundMouseDown,
    BackgroundMouseUp,
    BackgroundMouseClick
}

public sealed class TimelineAddMenuActionEventArgs : EventArgs
{
    public TimelineAddMenuActionEventArgs(TimelineAddMenuAction action)
    {
        Action = action;
    }

    public TimelineAddMenuAction Action { get; }
}

public partial class TimelineAddMenuView : UserControl
{
    public event EventHandler<TimelineAddMenuActionEventArgs>? ActionRequested;

    public TimelineAddMenuView()
    {
        InitializeComponent();
    }

    public void Open(UIElement placementTarget, FeatureGate featureGate, bool showExperimentalFeatures)
    {
        ApplyFeatureState(featureGate);
        SetExperimentalFeaturesVisible(showExperimentalFeatures);

        AddPopup.PlacementTarget = placementTarget;
        AddPopup.Placement = PlacementMode.Top;
        AddPopup.IsOpen = true;
    }

    public void Close()
    {
        AddPopup.IsOpen = false;
    }

    public void ApplyFeatureState(FeatureGate featureGate)
    {
        ApplyFeatureAddMenuButtonState(RepeatBlockMenuButton, featureGate, FeatureId.RepeatBlocks, "Repeat", "Repeat block");
        ApplyFeatureAddMenuButtonState(ConditionBlockMenuButton, featureGate, FeatureId.ConditionBlocks, "Condition", "Condition block");
        ApplySystemAddMenuState(featureGate);
    }

    public void SetExperimentalFeaturesVisible(bool isVisible)
    {
        ExperimentalAddMenuExpander.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyFeatureAddMenuButtonState(
        Button button,
        FeatureGate featureGate,
        FeatureId feature,
        string label,
        string? enabledTooltip = null)
    {
        if (featureGate.IsHidden(feature))
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        button.Visibility = Visibility.Visible;

        if (featureGate.IsEnabled(feature))
        {
            button.Content = label;
            button.Opacity = 1.0;
            button.ToolTip = enabledTooltip;
            return;
        }

        button.Content = $"{label} (locked)";
        button.Opacity = 0.55;
        button.ToolTip = featureGate.GetLockedFeatureMessage(feature);
    }

    private void ApplySystemAddMenuState(FeatureGate featureGate)
    {
        if (featureGate.IsHidden(FeatureId.SystemNodes))
        {
            SystemAddMenuExpander.Visibility = Visibility.Collapsed;
            return;
        }

        SystemAddMenuExpander.Visibility = Visibility.Visible;
        ApplyFeatureAddMenuButtonState(SystemOpenLaunchMenuButton, featureGate, FeatureId.SystemNodes, "Open", "Open/Launch");
        ApplyFeatureAddMenuButtonState(SystemVolumeControlMenuButton, featureGate, FeatureId.SystemNodes, "Volume", "Volume control");
        ApplyFeatureAddMenuButtonState(SystemWaitUntilWindowOpensMenuButton, featureGate, FeatureId.SystemNodes, "Wait Window", "Wait until window opens");
        ApplyFeatureAddMenuButtonState(SystemFocusWindowMenuButton, featureGate, FeatureId.SystemNodes, "Focus", "Focus window");
        ApplyFeatureAddMenuButtonState(SystemSelectTargetWindowMenuButton, featureGate, FeatureId.SystemNodes, "Set Target", "Set target window");
    }

    private void Request(TimelineAddMenuAction action)
    {
        ActionRequested?.Invoke(this, new TimelineAddMenuActionEventArgs(action));
    }

    private void RecordMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.RecordInput);

    private void DelayMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.Delay);

    private void RandomDelayMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.RandomDelay);

    private void TextMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.Text);

    private void RepeatBlockMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.RepeatBlock);

    private void ConditionBlockMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.ConditionBlock);

    private void CursorMoveMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.CursorMove);

    private void MouseScrollUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.MouseScrollUp);

    private void MouseScrollDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.MouseScrollDown);

    private void MouseScrollLeftMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.MouseScrollLeft);

    private void MouseScrollRightMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.MouseScrollRight);

    private void SystemOpenLaunchMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.SystemOpenLaunch);

    private void SystemVolumeControlMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.SystemVolumeControl);

    private void SystemWaitUntilWindowOpensMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.SystemWaitUntilWindowOpens);

    private void SystemFocusWindowMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.SystemFocusWindow);

    private void SystemSelectTargetWindowMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.SystemSelectTargetWindow);

    private void MouseDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.BackgroundMouseDown);

    private void MouseUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.BackgroundMouseUp);

    private void MouseClickMenuButton_Click(object sender, RoutedEventArgs e) =>
        Request(TimelineAddMenuAction.BackgroundMouseClick);
}
