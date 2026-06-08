using System.Collections.Generic;
using System.Linq;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Macro;

namespace KeyLine;

internal sealed class SettingsFeatureValidationService
{
    private readonly MacroFeatureValidator _macroFeatureValidator;

    public SettingsFeatureValidationService(FeatureGate featureGate)
    {
        _macroFeatureValidator = new MacroFeatureValidator(featureGate);
    }

    public string ValidateAndMarkWorkspaces(IEnumerable<MacroWorkspace> workspaces)
    {
        var workspaceList = workspaces.ToList();
        foreach (var workspace in workspaceList)
        {
            var validation = _macroFeatureValidator.ValidateWorkspace(workspace);
            if (!validation.CanRun)
                workspace.ErrorMessage = MacroFeatureValidator.FormatErrors(validation);
        }

        var aggregateValidation = _macroFeatureValidator.ValidateWorkspaces(workspaceList);
        return aggregateValidation.CanRun
            ? ""
            : MacroFeatureValidator.FormatErrors(aggregateValidation);
    }
}
