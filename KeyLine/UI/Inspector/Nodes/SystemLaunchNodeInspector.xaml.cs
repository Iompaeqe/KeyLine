using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
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
        var canEdit = context.CanEditOption(isEnabled);

        KindCombo.ItemsSource = LaunchKindOptions;
        KindCombo.SelectedValue = node.SystemLaunchKind;
        KindCombo.IsEnabled = canEdit;

        KindCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (KindCombo.SelectedItem is not LaunchKindOption option)
                return;

            if (option.Value == node.SystemLaunchKind)
                return;

            context.CommitNodeChange(() =>
            {
                node.SystemLaunchKind = option.Value;
                node.SystemLaunchTarget = node.SystemLaunchTarget?.Trim() ?? "";
            });

            RefreshDynamicState(node);
        };
    }

    private void BindTargetTextBox(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        TargetTextBox.Text = node.SystemLaunchTarget;
        TargetTextBox.IsEnabled = canEdit;

        void CommitTargetText()
        {
            if (context.IsRefreshing())
                return;

            var target = TargetTextBox.Text.Trim();

            if (string.Equals(target, node.SystemLaunchTarget, StringComparison.Ordinal))
                return;

            context.CommitNodeChange(() => node.SystemLaunchTarget = target);
            RefreshDynamicState(node);
        }

        TargetTextBox.LostFocus += (_, _) => CommitTargetText();

        TargetTextBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitTargetText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
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