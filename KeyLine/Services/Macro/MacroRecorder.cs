using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Input;

namespace KeyLine.Services.Recording;

public sealed class MacroRecorder
{
    private DateTime _lastInputTimeUtc;

    public bool IsRecording { get; private set; }

    public void Start()
    {
        IsRecording = true;
        _lastInputTimeUtc = DateTime.UtcNow;
    }

    public void Stop()
    {
        IsRecording = false;
    }

    public IEnumerable<MacroNode> RecordKeyDown(KeyEventArgs e, bool includeDelay)
    {
        return RecordKey(e, MacroNodeType.KeyDown, includeDelay);
    }

    public IEnumerable<MacroNode> RecordKeyUp(KeyEventArgs e, bool includeDelay)
    {
        return RecordKey(e, MacroNodeType.KeyUp, includeDelay);
    }

    public IEnumerable<MacroNode> RecordMouseClick(bool includeDelay)
    {
        return RecordMouse(MacroNodeType.MouseClick, includeDelay);
    }

    public IEnumerable<MacroNode> RecordMouseDown(int mouseButton, bool includeDelay)
    {
        return RecordMouse(MacroNodeType.MouseDown, includeDelay, mouseButton);
    }

    public IEnumerable<MacroNode> RecordMouseUp(int mouseButton, bool includeDelay)
    {
        return RecordMouse(MacroNodeType.MouseUp, includeDelay, mouseButton);
    }

    private IEnumerable<MacroNode> RecordKey(KeyEventArgs e, MacroNodeType type, bool includeDelay)
    {
        if (!VirtualKeyParser.TryFromRecordedKey(e, out var virtualKey, out var keyName))
            yield break;

        if (includeDelay)
        {
            var delayMs = GetDelaySinceLastInput();

            if (delayMs > 0)
            {
                yield return new MacroNode
                {
                    Type = MacroNodeType.Delay,
                    DelayMs = delayMs,
                    IsRecordedDelay = true
                };
            }
        }

        yield return new MacroNode
        {
            Type = type,
            KeyName = keyName,
            VirtualKey = virtualKey
        };

        _lastInputTimeUtc = DateTime.UtcNow;
    }

    private IEnumerable<MacroNode> RecordMouse(MacroNodeType type, bool includeDelay, int mouseButton = 1)
    {
        if (includeDelay)
        {
            var delayMs = GetDelaySinceLastInput();

            if (delayMs > 0)
            {
                yield return new MacroNode
                {
                    Type = MacroNodeType.Delay,
                    DelayMs = delayMs,
                    IsRecordedDelay = true
                };
            }
        }

        yield return new MacroNode
        {
            Type = type,
            MouseButton = Math.Clamp(mouseButton, 1, 5),
            KeyName = $"M{Math.Clamp(mouseButton, 1, 5)}"
        };

        _lastInputTimeUtc = DateTime.UtcNow;
    }

    private int GetDelaySinceLastInput()
    {
        return (int)(DateTime.UtcNow - _lastInputTimeUtc).TotalMilliseconds;
    }
}
