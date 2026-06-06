using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class KeyStateConditionInspector
{
    private OptionsPillBlock InputPill =>
        InputRow.GetContent<OptionsPillBlock>()!;

    public KeyStateConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);
        var pill = InputPill;

        pill.Text = ConditionInputGesture.Format(node);
        pill.PlaceholderText = "press input";
        pill.HorizontalAlignment = HorizontalAlignment.Right;
        pill.IsEnabled = canEdit;
        pill.ToolTip = TooltipNotes.ConditionKeyState;

        StyleConditionInputPill(pill);
        BindCaptureEvents(context, node, pill, canEdit);
    }

    private static void BindCaptureEvents(
        NodeInspectorContext context,
        MacroNode node,
        OptionsPillBlock pill,
        bool canEdit)
    {
        var capturedKeys = new List<int>();
        var downKeys = new HashSet<int>();
        var isCapturing = false;

        pill.MouseLeftButtonDown += (_, e) =>
        {
            if (!canEdit)
                return;

            BeginCapture();
            e.Handled = true;
        };

        pill.PreviewKeyDown += (_, e) =>
        {
            if (!isCapturing)
                return;

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                CancelCapture();
                return;
            }

            if (e.Key is Key.Back or Key.Delete)
            {
                CommitCapture(Array.Empty<int>());
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitCapture(capturedKeys);
                return;
            }

            var virtualKey = GetVirtualKeyFromKeyEvent(e);
            AddCapturedKey(virtualKey);
            UpdateCaptureText();
        };

        pill.PreviewKeyUp += (_, e) =>
        {
            if (!isCapturing)
                return;

            e.Handled = true;

            var virtualKey = GetVirtualKeyFromKeyEvent(e);

            if (virtualKey > 0)
                downKeys.Remove(virtualKey);

            if (capturedKeys.Count > 0 && downKeys.Count == 0)
                CommitCapture(capturedKeys);
        };

        pill.PreviewMouseDown += (_, e) =>
        {
            if (!isCapturing)
                return;

            if (IsShortcutCaptureStopMouseButton(e.ChangedButton))
            {
                e.Handled = true;
                FinishCaptureWithoutMouseButton();
                return;
            }

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);

            if (virtualKey <= 0)
                return;

            e.Handled = true;
            AddCapturedKey(virtualKey);
            UpdateCaptureText();
        };

        pill.PreviewMouseUp += (_, e) =>
        {
            if (!isCapturing)
                return;

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);

            if (virtualKey <= 0)
                return;

            e.Handled = true;
            downKeys.Remove(virtualKey);

            if (capturedKeys.Count > 0 && downKeys.Count == 0)
                CommitCapture(capturedKeys);
        };

        pill.LostKeyboardFocus += (_, _) =>
        {
            if (!isCapturing)
                return;

            if (capturedKeys.Count > 0)
                CommitCapture(capturedKeys);
            else
                CancelCapture();
        };

        void BeginCapture()
        {
            isCapturing = true;
            capturedKeys.Clear();
            downKeys.Clear();

            pill.Text = "press input";
            pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252));

            pill.FocusInput();
            Mouse.Capture(pill);
        }

        void AddCapturedKey(int virtualKey)
        {
            virtualKey = ShortcutGesture.NormalizeVirtualKey(virtualKey);

            if (virtualKey <= 0)
                return;

            downKeys.Add(virtualKey);

            if (!capturedKeys.Contains(virtualKey) &&
                capturedKeys.Count < ConditionInputGesture.MaxKeyCount)
            {
                capturedKeys.Add(virtualKey);
            }
        }

        void UpdateCaptureText()
        {
            pill.Text = capturedKeys.Count == 0
                ? "press input"
                : ConditionInputGesture.Format(ConditionInputGesture.Serialize(capturedKeys));
        }

        void CommitCapture(IEnumerable<int> virtualKeys)
        {
            var serialized = ConditionInputGesture.Serialize(virtualKeys);
            var keys = ConditionInputGesture.Parse(serialized);

            EndCapture();

            context.CommitNodeChange(() =>
            {
                node.ConditionShortcutKeys = serialized;
                node.ConditionVirtualKey = keys.FirstOrDefault();
                node.ConditionKeyName = keys.Length == 0
                    ? ""
                    : ShortcutGesture.Format(keys);
            });

            Keyboard.ClearFocus();
        }

        void CancelCapture()
        {
            EndCapture();
            pill.Text = ConditionInputGesture.Format(node);
            Keyboard.ClearFocus();
        }

        void FinishCaptureWithoutMouseButton()
        {
            if (capturedKeys.Count > 0)
                CommitCapture(capturedKeys);
            else
                CancelCapture();
        }

        void EndCapture()
        {
            isCapturing = false;
            capturedKeys.Clear();
            downKeys.Clear();

            pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));

            if (ReferenceEquals(Mouse.Captured, pill))
                Mouse.Capture(null);
        }
    }

    private static void StyleConditionInputPill(OptionsPillBlock pill)
    {
        pill.BorderElement.Margin = new Thickness(0);
        pill.BorderElement.Background = new SolidColorBrush(Color.FromRgb(21, 34, 53));
        pill.BorderElement.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        pill.BorderElement.BorderThickness = new Thickness(1);
        pill.BorderElement.CornerRadius = new CornerRadius(6);

        pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        pill.TextElement.FontSize = 11;
        pill.TextElement.FontWeight = FontWeights.SemiBold;
        pill.TextElement.Padding = new Thickness(7, 1, 7, 2);
        pill.TextElement.TextAlignment = TextAlignment.Right;
        pill.TextElement.TextTrimming = TextTrimming.CharacterEllipsis;
        pill.TextElement.VerticalAlignment = VerticalAlignment.Center;
    }

    private static int GetVirtualKeyFromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
    }

    private static int GetVirtualKeyFromMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.XButton1 => NativeMethods.VK_XBUTTON1,
            MouseButton.XButton2 => NativeMethods.VK_XBUTTON2,
            _ => 0
        };
    }

    private static bool IsShortcutCaptureStopMouseButton(MouseButton button) =>
        button is MouseButton.Left or MouseButton.Right or MouseButton.Middle;
}