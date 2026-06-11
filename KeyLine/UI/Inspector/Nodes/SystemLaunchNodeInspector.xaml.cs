using System.IO;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Inspector.Fields;
using KeyLine.UI.Timeline;
using Microsoft.Win32;

namespace KeyLine.UI.Inspector.Nodes;

public partial class SystemLaunchNodeInspector
{
    private static readonly IReadOnlyList<LaunchKindOption> LaunchKindOptions =
    [
        new("Application", SystemLaunchKind.Application),
        new("File", SystemLaunchKind.File),
        new("Folder", SystemLaunchKind.Folder),
        new("URL", SystemLaunchKind.Url)
    ];
    private ComboBox KindCombo =>
        KindRow.GetContent<ComboBox>()!;

    public SystemLaunchNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        BindKindCombo(context, node, policy.CanEditSystemLaunch);
        BindTargetTextBox(context, node, policy.CanEditSystemLaunch);
        BindBrowseButton(context, node, policy.CanEditSystemLaunch);

        RefreshDynamicState(node);
    }

    private void BindKindCombo(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        KindCombo.ItemsSource = LaunchKindOptions;

        InspectorFieldBinder.BindCombo<SystemLaunchKind>(
            context.FieldHost,
            KindCombo,
            read: InspectorFieldBinder.SingleValue(() => node.SystemLaunchKind),
            apply: value => context.CommitNodeChange(() =>
            {
                node.SystemLaunchKind = value;
                node.SystemLaunchTarget = node.SystemLaunchTarget?.Trim() ?? "";
            }),
            isEnabled: isEnabled);
    }

    private void BindTargetTextBox(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        InspectorFieldBinder.BindText(
            context.FieldHost,
            TargetTextBox,
            read: InspectorFieldBinder.SingleText(() => node.SystemLaunchTarget),
            apply: value => context.CommitNodeChange(() => node.SystemLaunchTarget = value.Trim()),
            isEnabled: isEnabled);
    }

    private void BindBrowseButton(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        BrowseButton.IsEnabled = context.CanEditOption(isEnabled);

        BrowseButton.Click += (_, _) =>
        {
            var target = BrowseSystemLaunchTarget(node);

            if (string.IsNullOrWhiteSpace(target))
                return;

            context.CommitNodeChange(() => node.SystemLaunchTarget = target);
            TargetTextBox.Text = target;
            RefreshDynamicState(node);
        };
    }

    private void RefreshDynamicState(MacroNode node)
    {
        TargetTextBox.ToolTip = GetLaunchTargetTooltip(node.SystemLaunchKind);

        BrowseButton.Visibility = node.SystemLaunchKind == SystemLaunchKind.Url
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string GetLaunchTargetTooltip(SystemLaunchKind kind) =>
        kind switch
        {
            SystemLaunchKind.Application => "Executable path or application command.",
            SystemLaunchKind.File => "File path to open with the default application.",
            SystemLaunchKind.Folder => "Folder path to open in Explorer.",
            SystemLaunchKind.Url => "URL to open in the default browser.",
            _ => "Target to open."
        };

    private static string? BrowseSystemLaunchTarget(MacroNode node)
    {
        if (node.SystemLaunchKind == SystemLaunchKind.Folder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to open",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(node.SystemLaunchTarget)
                    ? node.SystemLaunchTarget
                    : ""
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dialog.SelectedPath
                : null;
        }

        var fileDialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            FileName = File.Exists(node.SystemLaunchTarget)
                ? node.SystemLaunchTarget
                : ""
        };

        fileDialog.Filter = node.SystemLaunchKind == SystemLaunchKind.Application
            ? "Applications (*.exe)|*.exe|All files (*.*)|*.*"
            : "All files (*.*)|*.*";

        return fileDialog.ShowDialog() == true
            ? fileDialog.FileName
            : null;
    }

    private sealed record LaunchKindOption(string Label, SystemLaunchKind Value)
    {
        public override string ToString() => Label;
    }
}