using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Macro;

namespace KeyLine;

public partial class MainWindow
{
    private bool TryUseFeature(FeatureId feature)
    {
        if (_featureGate.IsEnabled(feature))
            return true;

        ShowLockedFeatureStatus(feature);
        return false;
    }

    private void ShowLockedFeatureStatus(FeatureId feature)
    {
        SetWarningStatus(_featureGate.GetLockedFeatureMessage(feature));
    }

    private void SetWarningStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
    }

    private bool ValidateWorkspaceFeaturesForPlayback(MacroWorkspace workspace)
    {
        var validation = _macroFeatureValidator.ValidateWorkspace(workspace);
        if (validation.CanRun)
            return true;

        var message = MacroFeatureValidator.FormatErrors(validation);
        workspace.ErrorMessage = message;
        SetWarningStatus(message);
        RefreshMacroTabs();
        RefreshTimelineHeaderStatuses();
        return false;
    }

    private bool TryUseFeaturesRequiredByNodes(IEnumerable<MacroNode> nodes)
    {
        foreach (var feature in nodes
                     .Select(MacroFeatureValidator.GetRequiredFeatureForNode)
                     .OfType<FeatureId>()
                     .Distinct())
        {
            if (!TryUseFeature(feature))
                return false;
        }

        return true;
    }

    private bool TryUseFeaturesRequiredByTimeline(MacroTimeline timeline)
    {
        return TryUseFeaturesRequiredByNodes(timeline.Nodes);
    }

    private bool TryUseFeaturesRequiredByWorkspace(MacroWorkspace workspace)
    {
        return workspace.Document.Timelines.All(TryUseFeaturesRequiredByTimeline);
    }
}
